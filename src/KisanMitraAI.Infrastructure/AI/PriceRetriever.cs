using KisanMitraAI.Core.Models;
using KisanMitraAI.Core.VoiceIntelligence;
using KisanMitraAI.Infrastructure.Repositories.Timestream;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace KisanMitraAI.Infrastructure.AI;

/// <summary>
/// Implementation of price retriever with caching
/// </summary>
public class PriceRetriever : IPriceRetriever
{
    private readonly IMandiPriceRepository _priceRepository;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PriceRetriever> _logger;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(15);
    private readonly TimeSpan _retrievalTimeout = TimeSpan.FromSeconds(10); // Increased from 1 to 10 seconds

    public PriceRetriever(
        IMandiPriceRepository priceRepository,
        IMemoryCache cache,
        ILogger<PriceRetriever> logger)
    {
        _priceRepository = priceRepository ?? throw new ArgumentNullException(nameof(priceRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IEnumerable<MandiPrice>> GetCurrentPricesAsync(
        string commodity,
        string location,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(commodity))
            throw new ArgumentException("Commodity cannot be null or empty", nameof(commodity));
        if (string.IsNullOrWhiteSpace(location))
            throw new ArgumentException("Location cannot be null or empty", nameof(location));

        var cacheKey = $"price:{commodity}:{location}:current";

        // Try to get from cache
        if (_cache.TryGetValue<IEnumerable<MandiPrice>>(cacheKey, out var cachedPrices))
        {
            _logger.LogInformation("Retrieved prices from cache for {Commodity} in {Location}",
                commodity, location);
            return cachedPrices!;
        }

        _logger.LogInformation("Retrieving current prices for {Commodity} in {Location}",
            commodity, location);

        try
        {
            using var timeoutCts = new CancellationTokenSource(_retrievalTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var prices = await _priceRepository.GetCurrentPricesAsync(
                commodity,
                location,
                linkedCts.Token);

            var priceList = prices.ToList();

            // Cache the results
            _cache.Set(cacheKey, priceList, _cacheExpiration);

            _logger.LogInformation("Retrieved {Count} prices for {Commodity} in {Location}",
                priceList.Count, commodity, location);

            return priceList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving prices for {Commodity} in {Location}",
                commodity, location);
            throw;
        }
    }

    public async Task<IEnumerable<MandiPrice>> GetHistoricalPricesAsync(
        string commodity,
        string location,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(commodity))
            throw new ArgumentException("Commodity cannot be null or empty", nameof(commodity));
        if (string.IsNullOrWhiteSpace(location))
            throw new ArgumentException("Location cannot be null or empty", nameof(location));
        if (startDate > endDate)
            throw new ArgumentException("Start date must be before end date");

        _logger.LogInformation("Retrieving historical prices for {Commodity} in {Location} from {StartDate} to {EndDate}",
            commodity, location, startDate, endDate);

        try
        {
            var prices = await _priceRepository.GetHistoricalPricesAsync(
                commodity,
                location,
                startDate,
                endDate,
                cancellationToken);

            var priceList = prices.ToList();

            _logger.LogInformation("Retrieved {Count} historical prices for {Commodity} in {Location}",
                priceList.Count, commodity, location);

            return priceList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving historical prices for {Commodity} in {Location}",
                commodity, location);
            throw;
        }
    }
}
