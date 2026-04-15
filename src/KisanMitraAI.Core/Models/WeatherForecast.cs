namespace KisanMitraAI.Core.Models;

/// <summary>
/// Domain model representing weather forecast data
/// </summary>
public record WeatherForecast
{
    public string Location { get; init; }
    public IEnumerable<DailyForecast> DailyForecasts { get; init; }
    public DateTimeOffset FetchedAt { get; init; }

    public WeatherForecast(
        string location,
        IEnumerable<DailyForecast> dailyForecasts,
        DateTimeOffset fetchedAt)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            throw new ArgumentException("Location cannot be null or empty", nameof(location));
        }

        Location = location;
        DailyForecasts = dailyForecasts ?? Enumerable.Empty<DailyForecast>();
        FetchedAt = fetchedAt;
    }
}

/// <summary>
/// Domain model representing daily weather forecast
/// </summary>
public record DailyForecast
{
    public DateOnly Date { get; init; }
    public float MinTemperature { get; init; }
    public float MaxTemperature { get; init; }
    public float Rainfall { get; init; }
    public float Humidity { get; init; }
    public float SoilMoisture { get; init; }

    public DailyForecast(
        DateOnly date,
        float minTemperature,
        float maxTemperature,
        float rainfall,
        float humidity,
        float soilMoisture)
    {
        if (maxTemperature < minTemperature)
        {
            throw new ArgumentException("Max temperature cannot be less than min temperature", 
                nameof(maxTemperature));
        }

        if (rainfall < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rainfall), 
                "Rainfall cannot be negative");
        }

        if (humidity < 0 || humidity > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(humidity), 
                "Humidity must be between 0 and 100");
        }

        if (soilMoisture < 0 || soilMoisture > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(soilMoisture), 
                "Soil moisture must be between 0 and 100");
        }

        Date = date;
        MinTemperature = minTemperature;
        MaxTemperature = maxTemperature;
        Rainfall = rainfall;
        Humidity = humidity;
        SoilMoisture = soilMoisture;
    }
}
