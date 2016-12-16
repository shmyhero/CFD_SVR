namespace CFD_COMMON.Models.Entities
{
    using System;
    using System.Data.Entity;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Linq;

    public partial class Model1 : DbContext
    {
        public Model1()
            : base("name=Model1")
        {
        }

        public virtual DbSet<AyondoTransferHistory> AyondoTransferHistories { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AyondoTransferHistory>()
                .Property(e => e.Amount)
                .HasPrecision(18, 8);

            modelBuilder.Entity<AyondoTransferHistory>()
                .Property(e => e.Quantity)
                .HasPrecision(18, 8);

            modelBuilder.Entity<AyondoTransferHistory>()
                .Property(e => e.FinancingRate)
                .HasPrecision(18, 8);
        }
    }
}
