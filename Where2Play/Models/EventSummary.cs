using System;

namespace Where2Play.Models
{
    /// <summary>
    /// Combines artist and concert information from MusicBrainz and Setlist.fm.
    /// </summary>
    public class EventSummary
    {
        // Artist info (MusicBrainz)
        public string? ArtistName { get; set; }
        public string? Genre { get; set; }
        public string? Country { get; set; }
        public string? Popularity { get; set; }

        // Concert info (Setlist.fm)
        public string? Venue { get; set; }
        public string? City { get; set; }
        public DateTime? Date { get; set; }
        public string? Url { get; set; }
    }
}
