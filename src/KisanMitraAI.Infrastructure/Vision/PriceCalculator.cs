using KisanMitraAI.Core.Models;
using KisanMitraAI.Core.QualityGrading;
using KisanMitraAI.Core.VoiceIntelligence;
using Microsoft.Extensions.Logging;

namespace KisanMitraAI.Infrastructure.Vision;

public class PriceCalculator : IPriceCalculator
{
    private readonly IPriceRetriever _priceRetriever;
    private readonly ILogger<PriceCalculator> _logger;

    // Grade multipliers as per requirements
    private static readonly Dictionary<QualityGrade, decimal> GradeMultipliers = new()
    {
        [QualityGrade.A] = 1.2m,
        [QualityGrade.B] = 1.0m,
        [QualityGrade.C] = 0.8m,
        [QualityGrade.Reject] = 0.0m
    };

    public PriceCalculator(
        IPriceRetriever priceRetriever,
        ILogger<PriceCalculator> logger)
    {
        _priceRetriever = priceRetriever;
        _logger = logger;
    }

    public async Task<decimal> CalculateCertifiedPriceAsync(
        QualityGrade grade,
        string commodity,
        string location,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get current Mandi prices
            var prices = await _priceRetriever.GetCurrentPricesAsync(
                commodity,
                location,
                cancellationToken);

            var mandiPrices = prices.ToList();
            
            if (!mandiPrices.Any())
            {
                _logger.LogWarning(
                    "No Mandi prices found for commodity {Commodity} in location {Location}, using fallback prices",
                    commodity, location);
                
                // Fallback prices for common commodities (per quintal in INR)
                var fallbackPrices = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Potato"] = 1350m,
                    ["Tomato"] = 1000m,
                    ["Onion"] = 1750m,
                    ["Wheat"] = 2100m,
                    ["Rice"] = 2500m,
                    ["Corn"] = 1800m
                };
                
                var fallbackModalPrice = fallbackPrices.TryGetValue(commodity, out var price) ? price : 1500m;
                var fallbackGradeMultiplier = GradeMultipliers[grade];
                var fallbackCertifiedPrice = fallbackModalPrice * fallbackGradeMultiplier;
                
                _logger.LogInformation(
                    "Using fallback price for {Commodity}. Modal Price: {ModalPrice}, Grade: {Grade}, Certified Price: {CertifiedPrice}",
                    commodity, fallbackModalPrice, grade, fallbackCertifiedPrice);
                
                return fallbackCertifiedPrice;
            }

            // Use modal price (most common price) as base
            var modalPrice = mandiPrices.First().ModalPrice;

            // Apply grade multiplier
            var gradeMultiplier = GradeMultipliers[grade];
            var certifiedPrice = modalPrice * gradeMultiplier;

            _logger.LogInformation(
                "Certified price calculated for {Commodity} in {Location}. " +
                "Grade: {Grade}, Modal Price: {ModalPrice}, Multiplier: {Multiplier}, " +
                "Certified Price: {CertifiedPrice}",
                commodity, location, grade, modalPrice, gradeMultiplier, certifiedPrice);

            return certifiedPrice;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error calculating certified price for {Commodity} in {Location}",
                commodity, location);
            throw;
        }
    }
}
