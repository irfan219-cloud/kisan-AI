using Amazon.TimestreamQuery;
using Amazon.TimestreamQuery.Model;
using Amazon.TimestreamWrite;
using Amazon.TimestreamWrite.Model;
using KisanMitraAI.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KisanMitraAI.Infrastructure.Repositories.Timestream;

/// <summary>
/// Implementation of Mandi price repository using Amazon Timestream
/// </summary>
public class MandiPriceRepository : IMandiPriceRepository
{
    private readonly IAmazonTimestreamWrite _writeClient;
    private readonly IAmazonTimestreamQuery _queryClient;
    private readonly TimestreamConfiguration _config;
    private readonly ILogger<MandiPriceRepository> _logger;

    public MandiPriceRepository(
        IAmazonTimestreamWrite writeClient,
        IAmazonTimestreamQuery queryClient,
        IOptions<TimestreamConfiguration> config,
        ILogger<MandiPriceRepository> logger)
    {
        _writeClient = writeClient ?? throw new ArgumentNullException(nameof(writeClient));
        _queryClient = queryClient ?? throw new ArgumentNullException(nameof(queryClient));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IEnumerable<MandiPrice>> GetCurrentPricesAsync(
        string commodity, 
        string location,
        CancellationToken cancellationToken = default)
    {
        var query = $@"
            SELECT commodity, location, mandi_name, min_price, max_price, modal_price, time, unit
            FROM ""{_config.DatabaseName}"".""{_config.MandiPricesTableName}""
            WHERE commodity = '{EscapeSql(commodity)}'
              AND location = '{EscapeSql(location)}'
              AND time >= ago(24h)
            ORDER BY time DESC
            LIMIT 10";

        return await ExecuteQueryAsync(query, cancellationToken);
    }

    public async Task<IEnumerable<MandiPrice>> GetHistoricalPricesAsync(
        string commodity, 
        string location, 
        DateTimeOffset startDate, 
        DateTimeOffset endDate,
        CancellationToken cancellationToken = default)
    {
        var query = $@"
            SELECT commodity, location, mandi_name, min_price, max_price, modal_price, time, unit
            FROM ""{_config.DatabaseName}"".""{_config.MandiPricesTableName}""
            WHERE commodity = '{EscapeSql(commodity)}'
              AND location = '{EscapeSql(location)}'
              AND time BETWEEN from_iso8601_timestamp('{startDate:yyyy-MM-ddTHH:mm:ssZ}') 
                           AND from_iso8601_timestamp('{endDate:yyyy-MM-ddTHH:mm:ssZ}')
            ORDER BY time ASC";

        return await ExecuteQueryAsync(query, cancellationToken);
    }

    public async Task<IEnumerable<MandiPrice>> GetPriceTrendsAsync(
        string commodity, 
        string location,
        int daysBack = 30,
        CancellationToken cancellationToken = default)
    {
        var query = $@"
            SELECT commodity, location, mandi_name, 
                   AVG(min_price) as min_price, 
                   AVG(max_price) as max_price, 
                   AVG(modal_price) as modal_price, 
                   bin(time, 1d) as time,
                   unit
            FROM ""{_config.DatabaseName}"".""{_config.MandiPricesTableName}""
            WHERE commodity = '{EscapeSql(commodity)}'
              AND location = '{EscapeSql(location)}'
              AND time >= ago({daysBack}d)
            GROUP BY commodity, location, mandi_name, bin(time, 1d), unit
            ORDER BY time ASC";

        return await ExecuteQueryAsync(query, cancellationToken);
    }

    private async Task<IEnumerable<MandiPrice>> ExecuteQueryAsync(
        string query, 
        CancellationToken cancellationToken)
    {
        var prices = new List<MandiPrice>();
        
        try
        {
            var request = new QueryRequest { QueryString = query };
            var response = await _queryClient.QueryAsync(request, cancellationToken);

            foreach (var row in response.Rows)
            {
                prices.Add(ParseMandiPrice(row));
            }

            // Handle pagination if needed
            string? nextToken = response.NextToken;
            while (!string.IsNullOrEmpty(nextToken))
            {
                request.NextToken = nextToken;
                response = await _queryClient.QueryAsync(request, cancellationToken);
                
                foreach (var row in response.Rows)
                {
                    prices.Add(ParseMandiPrice(row));
                }
                
                nextToken = response.NextToken;
            }
        }
        catch (Amazon.TimestreamQuery.Model.ResourceNotFoundException ex)
        {
            _logger.LogWarning(ex, "Timestream resource not found - returning empty data for local testing");
            // Return empty list for local testing when AWS services aren't configured
            return prices;
        }
        catch (Amazon.Runtime.AmazonServiceException ex) when (ex.Message.Contains("Failed to discover the endpoint"))
        {
            _logger.LogWarning(ex, "Timestream endpoint not available - returning empty data for local testing");
            // Return empty list for local testing when AWS services aren't configured
            return prices;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying Mandi prices from Timestream");
            // Return empty list instead of throwing to allow frontend testing
            return prices;
        }

        return prices;
    }

    private MandiPrice ParseMandiPrice(Row row)
    {
        var data = row.Data;
        
        return new MandiPrice(
            commodity: GetStringValue(data[0]),
            location: GetStringValue(data[1]),
            mandiName: GetStringValue(data[2]),
            minPrice: GetDecimalValue(data[3]),
            maxPrice: GetDecimalValue(data[4]),
            modalPrice: GetDecimalValue(data[5]),
            priceDate: GetDateTimeValue(data[6]),
            unit: GetStringValue(data[7])
        );
    }

    private string GetStringValue(Datum datum) => datum.ScalarValue ?? string.Empty;
    
    private decimal GetDecimalValue(Datum datum) => 
        decimal.TryParse(datum.ScalarValue, out var value) ? value : 0m;
    
    private DateTimeOffset GetDateTimeValue(Datum datum) => 
        DateTimeOffset.TryParse(datum.ScalarValue, out var value) ? value : DateTimeOffset.UtcNow;

    private string EscapeSql(string value) => value.Replace("'", "''");
}
