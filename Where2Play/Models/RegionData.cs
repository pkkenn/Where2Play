using System.Collections.Generic;

namespace Where2Play.Models
{
    public static class RegionData
    {
        public static readonly Dictionary<string, HashSet<string>> Map = new()
        {
            { "NE", new HashSet<string> { "ME", "NH", "VT", "MA", "RI", "CT", "NY", "PA", "NJ" } },
            { "SE", new HashSet<string> { "DE", "MD", "VA", "WV", "NC", "SC", "GA", "FL", "AL", "TN", "MS", "KY" } },
            { "MW", new HashSet<string> { "OH", "IN", "MI", "IL", "WI", "MO", "IA", "MN", "ND", "SD", "NE", "KS" } },
            { "SW", new HashSet<string> { "TX", "OK", "NM", "AZ" } },
            { "W",  new HashSet<string> { "CO", "WY", "MT", "ID", "WA", "OR", "UT", "NV", "CA", "AK", "HI" } }
        };
    }
}