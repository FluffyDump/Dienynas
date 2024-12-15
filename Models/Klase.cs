using System.ComponentModel.DataAnnotations;

namespace RazorPages.Models
{
    public class Klase
    {
        [Key]
        public int klases_id { get; set; }
        public string pavadinimas { get; set; }
    }
}
