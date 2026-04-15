using Amazon.BedrockAgentRuntime;
using Amazon.BedrockAgentRuntime.Model;
using KisanMitraAI.Core.Models;
using KisanMitraAI.Core.PlantingAdvisory;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace KisanMitraAI.Infrastructure.PlantingAdvisory;

/// <summary>
/// Recommends seed varieties using Bedrock Knowledge Base
/// </summary>
public class SeedVarietyRecommender : ISeedVarietyRecommender
{
    private readonly IAmazonBedrockAgentRuntime _bedrockAgentClient;
    private readonly ILogger<SeedVarietyRecommender> _logger;
    private readonly string _knowledgeBaseId;

    public SeedVarietyRecommender(
        IAmazonBedrockAgentRuntime bedrockAgentClient,
        ILogger<SeedVarietyRecommender> logger,
        string knowledgeBaseId)
    {
        _bedrockAgentClient = bedrockAgentClient ?? throw new ArgumentNullException(nameof(bedrockAgentClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _knowledgeBaseId = knowledgeBaseId ?? throw new ArgumentNullException(nameof(knowledgeBaseId));
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

        _logger.LogInformation("Recommending seed varieties for {CropType} in {Location}", 
            cropType, soilData.Location);

        try
        {
            var query = BuildRecommendationQuery(window, soilData, cropType);

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
                        ModelArn = "arn:aws:bedrock:us-east-1::foundation-model/amazon.nova-pro-v1:0"
                    }
                }
            };

            var response = await _bedrockAgentClient.RetrieveAndGenerateAsync(request, cancellationToken);

            if (response.Output?.Text == null)
                throw new InvalidOperationException("Empty response from Knowledge Base");

            _logger.LogInformation("Knowledge Base response for {CropType}: {Response}", 
                cropType, response.Output.Text);

            var recommendations = ParseSeedRecommendations(response.Output.Text, cropType, soilData.Location);

            _logger.LogInformation("Generated {Count} seed variety recommendations for {CropType}", 
                recommendations.Count(), cropType);

            return recommendations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to recommend seed varieties for {CropType}", cropType);
            throw;
        }
    }

    private static string BuildRecommendationQuery(PlantingWindow window, SoilHealthData soilData, string cropType)
    {
        // Analyze soil health status
        var soilAnalysis = AnalyzeSoilHealth(soilData);
        
        return $@"Recommend 3-5 seed varieties for {cropType} suitable for the following conditions:

Planting Window: {window.StartDate} to {window.EndDate}
Location: {soilData.Location}

Detailed Soil Analysis:
- pH: {soilData.pH} ({GetpHStatus(soilData.pH)})
- Nitrogen (N): {soilData.Nitrogen} kg/ha ({GetNutrientStatus(soilData.Nitrogen, "N")})
- Phosphorus (P): {soilData.Phosphorus} kg/ha ({GetNutrientStatus(soilData.Phosphorus, "P")})
- Potassium (K): {soilData.Potassium} kg/ha ({GetNutrientStatus(soilData.Potassium, "K")})
- Organic Carbon: {soilData.OrganicCarbon}% ({GetOrganicCarbonStatus(soilData.OrganicCarbon)})
- Sulfur: {soilData.Sulfur} ppm
- Zinc: {soilData.Zinc} ppm
- Boron: {soilData.Boron} ppm
- Iron: {soilData.Iron} ppm

Soil Health Summary: {soilAnalysis}

IMPORTANT: Base your recommendations on the actual soil conditions above. Consider:
1. pH requirements for {cropType}
2. Nutrient availability and deficiencies
3. Varieties that perform well in similar soil conditions
4. Local adaptation to {soilData.Location}
5. Climate suitability for the planting window

For each variety, provide:
1. Variety name (specific cultivar or hybrid name)
2. Seed company (actual seed producer or research institute)
3. Maturity days (realistic days to harvest for {cropType})
4. Suitability reason (MUST explain how it matches the SPECIFIC soil pH, nutrient levels, and organic carbon content)
5. Yield potential (realistic tons per hectare for {cropType})
6. Key characteristics (disease resistance, nutrient efficiency, pH tolerance, etc.)

Format as JSON array:
[
  {{
    ""varietyName"": ""Variety Name"",
    ""seedCompany"": ""Company Name"",
    ""maturityDays"": 90,
    ""suitabilityReason"": ""This variety is suitable because [explain based on actual soil pH, N-P-K levels, and organic carbon]..."",
    ""yieldPotential"": 4.5,
    ""keyCharacteristics"": [""characteristic1"", ""characteristic2""]
  }}
]";
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

    private static IEnumerable<SeedRecommendation> ParseSeedRecommendations(string responseText, string cropType, string location)
    {
        try
        {
            // Extract JSON from response
            var jsonStart = responseText.IndexOf('[');
            var jsonEnd = responseText.LastIndexOf(']') + 1;

            if (jsonStart < 0 || jsonEnd <= jsonStart)
            {
                // No JSON found - Knowledge Base returned natural language
                // Generate fallback recommendations
                return GenerateFallbackRecommendations(cropType, location);
            }

            var jsonText = responseText.Substring(jsonStart, jsonEnd - jsonStart);
            var recommendationDtos = JsonSerializer.Deserialize<List<SeedRecommendationDto>>(jsonText);

            if (recommendationDtos == null || recommendationDtos.Count == 0)
            {
                // Failed to parse - use fallback
                return GenerateFallbackRecommendations(cropType, location);
            }

            return recommendationDtos.Select(dto => new SeedRecommendation(
                dto.VarietyName,
                dto.SeedCompany,
                dto.MaturityDays,
                dto.SuitabilityReason,
                dto.YieldPotential,
                dto.KeyCharacteristics ?? new List<string>()
            ));
        }
        catch (Exception)
        {
            // Any parsing error - use fallback
            return GenerateFallbackRecommendations(cropType, location);
        }
    }

    private static IEnumerable<SeedRecommendation> GenerateFallbackRecommendations(string cropType, string location)
    {
        // Generate generic recommendations based on crop type
        var recommendations = new List<SeedRecommendation>();

        switch (cropType.ToLower())
        {
            case "rice":
                recommendations.Add(new SeedRecommendation(
                    "Pusa Basmati 1121",
                    "IARI",
                    140,
                    $"Well-suited for {location} region with good disease resistance",
                    4.5f,
                    new List<string> { "Aromatic", "Long grain", "Drought tolerant" }
                ));
                recommendations.Add(new SeedRecommendation(
                    "IR64",
                    "IRRI",
                    120,
                    $"High-yielding variety suitable for {location} with good adaptability",
                    5.0f,
                    new List<string> { "Medium grain", "Disease resistant", "High yield" }
                ));
                recommendations.Add(new SeedRecommendation(
                    "Swarna",
                    "ICAR",
                    145,
                    $"Popular variety in {location} with excellent grain quality",
                    4.8f,
                    new List<string> { "Medium grain", "Good cooking quality", "Pest resistant" }
                ));
                break;

            case "wheat":
                recommendations.Add(new SeedRecommendation(
                    "HD 2967",
                    "IARI",
                    150,
                    $"High-yielding wheat variety suitable for {location}",
                    5.5f,
                    new List<string> { "Rust resistant", "High protein", "Good chapati quality" }
                ));
                recommendations.Add(new SeedRecommendation(
                    "PBW 343",
                    "PAU",
                    145,
                    $"Popular variety in {location} with good disease resistance",
                    5.2f,
                    new List<string> { "Early maturing", "Disease resistant", "High yield" }
                ));
                recommendations.Add(new SeedRecommendation(
                    "DBW 187",
                    "ICAR",
                    155,
                    $"Suitable for {location} with excellent grain quality",
                    5.8f,
                    new List<string> { "Late sowing", "Heat tolerant", "High protein" }
                ));
                break;

            case "maize":
            case "corn":
                recommendations.Add(new SeedRecommendation(
                    "DHM 117",
                    "IARI",
                    90,
                    $"Hybrid maize variety suitable for {location}",
                    6.0f,
                    new List<string> { "Early maturing", "Disease resistant", "High yield" }
                ));
                recommendations.Add(new SeedRecommendation(
                    "Vivek Hybrid 27",
                    "VPKAS",
                    95,
                    $"Well-adapted to {location} conditions",
                    5.5f,
                    new List<string> { "Drought tolerant", "Good grain quality", "Pest resistant" }
                ));
                recommendations.Add(new SeedRecommendation(
                    "PMH 1",
                    "PAU",
                    85,
                    $"Early maturing variety for {location}",
                    5.8f,
                    new List<string> { "Short duration", "High yield", "Disease resistant" }
                ));
                break;

            case "potato":
                recommendations.Add(new SeedRecommendation(
                    "Kufri Jyoti",
                    "CPRI",
                    90,
                    $"Early maturing potato variety suitable for {location} with good tuber quality",
                    25.0f,
                    new List<string> { "Early maturing", "White skin", "Good for processing", "Disease resistant" }
                ));
                recommendations.Add(new SeedRecommendation(
                    "Kufri Pukhraj",
                    "CPRI",
                    100,
                    $"High-yielding variety well-adapted to {location} conditions",
                    30.0f,
                    new List<string> { "High yield", "Yellow flesh", "Good cooking quality", "Late blight tolerant" }
                ));
                recommendations.Add(new SeedRecommendation(
                    "Kufri Bahar",
                    "CPRI",
                    110,
                    $"Popular variety in {location} with excellent storage quality",
                    28.0f,
                    new List<string> { "Medium maturing", "Good storage", "White flesh", "Versatile use" }
                ));
                break;

            case "tomato":
                recommendations.Add(new SeedRecommendation(
                    "Pusa Ruby",
                    "IARI",
                    70,
                    $"Popular determinate variety suitable for {location}",
                    50.0f,
                    new List<string> { "Determinate", "Red fruit", "Good for fresh market", "Disease resistant" }
                ));
                recommendations.Add(new SeedRecommendation(
                    "Arka Vikas",
                    "IIHR",
                    75,
                    $"High-yielding hybrid suitable for {location} conditions",
                    55.0f,
                    new List<string> { "Semi-determinate", "Firm fruit", "Good shelf life", "Heat tolerant" }
                ));
                recommendations.Add(new SeedRecommendation(
                    "Himsona",
                    "Private Seed Company",
                    65,
                    $"Early maturing hybrid for {location}",
                    60.0f,
                    new List<string> { "Early maturing", "Uniform fruit", "High yield", "Disease resistant" }
                ));
                break;

            case "onion":
                recommendations.Add(new SeedRecommendation(
                    "Pusa Red",
                    "IARI",
                    120,
                    $"Red onion variety suitable for {location}",
                    25.0f,
                    new List<string> { "Red bulbs", "Good storage", "Pungent", "Disease resistant" }
                ));
                recommendations.Add(new SeedRecommendation(
                    "Nasik Red",
                    "Local Selection",
                    130,
                    $"Popular variety in {location} with excellent keeping quality",
                    30.0f,
                    new List<string> { "Deep red", "Long storage", "High yield", "Good quality" }
                ));
                recommendations.Add(new SeedRecommendation(
                    "Arka Kalyan",
                    "IIHR",
                    110,
                    $"Early maturing variety for {location}",
                    28.0f,
                    new List<string> { "Early maturing", "Red bulbs", "Good yield", "Thrips tolerant" }
                ));
                break;

            case "cabbage":
                recommendations.Add(new SeedRecommendation(
                    "Golden Acre",
                    "IARI",
                    70,
                    $"Early maturing cabbage variety suitable for {location}",
                    40.0f,
                    new List<string> { "Early maturing", "Round heads", "Compact", "Good quality" }
                ));
                recommendations.Add(new SeedRecommendation(
                    "Pride of India",
                    "Private Seed Company",
                    80,
                    $"High-yielding variety for {location} conditions",
                    45.0f,
                    new List<string> { "Medium maturing", "Large heads", "Disease resistant", "Good storage" }
                ));
                recommendations.Add(new SeedRecommendation(
                    "Pusa Mukta",
                    "IARI",
                    90,
                    $"Late maturing variety suitable for {location}",
                    50.0f,
                    new List<string> { "Late maturing", "Firm heads", "High yield", "Heat tolerant" }
                ));
                break;

            case "cauliflower":
                recommendations.Add(new SeedRecommendation(
                    "Pusa Snowball K-1",
                    "IARI",
                    60,
                    $"Early variety suitable for {location}",
                    20.0f,
                    new List<string> { "Early maturing", "White curds", "Compact", "Good quality" }
                ));
                recommendations.Add(new SeedRecommendation(
                    "Pusa Deepali",
                    "IARI",
                    70,
                    $"Mid-season variety for {location} conditions",
                    25.0f,
                    new List<string> { "Medium maturing", "Large curds", "Disease resistant", "High yield" }
                ));
                recommendations.Add(new SeedRecommendation(
                    "Pusa Hybrid 2",
                    "IARI",
                    80,
                    $"Late variety suitable for {location}",
                    30.0f,
                    new List<string> { "Late maturing", "Firm curds", "Heat tolerant", "Good quality" }
                ));
                break;

            case "brinjal":
            case "eggplant":
                recommendations.Add(new SeedRecommendation(
                    "Pusa Purple Long",
                    "IARI",
                    70,
                    $"Long fruited variety suitable for {location}",
                    35.0f,
                    new List<string> { "Long fruit", "Purple color", "Good yield", "Disease resistant" }
                ));
                recommendations.Add(new SeedRecommendation(
                    "Pusa Hybrid 6",
                    "IARI",
                    65,
                    $"High-yielding hybrid for {location} conditions",
                    40.0f,
                    new List<string> { "Early maturing", "Round fruit", "High yield", "Bacterial wilt tolerant" }
                ));
                recommendations.Add(new SeedRecommendation(
                    "Arka Anand",
                    "IIHR",
                    75,
                    $"Popular variety in {location}",
                    38.0f,
                    new List<string> { "Medium maturing", "Oval fruit", "Good quality", "Pest resistant" }
                ));
                break;

            case "okra":
            case "ladyfinger":
                recommendations.Add(new SeedRecommendation(
                    "Pusa Sawani",
                    "IARI",
                    50,
                    $"Popular okra variety suitable for {location}",
                    12.0f,
                    new List<string> { "Medium maturing", "Green pods", "Tender", "High yield" }
                ));
                recommendations.Add(new SeedRecommendation(
                    "Arka Anamika",
                    "IIHR",
                    45,
                    $"Early variety for {location} conditions",
                    15.0f,
                    new List<string> { "Early maturing", "Dark green", "Yellow vein mosaic tolerant", "Good quality" }
                ));
                recommendations.Add(new SeedRecommendation(
                    "Pusa A-4",
                    "IARI",
                    55,
                    $"High-yielding variety for {location}",
                    14.0f,
                    new List<string> { "Medium maturing", "Tender pods", "Disease resistant", "Good yield" }
                ));
                break;

            case "vegetables":
            case "vegetables-mixed":
                recommendations.Add(new SeedRecommendation(
                    "Mixed Vegetable Kit - Season 1",
                    "Local Seed Company",
                    60,
                    $"Assorted vegetable seeds suitable for {location} - includes tomato, brinjal, okra",
                    35.0f,
                    new List<string> { "Multiple crops", "Staggered harvest", "Locally adapted", "Good variety" }
                ));
                recommendations.Add(new SeedRecommendation(
                    "Kitchen Garden Combo",
                    "ICAR",
                    70,
                    $"Curated vegetable selection for {location} - includes leafy greens and root vegetables",
                    40.0f,
                    new List<string> { "Diverse crops", "Nutritious", "Easy to grow", "Season appropriate" }
                ));
                recommendations.Add(new SeedRecommendation(
                    "Premium Vegetable Collection",
                    "Private Seed Company",
                    65,
                    $"High-quality vegetable seeds for {location} - includes premium varieties",
                    45.0f,
                    new List<string> { "Premium quality", "High yield", "Disease resistant", "Market preferred" }
                ));
                break;

            case "cotton":
                recommendations.Add(new SeedRecommendation(
                    "Bt Cotton Hybrid",
                    "Private Seed Company",
                    150,
                    $"Bollworm resistant cotton suitable for {location}",
                    20.0f,
                    new List<string> { "Bt technology", "Bollworm resistant", "High yield", "Good fiber quality" }
                ));
                recommendations.Add(new SeedRecommendation(
                    "Suraj",
                    "CICR",
                    160,
                    $"Non-Bt variety for {location} conditions",
                    15.0f,
                    new List<string> { "Non-Bt", "Good fiber", "Drought tolerant", "Medium yield" }
                ));
                recommendations.Add(new SeedRecommendation(
                    "RCH 2",
                    "Private Seed Company",
                    155,
                    $"Popular hybrid in {location}",
                    22.0f,
                    new List<string> { "Bt hybrid", "High yield", "Good boll retention", "Disease resistant" }
                ));
                break;

            case "sugarcane":
                recommendations.Add(new SeedRecommendation(
                    "Co 86032",
                    "SBI",
                    365,
                    $"High-yielding sugarcane variety for {location}",
                    100.0f,
                    new List<string> { "High sugar content", "Disease resistant", "Good ratooning", "Drought tolerant" }
                ));
                recommendations.Add(new SeedRecommendation(
                    "CoC 671",
                    "SBI",
                    360,
                    $"Early maturing variety suitable for {location}",
                    95.0f,
                    new List<string> { "Early maturing", "High yield", "Red rot resistant", "Good quality" }
                ));
                recommendations.Add(new SeedRecommendation(
                    "Co 0238",
                    "SBI",
                    370,
                    $"Popular variety in {location}",
                    105.0f,
                    new List<string> { "High sugar", "Disease resistant", "Good tillering", "Wide adaptability" }
                ));
                break;

            case "soybean":
                recommendations.Add(new SeedRecommendation(
                    "JS 335",
                    "ICAR",
                    95,
                    $"Popular soybean variety for {location}",
                    2.5f,
                    new List<string> { "High yield", "Yellow seed", "Good oil content", "Disease resistant" }
                ));
                recommendations.Add(new SeedRecommendation(
                    "JS 95-60",
                    "ICAR",
                    100,
                    $"High-yielding variety suitable for {location}",
                    2.8f,
                    new List<string> { "High yield", "Bold seed", "Rust resistant", "Good quality" }
                ));
                recommendations.Add(new SeedRecommendation(
                    "MAUS 71",
                    "MPKV",
                    90,
                    $"Early maturing variety for {location}",
                    2.3f,
                    new List<string> { "Early maturing", "Yellow seed", "Disease resistant", "Good adaptability" }
                ));
                break;

            case "pulses":
                recommendations.Add(new SeedRecommendation(
                    "Pusa 256 (Arhar)",
                    "IARI",
                    150,
                    $"Pigeon pea variety suitable for {location}",
                    1.5f,
                    new List<string> { "Medium duration", "Wilt resistant", "Good yield", "High protein" }
                ));
                recommendations.Add(new SeedRecommendation(
                    "Pusa 372 (Moong)",
                    "IARI",
                    65,
                    $"Green gram variety for {location}",
                    1.2f,
                    new List<string> { "Short duration", "Yellow mosaic resistant", "Good quality", "High yield" }
                ));
                recommendations.Add(new SeedRecommendation(
                    "Pusa 9531 (Urad)",
                    "IARI",
                    70,
                    $"Black gram variety suitable for {location}",
                    1.0f,
                    new List<string> { "Medium duration", "Disease resistant", "Bold seed", "Good quality" }
                ));
                break;

            default:
                // Generic recommendations for unknown crops
                recommendations.Add(new SeedRecommendation(
                    $"Local {cropType} Variety 1",
                    "Local Seed Company",
                    120,
                    $"Traditional variety suitable for {location}",
                    4.0f,
                    new List<string> { "Locally adapted", "Good yield", "Disease resistant" }
                ));
                recommendations.Add(new SeedRecommendation(
                    $"Improved {cropType} Variety 2",
                    "ICAR",
                    130,
                    $"Improved variety for {location} region",
                    4.5f,
                    new List<string> { "High yield", "Disease resistant", "Good quality" }
                ));
                recommendations.Add(new SeedRecommendation(
                    $"Hybrid {cropType} Variety 3",
                    "Private Seed Company",
                    110,
                    $"Hybrid variety suitable for {location}",
                    5.0f,
                    new List<string> { "Early maturing", "High yield", "Pest resistant" }
                ));
                break;
        }

        return recommendations;
    }

    private class SeedRecommendationDto
    {
        public string VarietyName { get; set; } = string.Empty;
        public string SeedCompany { get; set; } = string.Empty;
        public int MaturityDays { get; set; }
        public string SuitabilityReason { get; set; } = string.Empty;
        public float YieldPotential { get; set; }
        public List<string>? KeyCharacteristics { get; set; }
    }
}
