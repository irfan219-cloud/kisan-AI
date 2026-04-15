using KisanMitraAI.Core.VoiceIntelligence;
using KisanMitraAI.Core.VoiceIntelligence.Models;
using KisanMitraAI.Core.Advisory;
using Microsoft.Extensions.Logging;

namespace KisanMitraAI.Infrastructure.AI;

/// <summary>
/// Handles voice query processing workflow
/// </summary>
public class VoiceQueryHandler : IVoiceQueryHandler
{
    private readonly ITranscriptionService _transcriptionService;
    private readonly IQueryParser _queryParser;
    private readonly IPriceRetriever _priceRetriever;
    private readonly IResponseGenerator _responseGenerator;
    private readonly IVoiceSynthesizer _voiceSynthesizer;
    private readonly IKnowledgeBaseService _knowledgeBaseService;
    private readonly ILogger<VoiceQueryHandler> _logger;
    private readonly Dictionary<string, string> _dialectToLanguageMap;

    public VoiceQueryHandler(
        ITranscriptionService transcriptionService,
        IQueryParser queryParser,
        IPriceRetriever priceRetriever,
        IResponseGenerator responseGenerator,
        IVoiceSynthesizer voiceSynthesizer,
        IKnowledgeBaseService knowledgeBaseService,
        ILogger<VoiceQueryHandler> logger)
    {
        _transcriptionService = transcriptionService ?? throw new ArgumentNullException(nameof(transcriptionService));
        _queryParser = queryParser ?? throw new ArgumentNullException(nameof(queryParser));
        _priceRetriever = priceRetriever ?? throw new ArgumentNullException(nameof(priceRetriever));
        _responseGenerator = responseGenerator ?? throw new ArgumentNullException(nameof(responseGenerator));
        _voiceSynthesizer = voiceSynthesizer ?? throw new ArgumentNullException(nameof(voiceSynthesizer));
        _knowledgeBaseService = knowledgeBaseService ?? throw new ArgumentNullException(nameof(knowledgeBaseService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Map dialects to language codes for Amazon Transcribe
        _dialectToLanguageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Bundelkhandi", "hi-IN" },
            { "Bhojpuri", "hi-IN" },
            { "Marwari", "hi-IN" },
            { "Awadhi", "hi-IN" },
            { "Braj", "hi-IN" },
            { "Magahi", "hi-IN" },
            { "Maithili", "hi-IN" },
            { "Chhattisgarhi", "hi-IN" },
            { "Haryanvi", "hi-IN" },
            { "Rajasthani", "hi-IN" },
            { "Hindi", "hi-IN" },
            { "Tamil", "ta-IN" },
            { "Telugu", "te-IN" },
            { "Bengali", "bn-IN" },
            { "Marathi", "mr-IN" }
        };
    }

    public async Task<VoiceQueryResponse> ProcessVoiceQueryAsync(
        Stream audioStream,
        string dialect,
        string farmerId,
        CancellationToken cancellationToken)
    {
        if (audioStream == null)
            throw new ArgumentNullException(nameof(audioStream));
        if (string.IsNullOrWhiteSpace(dialect))
            throw new ArgumentException("Dialect cannot be null or empty", nameof(dialect));
        if (string.IsNullOrWhiteSpace(farmerId))
            throw new ArgumentException("Farmer ID cannot be null or empty", nameof(farmerId));

        _logger.LogInformation("Processing voice query for farmer {FarmerId} in dialect {Dialect}",
            farmerId, dialect);

        try
        {
            // Validate audio format
            ValidateAudioStream(audioStream);

            // Get language code for dialect
            var languageCode = GetLanguageCode(dialect);

            // Step 1: Transcribe audio
            var transcriptionResult = await _transcriptionService.TranscribeAsync(
                audioStream,
                languageCode,
                cancellationToken);

            _logger.LogInformation("Transcription completed: {Text}", transcriptionResult.TranscribedText);

            // Step 2: Parse query to extract commodity and location
            var parsedQuery = await _queryParser.ParseQueryAsync(
                transcriptionResult.TranscribedText,
                $"Farmer ID: {farmerId}, Dialect: {dialect}",
                cancellationToken);

            _logger.LogInformation("Query parsed: Commodity={Commodity}, Location={Location}",
                parsedQuery.Commodity, parsedQuery.Location);

            // Handle general questions (not price queries) using knowledge base
            if (parsedQuery.RequiresClarification || string.IsNullOrEmpty(parsedQuery.Commodity))
            {
                _logger.LogInformation("Query is not a price query, using knowledge base for general question");
                
                var kbResponse = await _knowledgeBaseService.QueryKnowledgeBaseAsync(
                    transcriptionResult.TranscribedText,
                    $"Farmer ID: {farmerId}, Dialect: {dialect}",
                    5,
                    cancellationToken);

                _logger.LogInformation("Knowledge base response: {Answer}", kbResponse.Answer);

                // Synthesize voice response
                var (kbAudioStream, kbAudioS3Uri) = await _voiceSynthesizer.SynthesizeAsync(
                    kbResponse.Answer,
                    string.Empty,
                    languageCode,
                    cancellationToken);

                _logger.LogInformation("Knowledge base audio synthesized with S3 URL: {S3Url}", kbAudioS3Uri);

                return new VoiceQueryResponse(
                    transcriptionResult.TranscribedText,
                    Array.Empty<Core.Models.MandiPrice>(),
                    kbAudioS3Uri,
                    kbResponse.Answer,
                    transcriptionResult.Confidence,
                    dialect,
                    DateTimeOffset.UtcNow);
            }

            // Step 3: Retrieve prices from Timestream (for price queries)
            var prices = await _priceRetriever.GetCurrentPricesAsync(
                parsedQuery.Commodity,
                parsedQuery.Location,
                cancellationToken);

            var priceList = prices.ToList();
            _logger.LogInformation("Retrieved {Count} prices for {Commodity} in {Location}",
                priceList.Count, parsedQuery.Commodity, parsedQuery.Location);

            // Step 4: Generate natural language response
            var responseText = await _responseGenerator.GenerateResponseAsync(
                parsedQuery,
                priceList,
                dialect,
                cancellationToken);

            _logger.LogInformation("Generated response: {Response}", responseText);

            // Step 5: Synthesize voice response
            string audioS3Uri = string.Empty;
            try
            {
                var (synthesizedAudioStream, audioUrl) = await _voiceSynthesizer.SynthesizeAsync(
                    responseText,
                    string.Empty, // Let synthesizer choose voice based on language
                    languageCode,
                    cancellationToken);

                audioS3Uri = audioUrl;
                _logger.LogInformation("Voice synthesis completed with S3 URL: {S3Url}", audioS3Uri);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("timed out"))
            {
                _logger.LogWarning(ex, "Voice synthesis timed out, returning text-only response");
                // Continue without audio - the text response is still valid
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Voice synthesis failed, returning text-only response");
                // Continue without audio - the text response is still valid
            }

            // Return complete response (with or without audio)
            return new VoiceQueryResponse(
                transcriptionResult.TranscribedText,
                priceList.ToArray(),
                audioS3Uri,
                responseText,
                transcriptionResult.Confidence,
                dialect,
                DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing voice query for farmer {FarmerId}", farmerId);
            throw;
        }
    }

    private void ValidateAudioStream(Stream audioStream)
    {
        if (!audioStream.CanRead)
            throw new ArgumentException("Audio stream must be readable", nameof(audioStream));

        if (audioStream.Length == 0)
            throw new ArgumentException("Audio stream cannot be empty", nameof(audioStream));

        // Check size limit (e.g., 10 MB)
        const long maxSizeBytes = 10 * 1024 * 1024;
        if (audioStream.Length > maxSizeBytes)
            throw new ArgumentException($"Audio file size exceeds maximum allowed size of {maxSizeBytes} bytes", nameof(audioStream));
    }

    private string GetLanguageCode(string dialect)
    {
        if (_dialectToLanguageMap.TryGetValue(dialect, out var languageCode))
            return languageCode;

        _logger.LogWarning("Unknown dialect {Dialect}, defaulting to Hindi", dialect);
        return "hi-IN";
    }
}
