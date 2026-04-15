using Amazon.S3;
using Amazon.TimestreamQuery;
using Amazon.TimestreamQuery.Model;
using KisanMitraAI.Core.Models;
using KisanMitraAI.Core.PlantingAdvisory;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace KisanMitraAI.Infrastructure.PlantingAdvisory;

/// <summary>
/// Retrieves soil health data from Amazon Timestream and S3 saved plans
/// </summary>
public class SoilDataRetriever : ISoilDataRetriever
{
    private readonly IAmazonTimestreamQuery _timestreamClient;
    private readonly IAmazonS3 _s3Client;
    private readonly ILogger<SoilDataRetriever> _logger;
    private readonly string _databaseName;
    private readonly string _tableName;
    private readonly string _bucketName;

    public SoilDataRetriever(
        IAmazonTimestreamQuery timestreamClient,
        IAmazonS3 s3Client,
        ILogger<SoilDataRetriever> logger,
        string databaseName = "KisanMitraDB",
        string tableName = "SoilHealthData",
        string bucketName = "kisan-mitra-documents")
    {
        _timestreamClient = timestreamClient ?? throw new ArgumentNullException(nameof(timestreamClient));
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _databaseName = databaseName;
        _tableName = tableName;
        _bucketName = bucketName;
    }

    public async Task<SoilHealthData?> GetLatestSoilDataAsync(
        string farmerId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(farmerId))
            throw new ArgumentException("Farmer ID cannot be null or empty", nameof(farmerId));

        _logger.LogInformation("Retrieving latest soil data for farmer {FarmerId}", farmerId);

        try
        {
            var query = $@"
                SELECT 
                    FarmerId, Location, Nitrogen, Phosphorus, Potassium, pH, 
                    OrganicCarbon, Sulfur, Zinc, Boron, Iron, Manganese, Copper, 
                    TestDate, LabId, time
                FROM ""{_databaseName}"".""{_tableName}""
                WHERE FarmerId = '{farmerId}'
                ORDER BY time DESC
                LIMIT 1";

            var request = new QueryRequest
            {
                QueryString = query
            };

            var response = await _timestreamClient.QueryAsync(request, cancellationToken);

            if (response.Rows.Count == 0)
            {
                _logger.LogWarning("No soil data found for farmer {FarmerId}", farmerId);
                return null;
            }

            var row = response.Rows[0];
            var columnInfo = response.ColumnInfo;

            var soilData = new SoilHealthData(
                farmerId: GetStringValue(row, columnInfo, "FarmerId"),
                location: GetStringValue(row, columnInfo, "Location"),
                nitrogen: GetFloatValue(row, columnInfo, "Nitrogen"),
                phosphorus: GetFloatValue(row, columnInfo, "Phosphorus"),
                potassium: GetFloatValue(row, columnInfo, "Potassium"),
                pH: GetFloatValue(row, columnInfo, "pH"),
                organicCarbon: GetFloatValue(row, columnInfo, "OrganicCarbon"),
                sulfur: GetFloatValue(row, columnInfo, "Sulfur"),
                zinc: GetFloatValue(row, columnInfo, "Zinc"),
                boron: GetFloatValue(row, columnInfo, "Boron"),
                iron: GetFloatValue(row, columnInfo, "Iron"),
                manganese: GetFloatValue(row, columnInfo, "Manganese"),
                copper: GetFloatValue(row, columnInfo, "Copper"),
                testDate: DateTimeOffset.Parse(GetStringValue(row, columnInfo, "TestDate")),
                labId: GetStringValue(row, columnInfo, "LabId")
            );

            _logger.LogInformation("Retrieved soil data for farmer {FarmerId} from {TestDate}", 
                farmerId, soilData.TestDate);

            return soilData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve soil data for farmer {FarmerId}", farmerId);
            throw;
        }
    }

    private static string GetStringValue(Row row, List<ColumnInfo> columnInfo, string columnName)
    {
        var index = columnInfo.FindIndex(c => c.Name == columnName);
        return index >= 0 ? row.Data[index].ScalarValue : string.Empty;
    }

    private static float GetFloatValue(Row row, List<ColumnInfo> columnInfo, string columnName)
    {
        var index = columnInfo.FindIndex(c => c.Name == columnName);
        if (index >= 0 && float.TryParse(row.Data[index].ScalarValue, out var value))
            return value;
        return 0f;
    }

    public async Task<SoilHealthData?> GetSoilDataFromPlanAsync(
        string farmerId,
        string planId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(farmerId))
            throw new ArgumentException("Farmer ID cannot be null or empty", nameof(farmerId));

        if (string.IsNullOrWhiteSpace(planId))
            throw new ArgumentException("Plan ID cannot be null or empty", nameof(planId));

        _logger.LogInformation(
            "Retrieving soil data from saved plan {PlanId} for farmer {FarmerId}",
            planId,
            farmerId);

        try
        {
            // Construct S3 key pattern for the plan
            // Plans are stored as: misc/{farmerId}/{date}/{filename}_{planId}.json
            var prefix = $"misc/{farmerId}/";

            // List objects to find the plan
            var listRequest = new Amazon.S3.Model.ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = prefix
            };

            var listResponse = await _s3Client.ListObjectsV2Async(listRequest, cancellationToken);

            // Find the object with matching planId
            var planObject = listResponse.S3Objects
                .FirstOrDefault(obj => obj.Key.Contains(planId));

            if (planObject == null)
            {
                _logger.LogWarning(
                    "Saved plan {PlanId} not found in S3 for farmer {FarmerId}",
                    planId,
                    farmerId);
                return null;
            }

            // Retrieve the plan from S3
            var getRequest = new Amazon.S3.Model.GetObjectRequest
            {
                BucketName = _bucketName,
                Key = planObject.Key
            };

            using var getResponse = await _s3Client.GetObjectAsync(getRequest, cancellationToken);
            using var reader = new StreamReader(getResponse.ResponseStream);
            var planJson = await reader.ReadToEndAsync();

            // Deserialize the plan
            var plan = JsonSerializer.Deserialize<RegenerativePlan>(planJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (plan?.SoilData == null)
            {
                _logger.LogWarning(
                    "Saved plan {PlanId} does not contain soil data for farmer {FarmerId}",
                    planId,
                    farmerId);
                return null;
            }

            _logger.LogInformation(
                "Retrieved soil data from saved plan {PlanId} for farmer {FarmerId}",
                planId,
                farmerId);

            return plan.SoilData;
        }
        catch (Amazon.S3.AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning(
                "Saved plan {PlanId} not found for farmer {FarmerId}",
                planId,
                farmerId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve soil data from plan {PlanId} for farmer {FarmerId}",
                planId,
                farmerId);
            throw;
        }
    }

    // Helper class for deserializing saved plans
    private class RegenerativePlan
    {
        public string? PlanId { get; set; }
        public string? FarmerId { get; set; }
        public SoilHealthData? SoilData { get; set; }
    }
}
