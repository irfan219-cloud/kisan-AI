using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using KisanMitraAI.Core.Models;
using KisanMitraAI.Core.PlantingAdvisory;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace KisanMitraAI.Infrastructure.PlantingAdvisory;

/// <summary>
/// Analyzes optimal planting windows using Amazon Bedrock with Claude
/// </summary>
public class PlantingWindowAnalyzer : IPlantingWindowAnalyzer
{
    private readonly IAmazonBedrockRuntime _bedrockClient;
    private readonly ILogger<PlantingWindowAnalyzer> _logger;
    private const string ModelId = "us.amazon.nova-pro-v1:0"; // Nova Pro inference profile

    public PlantingWindowAnalyzer(
        IAmazonBedrockRuntime bedrockClient,
        ILogger<PlantingWindowAnalyzer> logger)
    {
        _bedrockClient = bedrockClient ?? throw new ArgumentNullException(nameof(bedrockClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IEnumerable<PlantingWindow>> AnalyzePlantingWindowsAsync(
        WeatherForecast forecast,
        SoilHealthData soilData,
        string cropType,
        CancellationToken cancellationToken = default)
    {
        if (forecast == null)
            throw new ArgumentNullException(nameof(forecast));
        if (soilData == null)
            throw new ArgumentNullException(nameof(soilData));
        if (string.IsNullOrWhiteSpace(cropType))
            throw new ArgumentException("Crop type cannot be null or empty", nameof(cropType));

        _logger.LogInformation("Analyzing planting windows for {CropType} in {Location}", 
            cropType, forecast.Location);

        try
        {
            var prompt = BuildAnalysisPrompt(forecast, soilData, cropType);

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
                        maxTokens = 2000,
                        temperature = 0.7
                    }
                })))
            };

            var response = await _bedrockClient.InvokeModelAsync(request, cancellationToken);

            using var reader = new StreamReader(response.Body);
            var responseBody = await reader.ReadToEndAsync(cancellationToken);
            
            // Parse Nova response format
            var jsonResponse = JsonDocument.Parse(responseBody);
            var analysisText = jsonResponse.RootElement
                .GetProperty("output")
                .GetProperty("message")
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString() ?? string.Empty;

            if (string.IsNullOrEmpty(analysisText))
                throw new InvalidOperationException("Empty response from Bedrock");
            
            Console.WriteLine("=== BEDROCK AI RESPONSE FOR PLANTING WINDOWS ===");
            Console.WriteLine(analysisText);
            Console.WriteLine("=== END BEDROCK RESPONSE ===");
            
            _logger.LogDebug("Bedrock response for planting windows: {Response}", analysisText);
            
            var windows = ParsePlantingWindows(analysisText);
            
            // Log parsed windows for debugging
            foreach (var window in windows)
            {
                Console.WriteLine($"PARSED WINDOW: {window.StartDate} to {window.EndDate}, Confidence: {window.ConfidenceScore}%, Rationale: {window.Rationale}");
                
                _logger.LogDebug(
                    "Parsed window: {Start} to {End}, Confidence: {Confidence}%, Rationale: {Rationale}",
                    window.StartDate, window.EndDate, window.ConfidenceScore, 
                    window.Rationale.Length > 50 ? window.Rationale.Substring(0, 50) + "..." : window.Rationale);
            }

            _logger.LogInformation("Identified {Count} planting windows for {CropType}", 
                windows.Count(), cropType);

            return windows;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze planting windows for {CropType}", cropType);
            throw;
        }
    }

    private static string BuildAnalysisPrompt(WeatherForecast forecast, SoilHealthData soilData, string cropType)
    {
        var weatherSummary = new StringBuilder();
        weatherSummary.AppendLine("Weather Forecast:");
        foreach (var day in forecast.DailyForecasts.Take(30))
        {
            weatherSummary.AppendLine($"- {day.Date}: Temp {day.MinTemperature}°C-{day.MaxTemperature}°C, " +
                $"Rainfall {day.Rainfall}mm, Humidity {day.Humidity}%, Soil Moisture {day.SoilMoisture:P0}");
        }

        var soilSummary = $@"
Soil Health Data:
- pH: {soilData.pH}
- Organic Carbon: {soilData.OrganicCarbon}%
- Nitrogen: {soilData.Nitrogen} kg/ha
- Phosphorus: {soilData.Phosphorus} kg/ha
- Potassium: {soilData.Potassium} kg/ha
- Location: {soilData.Location}";

        return $@"You are an agricultural expert analyzing optimal planting windows for {cropType}.

{weatherSummary}

{soilSummary}

TASK: Identify 2-3 optimal planting windows for {cropType} based on the weather forecast and soil conditions.

For each window, provide:
1. Start date and end date (in YYYY-MM-DD format)
2. Detailed rationale explaining why this window is optimal (MUST reference specific temperature, rainfall, and soil moisture patterns from the forecast)
3. Confidence score (0-100) based on forecast reliability and suitability for {cropType}
4. Risk factors (e.g., drought risk, flood risk, extreme temperatures)

IMPORTANT GUIDELINES:
- Confidence score should be 60-90 for good conditions, 40-60 for moderate, 20-40 for poor
- Rationale MUST be specific and reference actual weather data (temperatures, rainfall amounts, dates)
- Consider optimal temperature range for {cropType}: 15-25°C for germination
- Ensure adequate soil moisture (>40%) for germination
- Avoid periods with extreme temperatures or heavy rainfall
- Each planting window should be 20-40 days long

Return ONLY a valid JSON array (no markdown, no additional text):
[
  {{
    ""startDate"": ""2026-07-18"",
    ""endDate"": ""2026-08-17"",
    ""rationale"": ""Optimal window with temperatures 18-24°C, moderate rainfall 50-80mm, and soil moisture 45-60% ideal for {cropType} germination and establishment"",
    ""confidenceScore"": 75,
    ""riskFactors"": [""Possible late monsoon variability""]
  }}
]";
    }

    private static IEnumerable<PlantingWindow> ParsePlantingWindows(string analysisText)
    {
        try
        {
            // Extract JSON from response (may be wrapped in markdown code blocks)
            var jsonStart = analysisText.IndexOf('[');
            var jsonEnd = analysisText.LastIndexOf(']') + 1;

            if (jsonStart < 0 || jsonEnd <= jsonStart)
                throw new InvalidOperationException("No JSON array found in response");

            var jsonText = analysisText.Substring(jsonStart, jsonEnd - jsonStart);
            
            Console.WriteLine("=== EXTRACTED JSON ===");
            Console.WriteLine(jsonText);
            Console.WriteLine("=== END JSON ===");
            
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            var windowDtos = JsonSerializer.Deserialize<List<PlantingWindowDto>>(jsonText, options);

            if (windowDtos == null || windowDtos.Count == 0)
                throw new InvalidOperationException("Failed to parse planting windows");

            Console.WriteLine($"=== DESERIALIZED {windowDtos.Count} WINDOWS ===");
            foreach (var dto in windowDtos)
            {
                Console.WriteLine($"DTO: StartDate={dto.StartDate}, EndDate={dto.EndDate}, Confidence={dto.ConfidenceScore}, Rationale={dto.Rationale}");
            }
            Console.WriteLine("=== END DESERIALIZED ===");

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var windowIndex = 0;

            return windowDtos.Select(dto =>
            {
                // Provide fallback dates if AI returns empty strings
                DateOnly startDate;
                DateOnly endDate;

                if (string.IsNullOrWhiteSpace(dto.StartDate) || string.IsNullOrWhiteSpace(dto.EndDate))
                {
                    // Generate default planting windows starting from today
                    // Each window is 30 days, with 15 days gap between windows
                    var daysOffset = windowIndex * 45; // 30 days window + 15 days gap
                    startDate = today.AddDays(daysOffset);
                    endDate = startDate.AddDays(30);
                }
                else
                {
                    startDate = DateOnly.Parse(dto.StartDate);
                    endDate = DateOnly.Parse(dto.EndDate);
                }

                // Provide fallback rationale if AI returns empty string
                // IMPORTANT: Must provide rationale BEFORE incrementing windowIndex
                var rationale = string.IsNullOrWhiteSpace(dto.Rationale)
                    ? $"Planting window {windowIndex + 1} based on current weather and soil conditions"
                    : dto.Rationale;

                // Provide reasonable confidence score if AI returns 0 or invalid value
                var confidenceScore = dto.ConfidenceScore > 0 && dto.ConfidenceScore <= 100
                    ? dto.ConfidenceScore
                    : 65.0f; // Default to moderate confidence

                windowIndex++;

                return new PlantingWindow(
                    startDate,
                    endDate,
                    rationale,
                    confidenceScore,
                    dto.RiskFactors ?? new List<string>()
                );
            });
        }
        catch (JsonException jsonEx)
        {
            throw new InvalidOperationException($"Failed to parse planting window JSON: {jsonEx.Message}. Response text: {analysisText}", jsonEx);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse planting window analysis: {ex.Message}", ex);
        }
    }

    private class PlantingWindowDto
    {
        public string StartDate { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;
        public string Rationale { get; set; } = string.Empty;
        public float ConfidenceScore { get; set; }
        public List<string>? RiskFactors { get; set; }
    }
}
