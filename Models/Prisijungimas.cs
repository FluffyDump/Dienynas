using System.ComponentModel.DataAnnotations;

namespace Dienynas.Models
{
    public class Prisijungimas
    {   
        [Key]
        public int autentifikacijos_id { get; set; }
        public DateTime sesijos_pradzios_laikas { get; set; }
        public DateTime? sesijos_pabaigos_laikas { get; set; }
        public bool naudotojas_autentifikuotas { get; set; }
        public required int fk_Naudotojo_id { get; set; }
    }
}