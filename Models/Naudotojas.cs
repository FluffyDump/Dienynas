using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace Dienynas.Models
{
    public class Naudotojas 
    {
        public int naudotojo_id { get; set; }

        public string vardas { get; set; }
        public string pavarde { get; set; }
        public string slapyvardis { get; set; }
        public DateTime gimimo_data { get; set; }
        public string elektroninis_pastas { get; set; }
        public string slaptazodis { get; set; }
        public string mokymo_istaigos_pavadinimas { get; set; }
        public string miestas { get; set; }
        public string adresas { get; set; }

        public string? profilio_nuotrauka { get; set; }
        public string? aprasymas { get; set; }
        public string? viesa_kontaktine_informacija { get; set; }
    }
}