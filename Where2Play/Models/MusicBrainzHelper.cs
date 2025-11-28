using Newtonsoft.Json;

namespace Where2Play.Models
{
    // Helper class to handle the MusicBrainz Search Response wrapper
    public class MusicBrainzSearchResponse
    {
        [JsonProperty("artists")]
        public List<MusicBrainzArtist> Artists { get; set; }
    }
}