using System;
using System.ComponentModel.DataAnnotations;

namespace RazorPages.Models
{
    public class Zinute
    {
        [Key]
        public int zinutes_id { get; set; }

        public string pavadinimas { get; set; }

        public string turinys { get; set; }

        public DateTime date { get; set; }

        public bool skaityta { get; set; } = false;

        public Priotitetas priotitetas { get; set; }

        public int fk_naudotojo_siuntejo_id { get; set; }

        public int fk_naudotojo_gavejo_id { get; set; }

        public int? fk_klases_id { get; set; }

        public bool archyvuota { get; set; } = false;
    }
}
