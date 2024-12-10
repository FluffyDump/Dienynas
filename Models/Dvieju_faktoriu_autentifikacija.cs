namespace Dienynas.Models
{
    public class Dvieju_faktoriu_autentifikacija
    {
        public int PatvirtinimoKodas { get; set; }
        public DateTime KodoIssiuntimoLaikas { get; set; }
        public DateTime KodoGaliojimoLaikas { get; set; }
        public bool KodasPanaudotas { get; set; }
        public required int FkNaudotojoId { get; set; }
    }
}