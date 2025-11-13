namespace Where2Play.Models;

public class ConcertResultViewModel
{
    public string ArtistName { get; set; } = default!;
    public string VenueName { get; set; } = default!;
    public string EventDate { get; set; } = default!;
    public string Genre { get; set; } = default!;
    public string Popularity { get; set; } = default!;
    public Uri SetListURL { get; set; } = default!;
}