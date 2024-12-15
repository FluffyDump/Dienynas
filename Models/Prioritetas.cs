using System.ComponentModel.DataAnnotations;

namespace RazorPages.Models
{
    public enum Priotitetas
    {
        [Display(Name = "Aukštas")] 
        Aukstas,
        [Display(Name = "Vidutinis")]
        Vidutinis,
        [Display(Name = "Žemas")]
        Zemas
    }
}
