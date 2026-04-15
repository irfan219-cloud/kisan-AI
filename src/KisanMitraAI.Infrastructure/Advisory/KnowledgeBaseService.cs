using Amazon.BedrockAgentRuntime;
using Amazon.BedrockAgentRuntime.Model;
using KisanMitraAI.Core.Advisory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KisanMitraAI.Infrastructure.Advisory;

public class KnowledgeBaseService : IKnowledgeBaseService
{
    private readonly IAmazonBedrockAgentRuntime _bedrockAgentRuntime;
    private readonly IConfiguration _configuration;
    private readonly ILogger<KnowledgeBaseService> _logger;
    private readonly string _knowledgeBaseId;

    public KnowledgeBaseService(
        IAmazonBedrockAgentRuntime bedrockAgentRuntime,
        IConfiguration configuration,
        ILogger<KnowledgeBaseService> logger)
    {
        _bedrockAgentRuntime = bedrockAgentRuntime;
        _configuration = configuration;
        _logger = logger;
        _knowledgeBaseId = configuration["AWS:BedrockKnowledgeBase:KnowledgeBaseId"] 
            ?? throw new InvalidOperationException("Knowledge Base ID not configured");
    }

    public async Task<KnowledgeBaseResponse> QueryKnowledgeBaseAsync(
        string query,
        string context,
        int maxResults,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Querying Knowledge Base with query: {Query}", query);

            var request = new RetrieveAndGenerateRequest
            {
                Input = new RetrieveAndGenerateInput
                {
                    Text = query
                },
                RetrieveAndGenerateConfiguration = new RetrieveAndGenerateConfiguration
                {
                    Type = RetrieveAndGenerateType.KNOWLEDGE_BASE,
                    KnowledgeBaseConfiguration = new KnowledgeBaseRetrieveAndGenerateConfiguration
                    {
                        KnowledgeBaseId = _knowledgeBaseId,
                        ModelArn = _configuration["AWS:BedrockKnowledgeBase:ModelArn"] 
                            ?? "arn:aws:bedrock:us-east-1::foundation-model/us.amazon.nova-pro-v1:0", // Nova Pro inference profile
                        RetrievalConfiguration = new KnowledgeBaseRetrievalConfiguration
                        {
                            VectorSearchConfiguration = new KnowledgeBaseVectorSearchConfiguration
                            {
                                NumberOfResults = maxResults
                            }
                        }
                    }
                }
            };

            var response = await _bedrockAgentRuntime.RetrieveAndGenerateAsync(request, cancellationToken);

            var citations = ExtractCitations(response);
            var confidenceScore = CalculateConfidenceScore(response);

            _logger.LogInformation("Knowledge Base query completed with confidence: {Confidence}", confidenceScore);

            return new KnowledgeBaseResponse(
                Answer: response.Output.Text,
                Citations: citations,
                ConfidenceScore: confidenceScore
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying Knowledge Base");
            throw;
        }
    }

    private IEnumerable<Core.Advisory.Citation> ExtractCitations(RetrieveAndGenerateResponse response)
    {
        var citations = new List<Core.Advisory.Citation>();

        if (response.Citations != null)
        {
            foreach (var citation in response.Citations)
            {
                foreach (var retrievedReference in citation.RetrievedReferences)
                {
                    var location = retrievedReference.Location;
                    var content = retrievedReference.Content;

                    citations.Add(new Core.Advisory.Citation(
                        DocumentTitle: location?.S3Location?.Uri ?? "Unknown Document",
                        DocumentUri: location?.S3Location?.Uri ?? string.Empty,
                        RelevantExcerpt: content?.Text ?? string.Empty,
                        RelevanceScore: 0.0f // Bedrock doesn't provide explicit relevance scores in this response
                    ));
                }
            }
        }

        return citations;
    }

    private float CalculateConfidenceScore(RetrieveAndGenerateResponse response)
    {
        // Calculate confidence based on number and quality of citations
        if (response.Citations == null || !response.Citations.Any())
        {
            return 0.3f; // Low confidence if no citations
        }

        var citationCount = response.Citations.Sum(c => c.RetrievedReferences.Count);
        
        // More citations generally indicate higher confidence
        // Scale from 0.5 to 1.0 based on citation count
        var confidence = Math.Min(1.0f, 0.5f + (citationCount * 0.1f));
        
        return confidence;
    }
}
