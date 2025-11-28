using System.ComponentModel.DataAnnotations;

namespace Where2Play.Models
{
    public enum BandPopularity
    {
        [Display(Name = "Small (Clubs/Bars)")]
        Small,
        [Display(Name = "Medium (Theaters/Halls)")]
        Medium,
        [Display(Name = "Large (Arenas/Festivals)")]
        Large
    }
}