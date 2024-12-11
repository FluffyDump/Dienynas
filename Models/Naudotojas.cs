using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Dienynas.Models
{
    public class Naudotojas : IdentityUser
    {
        [Key]
        public int naudotojo_id { get; set; }
        public required string vardas { get; set; }
        public required string pavarde { get; set; }
        public required string slapyvardis { get; set; }
        public required DateTime gimimo_data { get; set; }
        public required string elektroninis_pastas { get; set; }
        public required string slaptazodis { get; set; }
        public required string mokymo_istaigos_pavadinimas { get; set; }
        public required string miestas { get; set; }
        public required string adresas { get; set; }
        
        public string? profilio_nuotrauka { get; set; }
        public string? aprasymas { get; set; }
        public string? viesa_kontaktine_informacija { get; set; }
    }
}
