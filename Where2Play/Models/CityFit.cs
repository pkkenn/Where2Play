namespace Where2Play.Models;

/// <summary>
/// Represents a city's fitness score for touring based on genre and popularity analysis.
/// </summary>
public class CityFit
{
    /// <summary>
    /// The city name with state code (e.g., "Chicago, IL").
    /// </summary>
    public required string City { get; init; }

    /// <summary>
    /// Fitness score from 0-100 indicating how well this city matches the search criteria.
    /// </summary>
    public int FitScore { get; init; }

    /// <summary>
    /// Explanation of why this city scored well (e.g., shows hosted, venues).
    /// </summary>
    public string? Reason { get; init; }
}