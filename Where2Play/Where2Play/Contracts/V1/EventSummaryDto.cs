using System;

namespace Where2Play.Contracts.V1
{
    /// <summary>
    /// Public view of a music event summary.
    /// </summary>
    public class EventSummaryDto
    {
        /// <summary>Artist display name.</summary>
        public string? ArtistName { get; set; }
        /// <summary>Primary genre.</summary>
        public string? Genre { get; set; }
        /// <summary>Artist country code.</summary>
        public string? Country { get; set; }
        /// <summary>Popularity label or score.</summary>
        public string? Popularity { get; set; }
        /// <summary>Venue name.</summary>
        public string? Venue { get; set; }
        /// <summary>City name.</summary>
        public string? City { get; set; }
        /// <summary>Event date (UTC).</summary>
        public DateTime? Date { get; set; }
        /// <summary>Canonical URL to the event or setlist.</summary>
        public string? Url { get; set; }
    }
}
