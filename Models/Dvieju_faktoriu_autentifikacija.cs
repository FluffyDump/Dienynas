using System.ComponentModel.DataAnnotations;

namespace Dienynas.Models
{
    public class Dvieju_faktoriu_autentifikacija
    {
        [Key]
        public int dvieju_faktoriu_id { get; set; }
        public int patvirtinimo_kodas { get; set; }
        public DateTime kodo_issiuntimo_laikas { get; set; }
        public DateTime kodo_galiojimo_laikas { get; set; }
        public bool kodas_panaudotas { get; set; }
        public required int fk_Naudotojo_id { get; set; }
    }
}