using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace Dienynas.Models
{
    public class ApplicationDbContext : IdentityDbContext<Naudotojas>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        { }

        public DbSet<Prisijungimas> Prisijungimas { get; set; }
        public DbSet<Dvieju_faktoriu_autentifikacija> Dvieju_faktoriu_autentifikacija { get; set; }
    }
}
