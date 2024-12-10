using Microsoft.AspNetCore.Identity;

namespace Dienynas.Models
{
    public class Naudotojas : IdentityUser
    {
        public required string Vardas { get; set; }
        public required string Pavarde { get; set; }
        public required string Slapyvardis { get; set; }
        public required DateTime GimimoData { get; set; }
        public required string ElektroninisPastas { get; set; }
        public required string Slaptazodis { get; set; }
        public required string MokymoIstaigosPavadinimas { get; set; }
        public required string Miestas { get; set; }
        public required string Adresas { get; set; }
        
        public string? ProfilioNuotrauka { get; set; }
        public string? Aprasymas { get; set; }
        public string? ViesaKontaktineInformacija { get; set; }
    }
}
