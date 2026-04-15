using Amazon.S3;
using Amazon.Textract;
using Amazon.Textract.Model;
using KisanMitraAI.Core.SoilAnalysis;
using Microsoft.Extensions.Logging;

namespace KisanMitraAI.Infrastructure.SoilAnalysis;

public class TextExtractor : ITextExtractor
{
    private readonly IAmazonTextract _textractClient;
    private readonly IAmazonS3 _s3Client;
    private readonly ILogger<TextExtractor> _logger;
    private readonly string _bucketName;
    private const int TimeoutSeconds = 10;

    public TextExtractor(
        IAmazonTextract textractClient,
        IAmazonS3 s3Client,
        ILogger<TextExtractor> logger,
        string bucketName = "kisan-mitra-documents")
    {
        _textractClient = textractClient ?? throw new ArgumentNullException(nameof(textractClient));
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _bucketName = bucketName;
    }

    public async Task<TextExtractionResult> ExtractTextAsync(
        string documentS3Key,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(documentS3Key))
            throw new ArgumentException("Document S3 key is required", nameof(documentS3Key));

        try
        {
            _logger.LogInformation("Starting text extraction for document: {DocumentS3Key}", documentS3Key);

            // Check if the file is a TXT file
            var fileExtension = Path.GetExtension(documentS3Key).ToLowerInvariant();
            if (fileExtension == ".txt")
            {
                return await ExtractTextFromTxtFileAsync(documentS3Key, cancellationToken);
            }

            // Create timeout cancellation token
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            // Call Textract AnalyzeDocument API with FORMS and TABLES analysis
            var request = new AnalyzeDocumentRequest
            {
                Document = new Document
                {
                    S3Object = new Amazon.Textract.Model.S3Object
                    {
                        Bucket = _bucketName,
                        Name = documentS3Key
                    }
                },
                FeatureTypes = new List<string> { "FORMS", "TABLES" }
            };

            var response = await _textractClient.AnalyzeDocumentAsync(request, linkedCts.Token);

            // Parse the response to extract fields and tables
            var extractedFields = ExtractKeyValuePairs(response.Blocks);
            var extractedTables = ExtractTables(response.Blocks);
            var confidenceScore = CalculateAverageConfidence(response.Blocks);

            // LOG EXTRACTED DATA TO CONSOLE FOR DEBUGGING
            Console.WriteLine("========== TEXTRACT EXTRACTION RESULTS ==========");
            Console.WriteLine($"Document: {documentS3Key}");
            Console.WriteLine($"Confidence Score: {confidenceScore:F2}%");
            Console.WriteLine($"\n--- EXTRACTED FIELDS ({extractedFields.Count}) ---");
            foreach (var field in extractedFields)
            {
                Console.WriteLine($"  {field.Key}: {field.Value}");
            }
            Console.WriteLine($"\n--- EXTRACTED TABLES ({extractedTables.Count}) ---");
            foreach (var table in extractedTables)
            {
                Console.WriteLine($"\n  Table: {table.Key}");
                Console.WriteLine($"  Headers: {string.Join(" | ", table.Value.Headers)}");
                Console.WriteLine($"  Rows: {table.Value.Rows.Count}");
                for (int i = 0; i < Math.Min(5, table.Value.Rows.Count); i++)
                {
                    Console.WriteLine($"    Row {i + 1}: {string.Join(" | ", table.Value.Rows[i])}");
                }
                if (table.Value.Rows.Count > 5)
                {
                    Console.WriteLine($"    ... and {table.Value.Rows.Count - 5} more rows");
                }
            }
            Console.WriteLine("================================================\n");

            _logger.LogInformation(
                "Text extraction completed. DocumentS3Key: {DocumentS3Key}, Fields: {FieldCount}, Tables: {TableCount}, Confidence: {Confidence:F2}",
                documentS3Key, extractedFields.Count, extractedTables.Count, confidenceScore);

            return new TextExtractionResult(
                documentS3Key,
                extractedFields,
                extractedTables,
                confidenceScore,
                DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Text extraction cancelled by user. DocumentS3Key: {DocumentS3Key}", documentS3Key);
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Text extraction timed out after {TimeoutSeconds} seconds. DocumentS3Key: {DocumentS3Key}",
                TimeoutSeconds, documentS3Key);
            throw new TimeoutException($"Text extraction timed out after {TimeoutSeconds} seconds");
        }
        catch (AmazonTextractException ex) when (ex.Message.Contains("unsupported document format") || ex.ErrorCode == "UnsupportedDocumentException")
        {
            _logger.LogError(ex, "Unsupported document format. DocumentS3Key: {DocumentS3Key}", documentS3Key);
            throw new InvalidOperationException(
                "The uploaded document format is not supported. Please ensure the file is a valid PDF, JPEG, PNG, or TXT file.", 
                ex);
        }
        catch (AmazonTextractException ex)
        {
            _logger.LogError(ex, "Textract API error during text extraction. DocumentS3Key: {DocumentS3Key}", documentS3Key);
            throw new InvalidOperationException("Failed to extract text from document", ex);
        }
    }

    private async Task<TextExtractionResult> ExtractTextFromTxtFileAsync(
        string documentS3Key,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Extracting text from TXT file: {DocumentS3Key}", documentS3Key);

            // Read the text file from S3
            var getObjectRequest = new Amazon.S3.Model.GetObjectRequest
            {
                BucketName = _bucketName,
                Key = documentS3Key
            };

            using var response = await _s3Client.GetObjectAsync(getObjectRequest, cancellationToken);
            using var reader = new StreamReader(response.ResponseStream);
            var textContent = await reader.ReadToEndAsync();

            // Parse the text content to extract key-value pairs
            var extractedFields = ParseTextContent(textContent);

            _logger.LogInformation(
                "Text extraction from TXT file completed. DocumentS3Key: {DocumentS3Key}, Fields: {FieldCount}",
                documentS3Key, extractedFields.Count);

            return new TextExtractionResult(
                documentS3Key,
                extractedFields,
                new Dictionary<string, TableData>(),
                100.0f, // TXT files have 100% confidence since we're reading directly
                DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text from TXT file. DocumentS3Key: {DocumentS3Key}", documentS3Key);
            throw new InvalidOperationException("Failed to extract text from TXT file", ex);
        }
    }

    private Dictionary<string, string> ParseTextContent(string textContent)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(textContent))
            return fields;

        // Split by lines and parse key-value pairs
        var lines = textContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            // Try to parse as key:value or key=value format
            var separators = new[] { ':', '=' };
            foreach (var separator in separators)
            {
                var parts = line.Split(new[] { separator }, 2);
                if (parts.Length == 2)
                {
                    var key = parts[0].Trim();
                    var value = parts[1].Trim();
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        fields[key] = value;
                        break;
                    }
                }
            }
        }

        // If no key-value pairs found, store the entire content as raw text
        if (fields.Count == 0)
        {
            fields["RawText"] = textContent.Trim();
        }

        return fields;
    }

    private Dictionary<string, string> ExtractKeyValuePairs(List<Block> blocks)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var blockMap = blocks.ToDictionary(b => b.Id, b => b);

        foreach (var block in blocks.Where(b => b.BlockType == BlockType.KEY_VALUE_SET && b.EntityTypes.Contains("KEY")))
        {
            var key = GetText(block, blockMap);
            var value = string.Empty;

            // Find the VALUE block associated with this KEY
            if (block.Relationships != null)
            {
                foreach (var relationship in block.Relationships.Where(r => r.Type == RelationshipType.VALUE))
                {
                    foreach (var valueId in relationship.Ids)
                    {
                        if (blockMap.TryGetValue(valueId, out var valueBlock))
                        {
                            value = GetText(valueBlock, blockMap);
                            break;
                        }
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(key))
            {
                fields[key.Trim()] = value.Trim();
            }
        }

        return fields;
    }

    private Dictionary<string, TableData> ExtractTables(List<Block> blocks)
    {
        var tables = new Dictionary<string, TableData>();
        var blockMap = blocks.ToDictionary(b => b.Id, b => b);
        var tableIndex = 0;

        foreach (var tableBlock in blocks.Where(b => b.BlockType == BlockType.TABLE))
        {
            var rows = new List<List<string>>();
            var headers = new List<string>();

            if (tableBlock.Relationships != null)
            {
                var cellBlocks = new Dictionary<(int row, int col), Block>();

                // Collect all cells
                foreach (var relationship in tableBlock.Relationships.Where(r => r.Type == RelationshipType.CHILD))
                {
                    foreach (var cellId in relationship.Ids)
                    {
                        if (blockMap.TryGetValue(cellId, out var cellBlock) && cellBlock.BlockType == BlockType.CELL)
                        {
                            var rowIndex = (cellBlock.RowIndex ?? 1) - 1;
                            var colIndex = (cellBlock.ColumnIndex ?? 1) - 1;
                            cellBlocks[(rowIndex, colIndex)] = cellBlock;

                        }
                    }
                }

                if (cellBlocks.Any())
                {
                    var maxRow = cellBlocks.Keys.Max(k => k.row);
                    var maxCol = cellBlocks.Keys.Max(k => k.col);

                    // Extract table data
                    for (int row = 0; row <= maxRow; row++)
                    {
                        var rowData = new List<string>();
                        for (int col = 0; col <= maxCol; col++)
                        {
                            var cellText = cellBlocks.TryGetValue((row, col), out var cell)
                                ? GetText(cell, blockMap)
                                : string.Empty;
                            rowData.Add(cellText);
                        }

                        if (row == 0)
                        {
                            headers = rowData;
                        }
                        else
                        {
                            rows.Add(rowData);
                        }
                    }
                }
            }

            tables[$"Table_{tableIndex++}"] = new TableData(rows, headers);
        }

        return tables;
    }

    private string GetText(Block block, Dictionary<string, Block> blockMap)
    {
        if (block.Text != null)
            return block.Text;

        var text = new List<string>();
        if (block.Relationships != null)
        {
            foreach (var relationship in block.Relationships.Where(r => r.Type == RelationshipType.CHILD))
            {
                foreach (var childId in relationship.Ids)
                {
                    if (blockMap.TryGetValue(childId, out var childBlock) && childBlock.BlockType == BlockType.WORD)
                    {
                        text.Add(childBlock.Text ?? string.Empty);
                    }
                }
            }
        }

        return string.Join(" ", text);
    }

    private float CalculateAverageConfidence(List<Block> blocks)
    {
        var confidenceScores = blocks
            .Where(b => b.Confidence.HasValue)
            .Select(b => b.Confidence.Value)
            .ToList();

        return confidenceScores.Any() ? confidenceScores.Average() : 0f;
    }
}
