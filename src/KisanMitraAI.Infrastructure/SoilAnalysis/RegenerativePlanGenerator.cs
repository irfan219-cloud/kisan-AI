using Amazon.BedrockAgentRuntime;
using Amazon.BedrockAgentRuntime.Model;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using KisanMitraAI.Core.Models;
using KisanMitraAI.Core.SoilAnalysis;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace KisanMitraAI.Infrastructure.SoilAnalysis;

public class RegenerativePlanGenerator : IRegenerativePlanGenerator
{
    private readonly IAmazonBedrockAgentRuntime _bedrockAgentClient;
    private readonly IAmazonBedrockRuntime _bedrockClient;
    private readonly ILogger<RegenerativePlanGenerator> _logger;
    private readonly string _knowledgeBaseId;
    private const int TimeoutSeconds = 60;
    private const string ModelId = "us.amazon.nova-pro-v1:0"; // Amazon Nova Pro (latest generation, no approval needed)

    public RegenerativePlanGenerator(
        IAmazonBedrockAgentRuntime bedrockAgentClient,
        IAmazonBedrockRuntime bedrockClient,
        ILogger<RegenerativePlanGenerator> logger,
        string knowledgeBaseId = "default-kb-id")
    {
        _bedrockAgentClient = bedrockAgentClient ?? throw new ArgumentNullException(nameof(bedrockAgentClient));
        _bedrockClient = bedrockClient ?? throw new ArgumentNullException(nameof(bedrockClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _knowledgeBaseId = knowledgeBaseId;
    }

    public async Task<RegenerativePlan> GeneratePlanAsync(
        SoilHealthData soilData,
        FarmProfile farmProfile,
        CancellationToken cancellationToken)
    {
        if (soilData == null)
            throw new ArgumentNullException(nameof(soilData));
        if (farmProfile == null)
            throw new ArgumentNullException(nameof(farmProfile));

        _logger.LogInformation(
            "Generating regenerative plan. FarmerId: {FarmerId}, FarmSize: {FarmSize} acres, OC: {OC}%",
            soilData.FarmerId, farmProfile.AreaInAcres, soilData.OrganicCarbon);

        try
        {
            // Create timeout cancellation token
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            // Query Knowledge Base for relevant practices
            var knowledgeBaseContext = await QueryKnowledgeBaseAsync(soilData, farmProfile, linkedCts.Token);

            // Generate plan using Bedrock with RAG context
            var plan = await GeneratePlanWithBedrockAsync(soilData, farmProfile, knowledgeBaseContext, linkedCts.Token);

            _logger.LogInformation(
                "Regenerative plan generated successfully. PlanId: {PlanId}, FarmerId: {FarmerId}",
                plan.PlanId, plan.FarmerId);

            return plan;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Plan generation cancelled by user. FarmerId: {FarmerId}", soilData.FarmerId);
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Plan generation timed out after {TimeoutSeconds} seconds. FarmerId: {FarmerId}",
                TimeoutSeconds, soilData.FarmerId);
            throw new TimeoutException($"Plan generation timed out after {TimeoutSeconds} seconds");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate regenerative plan. FarmerId: {FarmerId}", soilData.FarmerId);
            throw new InvalidOperationException("Failed to generate regenerative plan", ex);
        }
    }

    private async Task<string> QueryKnowledgeBaseAsync(
        SoilHealthData soilData,
        FarmProfile farmProfile,
        CancellationToken cancellationToken)
    {
        // Check if Knowledge Base is configured
        if (string.IsNullOrEmpty(_knowledgeBaseId) || 
            _knowledgeBaseId == "NOTCONFIG" || 
            _knowledgeBaseId == "default-kb-id")
        {
            _logger.LogWarning(
                "Knowledge Base not configured. Skipping RAG context retrieval. FarmerId: {FarmerId}",
                soilData.FarmerId);
            return "Knowledge Base not configured. Using general agricultural best practices.";
        }

        try
        {
            var query = BuildKnowledgeBaseQuery(soilData, farmProfile);

            var request = new RetrieveRequest
            {
                KnowledgeBaseId = _knowledgeBaseId,
                RetrievalQuery = new KnowledgeBaseQuery
                {
                    Text = query
                },
                RetrievalConfiguration = new KnowledgeBaseRetrievalConfiguration
                {
                    VectorSearchConfiguration = new KnowledgeBaseVectorSearchConfiguration
                    {
                        NumberOfResults = 10
                    }
                }
            };

            var response = await _bedrockAgentClient.RetrieveAsync(request, cancellationToken);

            // Combine retrieved documents into context
            var contextBuilder = new StringBuilder();
            foreach (var result in response.RetrievalResults)
            {
                contextBuilder.AppendLine($"Source: {result.Location?.S3Location?.Uri ?? "Unknown"}");
                contextBuilder.AppendLine(result.Content?.Text ?? "");
                contextBuilder.AppendLine();
            }

            return contextBuilder.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, 
                "Failed to query Knowledge Base. Proceeding without RAG context. FarmerId: {FarmerId}",
                soilData.FarmerId);
            return "Knowledge Base query failed. Using general agricultural best practices.";
        }
    }

