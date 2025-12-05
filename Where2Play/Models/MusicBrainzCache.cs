using System.Text.Json;

namespace Where2Play.Models;

public class MusicBrainzCache
{
    public List<CachedArtist> Artists { get; set; } = new();
}

public class CachedArtist
{
    public string Mbid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public List<string> Genres { get; set; } = new();
    public string Popularity { get; set; } = string.Empty;
    public double Rating { get; set; }

    public static MusicBrainzCache? LoadFromFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return null;

            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<MusicBrainzCache>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }
}
