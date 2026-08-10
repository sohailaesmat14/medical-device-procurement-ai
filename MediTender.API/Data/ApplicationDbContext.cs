using MediTender.API.Models;
using Microsoft.EntityFrameworkCore;

namespace MediTender.API.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Tender> Tenders { get; set; }
        public DbSet<Standard> Standards { get; set; }
        public DbSet<VendorOffer> VendorOffers { get; set; }
        public DbSet<TenderInteraction> TenderInteractions { get; set; }
        public DbSet<OfferEvaluation> OfferEvaluations { get; set; }
        public DbSet<EvaluationDetail> EvaluationDetails { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Standard>()
                .HasIndex(s => s.TenderId)
                .HasDatabaseName("IX_Standards_TenderId");

            modelBuilder.Entity<VendorOffer>()
                .HasIndex(v => new { v.TenderId, v.CompanyName })
                .HasDatabaseName("IX_VendorOffers_TenderId_CompanyName");

            modelBuilder.Entity<OfferEvaluation>()
                .HasIndex(e => new { e.TenderId, e.VendorName })
                .HasDatabaseName("IX_OfferEvaluations_TenderId_VendorName");
        }
    }
}
