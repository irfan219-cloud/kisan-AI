using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using KisanMitraAI.Core.Models;
using KisanMitraAI.Core.PlantingAdvisory;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace KisanMitraAI.Infrastructure.PlantingAdvisory;

/// <summary>
/// Cost-optimized seed variety recommender using direct Bedrock InvokeModel API
/// instead of Knowledge Base + OpenSearch. Provides AI-generated, context-aware
/// recommendations for ~$2/month instead of $197/month.
/// </summary>
public class DirectBedrockSeedVarietyRecommender : ISeedVarietyRecommender
{
    private readonly IAmazonBedrockRuntime _bedrockRuntime;
    private readonly ILogger<DirectBedrockSeedVarietyRecommender> _logger;
    
    // Amazon Nova Pro model for high-quality recommendations
    private const string ModelId = "us.amazon.nova-pro-v1:0";

    public DirectBedrockSeedVarietyRecommender(
        IAmazonBedrockRuntime bedrockRuntime,
        ILogger<DirectBedrockSeedVarietyRecommender> logger)
    {
        _bedrockRuntime = bedrockRuntime ?? throw new ArgumentNullException(nameof(bedrockRuntime));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IEnumerable<SeedRecommendation>> RecommendVarietiesAsync(
        PlantingWindow window,
        SoilHealthData soilData,
        string cropType,
        CancellationToken cancellationToken = default)
    {
        if (window == null)
            throw new ArgumentNullException(nameof(window));
        if (soilData == null)
            throw new ArgumentNullException(nameof(soilData));
        if (string.IsNullOrWhiteSpace(cropType))
            throw new ArgumentException("Crop type cannot be null or empty", nameof(cropType));

        _logger.LogInformation(
            "Recommending seed varieties for {CropType} in {Location} using Direct Bedrock API",
            cropType, soilData.Location);

        try
        {
            // Build AI prompt with soil and planting window context
            var prompt = BuildRecommendationPrompt(window, soilData, cropType);

            // Invoke Bedrock with retry logic
            var responseJson = await InvokeBedrockWithRetryAsync(prompt, cancellationToken);

            // Parse AI response into seed recommendations
            var recommendations = ParseSeedRecommendations(responseJson, cropType, soilData.Location);

            Console.WriteLine($"=== GENERATED {recommendations.Count()} SEED RECOMMENDATIONS ===");
            foreach (var rec in recommendations)
            {
                Console.WriteLine($"VARIETY: {rec.VarietyName} ({rec.SeedCompany}), Maturity: {rec.MaturityDays} days, Yield: {rec.YieldPotential} t/ha");
                Console.WriteLine($"REASON: {rec.SuitabilityReason}");
            }
            Console.WriteLine("=== END RECOMMENDATIONS ===");

            _logger.LogInformation(
                "Generated {Count} AI-powered seed variety recommendations for {CropType}",
                recommendations.Count(), cropType);

            return recommendations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate AI recommendations for {CropType}, using fallback", cropType);
            
            // Fallback to hardcoded recommendations if AI fails
            return GenerateFallbackRecommendations(cropType, soilData.Location);
        }
    }

    private string BuildRecommendationPrompt(PlantingWindow window, SoilHealthData soilData, string cropType)
    {
        var soilAnalysis = AnalyzeSoilHealth(soilData);
        
        var prompt = $@"You are an expert agricultural advisor specializing in Indian farming practices.

TASK: Recommend 3-5 seed varieties for {cropType} based on the following conditions.

PLANTING WINDOW:
- Start Date: {window.StartDate:yyyy-MM-dd}
- End Date: {window.EndDate:yyyy-MM-dd}
- Duration: {window.EndDate.DayNumber - window.StartDate.DayNumber} days

LOCATION: {soilData.Location}

DETAILED SOIL ANALYSIS:
- pH: {soilData.pH:F1} ({GetpHStatus(soilData.pH)})
- Nitrogen (N): {soilData.Nitrogen:F1} kg/ha ({GetNutrientStatus(soilData.Nitrogen, "N")})
- Phosphorus (P): {soilData.Phosphorus:F1} kg/ha ({GetNutrientStatus(soilData.Phosphorus, "P")})
- Potassium (K): {soilData.Potassium:F1} kg/ha ({GetNutrientStatus(soilData.Potassium, "K")})
- Organic Carbon: {soilData.OrganicCarbon:F2}% ({GetOrganicCarbonStatus(soilData.OrganicCarbon)})
- Sulfur: {soilData.Sulfur:F1} ppm
- Zinc: {soilData.Zinc:F1} ppm
- Boron: {soilData.Boron:F1} ppm
- Iron: {soilData.Iron:F1} ppm

SOIL HEALTH SUMMARY: {soilAnalysis}

INSTRUCTIONS:
1. Recommend varieties that are SPECIFICALLY suited to the soil pH, nutrient levels, and organic carbon content shown above
2. Consider the planting window and location climate
3. Prioritize varieties from reputable Indian seed companies and research institutes (IARI, ICAR, PAU, CPRI, IIHR, etc.)
4. Include both traditional and hybrid varieties where appropriate
5. Provide realistic maturity days and yield potentials for {cropType}

For each variety, provide:
- varietyName: Specific cultivar or hybrid name
- seedCompany: Actual seed producer or research institute
- maturityDays: Realistic days to harvest for {cropType}
- suitabilityReason: Explain how it matches the SPECIFIC soil conditions (pH, N-P-K, organic carbon)
- yieldPotential: Realistic yield in tons per hectare
- keyCharacteristics: List of 3-4 important traits (disease resistance, nutrient efficiency, pH tolerance, etc.)

CRITICAL: Base recommendations on the ACTUAL soil data provided. Explain how each variety is suited to the specific pH level, nutrient availability, and organic matter content.

Return ONLY a valid JSON array in this exact format (no additional text):
[
  {{
    ""varietyName"": ""Variety Name"",
    ""seedCompany"": ""Company Name"",
    ""maturityDays"": 90,
    ""suitabilityReason"": ""This variety is suitable because it tolerates pH {soilData.pH:F1} and performs well with {GetNutrientStatus(soilData.Nitrogen, "N")} nitrogen levels..."",
    ""yieldPotential"": 4.5,
    ""keyCharacteristics"": [""characteristic1"", ""characteristic2"", ""characteristic3""]
  }}
]";

        return prompt;
    }

    private async Task<string> InvokeBedrockWithRetryAsync(string prompt, CancellationToken cancellationToken)
    {
        const int maxRetries = 3;
        const int baseDelayMs = 1000;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                // Build Amazon Nova request body
                var requestBody = new
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
                        maxTokens = 2048,
                        temperature = 0.7,
                        topP = 0.9
                    }
                };

