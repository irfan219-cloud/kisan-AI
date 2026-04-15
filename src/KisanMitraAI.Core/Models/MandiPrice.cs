namespace KisanMitraAI.Core.Models;

/// <summary>
/// Domain model representing Mandi (market) price information
/// </summary>
public record MandiPrice
{
    public string Commodity { get; init; }
    public string Location { get; init; }
    public string MandiName { get; init; }
    public decimal MinPrice { get; init; }
    public decimal MaxPrice { get; init; }
    public decimal ModalPrice { get; init; }
    public DateTimeOffset PriceDate { get; init; }
    public string Unit { get; init; }

    public MandiPrice(
        string commodity,
        string location,
        string mandiName,
        decimal minPrice,
        decimal maxPrice,
        decimal modalPrice,
        DateTimeOffset priceDate,
        string unit)
    {
        if (string.IsNullOrWhiteSpace(commodity))
        {
            throw new ArgumentException("Commodity cannot be null or empty", nameof(commodity));
        }

        if (string.IsNullOrWhiteSpace(location))
        {
            throw new ArgumentException("Location cannot be null or empty", nameof(location));
        }

        if (minPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minPrice), "Price cannot be negative");
        }

        if (maxPrice < minPrice)
        {
            throw new ArgumentException("Max price cannot be less than min price", nameof(maxPrice));
        }

        if (modalPrice < minPrice || modalPrice > maxPrice)
        {
            throw new ArgumentException("Modal price must be between min and max price", nameof(modalPrice));
        }

        Commodity = commodity;
        Location = location;
        MandiName = mandiName ?? string.Empty;
        MinPrice = minPrice;
        MaxPrice = maxPrice;
        ModalPrice = modalPrice;
        PriceDate = priceDate;
        Unit = unit ?? "Quintal";
    }
}