    private string BuildKnowledgeBaseQuery(SoilHealthData soilData, FarmProfile farmProfile)
    {
        var queryBuilder = new StringBuilder();
        queryBuilder.AppendLine("Regenerative farming practices for:");
        queryBuilder.AppendLine($"- Soil type: {farmProfile.SoilType}");
        queryBuilder.AppendLine($"- Farm size: {farmProfile.AreaInAcres} acres");
        queryBuilder.AppendLine($"- Current crops: {string.Join(", ", farmProfile.CurrentCrops)}");
        queryBuilder.AppendLine($"- Organic carbon: {soilData.OrganicCarbon}%");
        queryBuilder.AppendLine($"- pH: {soilData.pH}");
        queryBuilder.AppendLine($"- Nitrogen: {soilData.Nitrogen} kg/ha");
        queryBuilder.AppendLine($"- Phosphorus: {soilData.Phosphorus} kg/ha");
        queryBuilder.AppendLine($"- Potassium: {soilData.Potassium} kg/ha");
        
        if (soilData.OrganicCarbon < 0.5f)
        {
            queryBuilder.AppendLine("Focus on carbon-building practices: composting, cover crops, crop rotation");
        }

        queryBuilder.AppendLine("Include: crop rotation strategies, cover cropping, composting practices, carbon sequestration");

        return queryBuilder.ToString();
    }