                var requestBodyJson = JsonSerializer.Serialize(requestBody);
                var requestBodyBytes = Encoding.UTF8.GetBytes(requestBodyJson);

                var request = new InvokeModelRequest
                {
                    ModelId = ModelId,
                    Body = new MemoryStream(requestBodyBytes),
                    ContentType = "application/json",
                    Accept = "application/json"
                };

                _logger.LogDebug("Invoking Bedrock model {ModelId}, attempt {Attempt}", ModelId, attempt + 1);

                var response = await _bedrockRuntime.InvokeModelAsync(request, cancellationToken);

                // Parse response
                using var reader = new StreamReader(response.Body);
                var responseJson = await reader.ReadToEndAsync(cancellationToken);
                
                return responseJson;
            }
            catch (Amazon.BedrockRuntime.Model.ThrottlingException ex)
            {
                if (attempt == maxRetries - 1)
                {
                    _logger.LogError(ex, "Bedrock throttling exception after {Attempts} attempts", maxRetries);
                    throw;
                }

                var delay = baseDelayMs * (int)Math.Pow(2, attempt);
                var jitter = Random.Shared.Next(0, delay / 2);
                var totalDelay = delay + jitter;

                _logger.LogWarning(
                    "Bedrock throttled on attempt {Attempt}, retrying after {Delay}ms",
                    attempt + 1,
                    totalDelay);

                await Task.Delay(totalDelay, cancellationToken);
            }
            catch (Amazon.BedrockRuntime.Model.ModelTimeoutException ex)
            {
                _logger.LogError(ex, "Bedrock model timeout on attempt {Attempt}", attempt + 1);
                
                if (attempt == maxRetries - 1)
                {
                    throw;
                }

                await Task.Delay(baseDelayMs * (attempt + 1), cancellationToken);
            }
        }

        throw new InvalidOperationException("Failed to invoke Bedrock after maximum retries");
    }

    private IEnumerable<SeedRecommendation> ParseSeedRecommendations(string responseJson, string cropType, string location)
    {
        try
        {
            // Extract text from Bedrock response
            var responseText = ExtractTextFromBedrockResponse(responseJson);

            if (string.IsNullOrWhiteSpace(responseText))
            {
                _logger.LogWarning("Empty response from Bedrock, using fallback");
                return GenerateFallbackRecommendations(cropType, location);
            }

            // Find JSON array in response
            var jsonStart = responseText.IndexOf('[');
            var jsonEnd = responseText.LastIndexOf(']') + 1;

            if (jsonStart < 0 || jsonEnd <= jsonStart)
            {
                _logger.LogWarning("No JSON array found in Bedrock response, using fallback");
                return GenerateFallbackRecommendations(cropType, location);
            }

            var jsonText = responseText.Substring(jsonStart, jsonEnd - jsonStart);
            
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            
            var recommendationDtos = JsonSerializer.Deserialize<List<SeedRecommendationDto>>(jsonText, options);

            if (recommendationDtos == null || recommendationDtos.Count == 0)
            {
                _logger.LogWarning("Failed to parse Bedrock recommendations, using fallback");
                return GenerateFallbackRecommendations(cropType, location);
            }

            return recommendationDtos.Select(dto => new SeedRecommendation(
                dto.VarietyName ?? "Unknown Variety",
                dto.SeedCompany ?? "Unknown Company",
                dto.MaturityDays,
                dto.SuitabilityReason ?? "Suitable for local conditions",
                dto.YieldPotential,
                dto.KeyCharacteristics ?? new List<string>()
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Bedrock recommendations, using fallback");
            return GenerateFallbackRecommendations(cropType, location);
        }
    }

    private string ExtractTextFromBedrockResponse(string responseJson)
    {
        try
        {
            using var document = JsonDocument.Parse(responseJson);
            var root = document.RootElement;

            // Amazon Nova response format
            if (root.TryGetProperty("output", out var outputElement) &&
                outputElement.TryGetProperty("message", out var messageElement) &&
                messageElement.TryGetProperty("content", out var contentArray))
            {
                foreach (var contentItem in contentArray.EnumerateArray())
                {
                    if (contentItem.TryGetProperty("text", out var textElement))
                    {
                        return textElement.GetString() ?? string.Empty;
                    }
                }
            }

            return string.Empty;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing Bedrock response JSON");
            return string.Empty;
        }
    }

    private static string AnalyzeSoilHealth(SoilHealthData soilData)
    {
        var issues = new List<string>();
        var strengths = new List<string>();

        // pH analysis
        if (soilData.pH < 5.5) issues.Add("acidic pH may limit nutrient availability");
        else if (soilData.pH > 8.5) issues.Add("alkaline pH may cause micronutrient deficiencies");
        else if (soilData.pH >= 6.0 && soilData.pH <= 7.5) strengths.Add("optimal pH range");

        // Nitrogen analysis
        if (soilData.Nitrogen < 280) issues.Add("low nitrogen levels");
        else if (soilData.Nitrogen > 560) strengths.Add("high nitrogen availability");
        else strengths.Add("adequate nitrogen");

        // Phosphorus analysis
        if (soilData.Phosphorus < 11) issues.Add("phosphorus deficiency");
        else if (soilData.Phosphorus > 22) strengths.Add("good phosphorus levels");
        else strengths.Add("adequate phosphorus");

        // Potassium analysis
        if (soilData.Potassium < 110) issues.Add("low potassium");
        else if (soilData.Potassium > 280) strengths.Add("high potassium availability");
        else strengths.Add("adequate potassium");

        // Organic carbon analysis
        if (soilData.OrganicCarbon < 0.5) issues.Add("very low organic matter");
        else if (soilData.OrganicCarbon > 0.75) strengths.Add("good organic matter content");
        else strengths.Add("moderate organic matter");

        var summary = new StringBuilder();
        if (strengths.Any())
            summary.Append($"Strengths: {string.Join(", ", strengths)}. ");
        if (issues.Any())
            summary.Append($"Concerns: {string.Join(", ", issues)}.");

        return summary.ToString();
    }

    private static string GetpHStatus(float pH)
    {
        if (pH < 5.5) return "Strongly Acidic";
        if (pH < 6.0) return "Moderately Acidic";
        if (pH < 6.5) return "Slightly Acidic";
        if (pH < 7.5) return "Neutral";
        if (pH < 8.5) return "Slightly Alkaline";
        return "Strongly Alkaline";
    }

    private static string GetNutrientStatus(float value, string nutrient)
    {
        return nutrient switch
        {
            "N" => value < 280 ? "Low" : value < 560 ? "Medium" : "High",
            "P" => value < 11 ? "Low" : value < 22 ? "Medium" : "High",
            "K" => value < 110 ? "Low" : value < 280 ? "Medium" : "High",
            _ => "Unknown"
        };
    }

    private static string GetOrganicCarbonStatus(float oc)
    {
        if (oc < 0.5) return "Low - Needs improvement";
        if (oc < 0.75) return "Medium - Adequate";
        return "High - Excellent";
    }

    // Fallback recommendations (same as original SeedVarietyRecommender)
    private static IEnumerable<SeedRecommendation> GenerateFallbackRecommendations(string cropType, string location)
    {
        var recommendations = new List<SeedRecommendation>();

        switch (cropType.ToLower())
        {
            case "rice":
                recommendations.Add(new SeedRecommendation(
                    "Pusa Basmati 1121", "IARI", 140,
                    $"Well-suited for {location} region with good disease resistance",
                    4.5f, new List<string> { "Aromatic", "Long grain", "Drought tolerant" }));
                recommendations.Add(new SeedRecommendation(
                    "IR64", "IRRI", 120,
                    $"High-yielding variety suitable for {location} with good adaptability",
                    5.0f, new List<string> { "Medium grain", "Disease resistant", "High yield" }));
                recommendations.Add(new SeedRecommendation(
                    "Swarna", "ICAR", 145,
                    $"Popular variety in {location} with excellent grain quality",
                    4.8f, new List<string> { "Medium grain", "Good cooking quality", "Pest resistant" }));
                break;

            case "wheat":
                recommendations.Add(new SeedRecommendation(
                    "HD 2967", "IARI", 150,
                    $"High-yielding wheat variety suitable for {location}",
                    5.5f, new List<string> { "Rust resistant", "High protein", "Good chapati quality" }));
                recommendations.Add(new SeedRecommendation(
                    "PBW 343", "PAU", 145,
                    $"Popular variety in {location} with good disease resistance",
                    5.2f, new List<string> { "Early maturing", "Disease resistant", "High yield" }));
                recommendations.Add(new SeedRecommendation(
                    "DBW 187", "ICAR", 155,
                    $"Suitable for {location} with excellent grain quality",
                    5.8f, new List<string> { "Late sowing", "Heat tolerant", "High protein" }));
                break;

            case "potato":
                recommendations.Add(new SeedRecommendation(
                    "Kufri Jyoti", "CPRI", 90,
                    $"Early maturing potato variety suitable for {location}",
                    25.0f, new List<string> { "Early maturing", "White skin", "Good for processing" }));
                recommendations.Add(new SeedRecommendation(
                    "Kufri Pukhraj", "CPRI", 100,
                    $"High-yielding variety well-adapted to {location}",
                    30.0f, new List<string> { "High yield", "Yellow flesh", "Good cooking quality" }));
                recommendations.Add(new SeedRecommendation(
                    "Kufri Bahar", "CPRI", 110,
                    $"Popular variety in {location} with excellent storage quality",
                    28.0f, new List<string> { "Medium maturing", "Good storage", "White flesh" }));
                break;

            case "tomato":
                recommendations.Add(new SeedRecommendation(
                    "Pusa Ruby", "IARI", 70,
                    $"Popular determinate variety suitable for {location}",
                    50.0f, new List<string> { "Determinate", "Red fruit", "Good for fresh market" }));
                recommendations.Add(new SeedRecommendation(
                    "Arka Vikas", "IIHR", 75,
                    $"High-yielding hybrid suitable for {location}",
                    55.0f, new List<string> { "Semi-determinate", "Firm fruit", "Good shelf life" }));
                recommendations.Add(new SeedRecommendation(
                    "Himsona", "Private Seed Company", 65,
                    $"Early maturing hybrid for {location}",
                    60.0f, new List<string> { "Early maturing", "Uniform fruit", "High yield" }));
                break;

            default:
                recommendations.Add(new SeedRecommendation(
                    $"Local {cropType} Variety 1", "Local Seed Company", 120,
                    $"Traditional variety suitable for {location}",
                    4.0f, new List<string> { "Locally adapted", "Good yield", "Disease resistant" }));
                recommendations.Add(new SeedRecommendation(
                    $"Improved {cropType} Variety 2", "ICAR", 130,
                    $"Improved variety for {location} region",
                    4.5f, new List<string> { "High yield", "Disease resistant", "Good quality" }));
                recommendations.Add(new SeedRecommendation(
                    $"Hybrid {cropType} Variety 3", "Private Seed Company", 110,
                    $"Hybrid variety suitable for {location}",
                    5.0f, new List<string> { "Early maturing", "High yield", "Pest resistant" }));
                break;
        }

        return recommendations;
    }

    private class SeedRecommendationDto
    {
        public string? VarietyName { get; set; }
        public string? SeedCompany { get; set; }
        public int MaturityDays { get; set; }
        public string? SuitabilityReason { get; set; }
        public float YieldPotential { get; set; }
        public List<string>? KeyCharacteristics { get; set; }
    }
}
