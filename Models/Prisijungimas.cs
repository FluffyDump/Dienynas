namespace Dienynas.Models
{
    public class Prisijungimas
    {   
        public DateTime SesijosPradziosLaikas { get; set; }
        public DateTime? SesijosPabaigosLaikas { get; set; }
        public bool NaudotojasAutentifikuotas { get; set; }
        public required int FkNaudotojoId { get; set; }
    }
}