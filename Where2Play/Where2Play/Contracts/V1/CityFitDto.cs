namespace Where2Play.Contracts.V1
{
    /// <summary>
    /// Recommended city to play with a relative fit score.
    /// </summary>
    public class CityFitDto
    {
        /// <summary>Name of the city.</summary>
        public string City { get; set; } = string.Empty;
        /// <summary>Relative fitness score (higher = better).</summary>
        public int FitScore { get; set; }
        /// <summary>Reason for recommendation.</summary>
        public string Reason { get; set; } = string.Empty;
    }
}
