namespace KisanMitraAI.Core.Models;

/// <summary>
/// Domain model representing soil health data with nutrient validation
/// </summary>
public record SoilHealthData
{
    public string FarmerId { get; init; }
    public string Location { get; init; }
    public float Nitrogen { get; init; }
    public float Phosphorus { get; init; }
    public float Potassium { get; init; }
    public float pH { get; init; }
    public float OrganicCarbon { get; init; }
    public float Sulfur { get; init; }
    public float Zinc { get; init; }
    public float Boron { get; init; }
    public float Iron { get; init; }
    public float Manganese { get; init; }
    public float Copper { get; init; }
    public DateTimeOffset TestDate { get; init; }
    public string LabId { get; init; }

    public SoilHealthData(
        string farmerId,
        string location,
        float nitrogen,
        float phosphorus,
        float potassium,
        float pH,
        float organicCarbon,
        float sulfur,
        float zinc,
        float boron,
        float iron,
        float manganese,
        float copper,
        DateTimeOffset testDate,
        string labId)
    {
        if (string.IsNullOrWhiteSpace(farmerId))
        {
            throw new ArgumentException("Farmer ID cannot be null or empty", nameof(farmerId));
        }

        if (string.IsNullOrWhiteSpace(location))
        {
            throw new ArgumentException("Location cannot be null or empty", nameof(location));
        }

        // Validate pH range (0-14)
        if (pH < 0 || pH > 14)
        {
            throw new ArgumentOutOfRangeException(nameof(pH), "pH must be between 0 and 14");
        }

        // Validate major nutrients (kg/ha range: 0-1000)
        ValidateNutrientKgHa(nitrogen, nameof(nitrogen));
        ValidateNutrientKgHa(phosphorus, nameof(phosphorus));
        ValidateNutrientKgHa(potassium, nameof(potassium));

        // Validate organic carbon percentage (0-100)
        ValidatePercentage(organicCarbon, nameof(organicCarbon));

        // Validate micronutrients (ppm/mg/kg range: 0-100)
        ValidatePercentage(sulfur, nameof(sulfur));
        ValidatePercentage(zinc, nameof(zinc));
        ValidatePercentage(boron, nameof(boron));
        ValidatePercentage(iron, nameof(iron));
        ValidatePercentage(manganese, nameof(manganese));
        ValidatePercentage(copper, nameof(copper));

        FarmerId = farmerId;
        Location = location;
        Nitrogen = nitrogen;
        Phosphorus = phosphorus;
        Potassium = potassium;
        this.pH = pH;
        OrganicCarbon = organicCarbon;
        Sulfur = sulfur;
        Zinc = zinc;
        Boron = boron;
        Iron = iron;
        Manganese = manganese;
        Copper = copper;
        TestDate = testDate;
        LabId = labId ?? string.Empty;
    }

    private static void ValidatePercentage(float value, string paramName)
    {
        if (value < 0 || value > 100)
        {
            throw new ArgumentOutOfRangeException(paramName, 
                $"{paramName} must be between 0 and 100");
        }
    }

    private static void ValidateNutrientKgHa(float value, string paramName)
    {
        if (value < 0 || value > 1000)
        {
            throw new ArgumentOutOfRangeException(paramName, 
                $"{paramName} must be between 0 and 1000 kg/ha");
        }
    }
}
