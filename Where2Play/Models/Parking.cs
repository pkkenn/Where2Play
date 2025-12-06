namespace Where2Play.Models
{
    using System;
    using System.Collections.Generic;

    using System.Globalization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    public partial class Parking
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("datetime_local")]
        public DateTimeOffset DatetimeLocal { get; set; }

        [JsonProperty("datetime_utc")]
        public DateTimeOffset DatetimeUtc { get; set; }

        [JsonProperty("enddatetime_utc")]
        public DateTimeOffset EnddatetimeUtc { get; set; }

        [JsonProperty("venue")]
        public Venue Venue { get; set; }

        [JsonProperty("performers")]
        public Performer[] Performers { get; set; }
    }

    public partial class Performer
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("image")]
        public Uri Image { get; set; }

        [JsonProperty("displayImageUrl")]
        public Uri DisplayImageUrl { get; set; }
    }

    public partial class Venue
    {
        [JsonProperty("id")]
        public long ParkingVenueId { get; set; }

        [JsonProperty("name_v2")]
        public string NameV2 { get; set; }

        [JsonProperty("display_location")]
        public string DisplayLocation { get; set; }

        [JsonProperty("location")]
        public Location Location { get; set; }
    }

    public partial class Location
    {
        [JsonProperty("lat")]
        public double Lat { get; set; }

        [JsonProperty("lon")]
        public double Lon { get; set; }
    }
}