    private async Task<RegenerativePlan> GeneratePlanWithBedrockAsync(
        SoilHealthData soilData,
        FarmProfile farmProfile,
        string knowledgeBaseContext,
        CancellationToken cancellationToken)
    {
        var prompt = BuildPlanGenerationPrompt(soilData, farmProfile, knowledgeBaseContext);

        // Amazon Nova uses the Converse API format (similar to Claude)
        var request = new InvokeModelRequest
        {
            ModelId = ModelId,
            ContentType = "application/json",
            Accept = "application/json",
            Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
            {
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new[]
                        {
                            new { text = prompt }
                        }
                    }
                },
                inferenceConfig = new
                {
                    maxTokens = 4000,
                    temperature = 0.7,
                    topP = 0.9
                }
            })))
        };

        var response = await _bedrockClient.InvokeModelAsync(request, cancellationToken);

        using var reader = new StreamReader(response.Body);
        var responseBody = await reader.ReadToEndAsync(cancellationToken);
        var jsonResponse = JsonDocument.Parse(responseBody);

        // Nova response format: { "output": { "message": { "content": [{ "text": "..." }] } } }
        var planText = jsonResponse.RootElement
            .GetProperty("output")
            .GetProperty("message")
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;

        // Parse the generated plan into structured format
        return ParseGeneratedPlan(planText, soilData.FarmerId, soilData);
    }

    private string BuildPlanGenerationPrompt(
        SoilHealthData soilData,
        FarmProfile farmProfile,
        string knowledgeBaseContext)
    {
        var promptBuilder = new StringBuilder();
        
        promptBuilder.AppendLine("You are an expert agricultural advisor specializing in regenerative farming practices.");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Based on the following soil health data and farm profile, generate a detailed 12-month regenerative farming plan:");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("FARM PROFILE:");
        promptBuilder.AppendLine($"- Farm Size: {farmProfile.AreaInAcres} acres");
        promptBuilder.AppendLine($"- Soil Type: {farmProfile.SoilType}");
        promptBuilder.AppendLine($"- Irrigation: {farmProfile.IrrigationType}");
        promptBuilder.AppendLine($"- Current Crops: {string.Join(", ", farmProfile.CurrentCrops)}");
        promptBuilder.AppendLine($"- Location: {farmProfile.Coordinates.Latitude}, {farmProfile.Coordinates.Longitude}");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("SOIL HEALTH DATA:");
        promptBuilder.AppendLine($"- pH: {soilData.pH}");
        promptBuilder.AppendLine($"- Organic Carbon: {soilData.OrganicCarbon}%");
        promptBuilder.AppendLine($"- Nitrogen: {soilData.Nitrogen} kg/ha");
        promptBuilder.AppendLine($"- Phosphorus: {soilData.Phosphorus} kg/ha");
        promptBuilder.AppendLine($"- Potassium: {soilData.Potassium} kg/ha");
        promptBuilder.AppendLine($"- Sulfur: {soilData.Sulfur} ppm");
        promptBuilder.AppendLine($"- Zinc: {soilData.Zinc} ppm");
        promptBuilder.AppendLine($"- Boron: {soilData.Boron} ppm");
        promptBuilder.AppendLine($"- Iron: {soilData.Iron} ppm");
        promptBuilder.AppendLine($"- Manganese: {soilData.Manganese} ppm");
        promptBuilder.AppendLine($"- Copper: {soilData.Copper} ppm");
        promptBuilder.AppendLine();
        
        if (soilData.OrganicCarbon < 0.5f)
        {
            promptBuilder.AppendLine("CRITICAL: Organic carbon is below 0.5%. Prioritize carbon-building practices in the first 6 months.");
            promptBuilder.AppendLine();
        }

        promptBuilder.AppendLine("RELEVANT KNOWLEDGE BASE CONTEXT:");
        promptBuilder.AppendLine(knowledgeBaseContext);
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Generate a 12-month plan in the following JSON format:");
        promptBuilder.AppendLine("{");
        promptBuilder.AppendLine("  \"recommendations\": [");
        promptBuilder.AppendLine("    {");
        promptBuilder.AppendLine("      \"title\": \"Increase Organic Matter\",");
        promptBuilder.AppendLine("      \"description\": \"Add compost and green manure to improve soil health\",");
        promptBuilder.AppendLine("      \"category\": \"Soil Health\",");
        promptBuilder.AppendLine("      \"priority\": \"high\",");
        promptBuilder.AppendLine("      \"estimatedCost\": 15000,");
        promptBuilder.AppendLine("      \"expectedBenefit\": \"Increase organic carbon by 0.3% in 6 months\",");
        promptBuilder.AppendLine("      \"implementationSteps\": [\"Step 1\", \"Step 2\", \"Step 3\"]");
        promptBuilder.AppendLine("    }");
        promptBuilder.AppendLine("  ],");
        promptBuilder.AppendLine("  \"months\": [");
        promptBuilder.AppendLine("    {");
        promptBuilder.AppendLine("      \"month\": 1,");
        promptBuilder.AppendLine("      \"monthName\": \"January\",");
        promptBuilder.AppendLine("      \"practices\": [\"Practice 1\", \"Practice 2\", \"Practice 3\"],");
        promptBuilder.AppendLine("      \"rationale\": \"Explanation of why these practices are recommended\",");
        promptBuilder.AppendLine("      \"expectedOutcomes\": [\"Outcome 1\", \"Outcome 2\"]");
        promptBuilder.AppendLine("    },");
        promptBuilder.AppendLine("    ...");
        promptBuilder.AppendLine("  ],");
        promptBuilder.AppendLine("  \"carbonEstimate\": {");
        promptBuilder.AppendLine("    \"totalTonnesPerYear\": 5.2,");
        promptBuilder.AppendLine("    \"monthlyAverageTonnes\": 0.43,");
        promptBuilder.AppendLine("    \"monthlyBreakdown\": [");
        promptBuilder.AppendLine("      { \"month\": 1, \"estimatedTonnes\": 0.5, \"primaryPractice\": \"Composting\" },");
        promptBuilder.AppendLine("      ...");
        promptBuilder.AppendLine("    ]");
        promptBuilder.AppendLine("  }");
        promptBuilder.AppendLine("}");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Ensure:");
        promptBuilder.AppendLine("- Include 5-8 high-level recommendations with priorities (high/medium/low)");
        promptBuilder.AppendLine("- All 12 months are included");
        promptBuilder.AppendLine("- Each month has 3-5 specific, actionable practices");
        promptBuilder.AppendLine("- Practices are customized to the farm's soil type, size, and current crops");
        promptBuilder.AppendLine("- Carbon sequestration estimates are research-based and realistic");
        promptBuilder.AppendLine("- If organic carbon is low, prioritize carbon-building in months 1-6");

        return promptBuilder.ToString();
    }

    private RegenerativePlan ParseGeneratedPlan(string planText, string farmerId, SoilHealthData soilData)
    {
        try
        {
            // Extract JSON from the response (it might be wrapped in markdown code blocks)
            var jsonStart = planText.IndexOf('{');
            var jsonEnd = planText.LastIndexOf('}') + 1;
            
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonText = planText.Substring(jsonStart, jsonEnd - jsonStart);
                var planJson = JsonDocument.Parse(jsonText);

                // Parse recommendations
                var recommendations = new List<PlanRecommendation>();
                if (planJson.RootElement.TryGetProperty("recommendations", out var recsElement))
                {
                    foreach (var recElement in recsElement.EnumerateArray())
                    {
                        var title = recElement.GetProperty("title").GetString() ?? "";
                        var description = recElement.GetProperty("description").GetString() ?? "";
                        var category = recElement.GetProperty("category").GetString() ?? "General";
                        var priority = recElement.GetProperty("priority").GetString() ?? "medium";
                        var cost = recElement.TryGetProperty("estimatedCost", out var costProp) 
                            ? costProp.GetDecimal() 
                            : 0;
                        var benefit = recElement.GetProperty("expectedBenefit").GetString() ?? "";
                        
                        var steps = new List<string>();
                        if (recElement.TryGetProperty("implementationSteps", out var stepsElement))
                        {
                            foreach (var step in stepsElement.EnumerateArray())
                            {
                                steps.Add(step.GetString() ?? "");
                            }
                        }

                        recommendations.Add(new PlanRecommendation(
                            title, description, category, priority, cost, benefit, steps));
                    }
                }

                // Parse monthly actions
                var monthlyActions = new List<MonthlyAction>();
                var monthsArray = planJson.RootElement.GetProperty("months");

                foreach (var monthElement in monthsArray.EnumerateArray())
                {
                    var month = monthElement.GetProperty("month").GetInt32();
                    var monthName = monthElement.GetProperty("monthName").GetString() ?? $"Month {month}";
                    
                    var practices = new List<string>();
                    foreach (var practice in monthElement.GetProperty("practices").EnumerateArray())
                    {
                        practices.Add(practice.GetString() ?? "");
                    }

                    var rationale = monthElement.GetProperty("rationale").GetString() ?? "";
                    
                    var expectedOutcomes = new List<string>();
                    foreach (var outcome in monthElement.GetProperty("expectedOutcomes").EnumerateArray())
                    {
                        expectedOutcomes.Add(outcome.GetString() ?? "");
                    }

                    monthlyActions.Add(new MonthlyAction(month, monthName, practices, rationale, expectedOutcomes));
                }

                // Parse carbon estimate
                var carbonElement = planJson.RootElement.GetProperty("carbonEstimate");
                var totalTonnes = (float)carbonElement.GetProperty("totalTonnesPerYear").GetDouble();
                var monthlyAverage = (float)carbonElement.GetProperty("monthlyAverageTonnes").GetDouble();

                var monthlyBreakdown = new List<MonthlyCarbon>();
                foreach (var monthCarbon in carbonElement.GetProperty("monthlyBreakdown").EnumerateArray())
                {
                    var month = monthCarbon.GetProperty("month").GetInt32();
                    var tonnes = (float)monthCarbon.GetProperty("estimatedTonnes").GetDouble();
                    var practice = monthCarbon.GetProperty("primaryPractice").GetString() ?? "";
                    
                    monthlyBreakdown.Add(new MonthlyCarbon(month, tonnes, practice));
                }

                var carbonEstimate = new CarbonSequestrationEstimate(totalTonnes, monthlyAverage, monthlyBreakdown);

                // Calculate estimated cost savings
                var estimatedSavings = recommendations.Sum(r => r.EstimatedCost) * 0.3m; // Assume 30% savings

                return new RegenerativePlan(
                    Guid.NewGuid().ToString(),
                    farmerId,
                    recommendations,
                    monthlyActions,
                    carbonEstimate,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow.AddYears(1),
                    estimatedSavings,
                    soilData);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse generated plan JSON. FarmerId: {FarmerId}", farmerId);
        }

        // Fallback: create a basic plan if parsing fails
        return CreateFallbackPlan(farmerId, soilData);
    }

    private RegenerativePlan CreateFallbackPlan(string farmerId, SoilHealthData soilData)
    {
        var monthlyActions = new List<MonthlyAction>();
        var monthNames = new[] { "January", "February", "March", "April", "May", "June", 
                                "July", "August", "September", "October", "November", "December" };

        for (int i = 1; i <= 12; i++)
        {
            monthlyActions.Add(new MonthlyAction(
                i,
                monthNames[i - 1],
                new List<string> { "Composting", "Cover cropping", "Crop rotation" },
                "Basic regenerative practices for soil health improvement",
                new List<string> { "Improved soil structure", "Increased organic matter" }
            ));
        }

        var carbonEstimate = new CarbonSequestrationEstimate(
            3.6f,
            0.3f,
            Enumerable.Range(1, 12).Select(m => new MonthlyCarbon(m, 0.3f, "Composting")).ToList()
        );

        // Create basic recommendations
        var recommendations = new List<PlanRecommendation>
        {
            new PlanRecommendation(
                "Start Composting",
                "Begin composting organic waste to improve soil organic matter",
                "Soil Health",
                "high",
                5000,
                "Increase organic carbon by 0.2% in 6 months",
                new[] { "Set up compost bins", "Collect organic waste", "Turn compost regularly" }
            ),
            new PlanRecommendation(
                "Implement Cover Cropping",
                "Plant cover crops during off-season to protect and enrich soil",
                "Soil Health",
                "high",
                8000,
                "Reduce erosion and add nitrogen naturally",
                new[] { "Select appropriate cover crop species", "Prepare seedbed", "Plant and manage cover crops" }
            )
        };

        return new RegenerativePlan(
            Guid.NewGuid().ToString(),
            farmerId,
            recommendations,
            monthlyActions,
            carbonEstimate,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddYears(1),
            13000,
            soilData);
    }
}
