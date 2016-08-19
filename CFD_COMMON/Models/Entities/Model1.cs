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

        public virtual DbSet<QuoteHistory> QuoteHistories { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<QuoteHistory>()
                .Property(e => e.Bid)
                .HasPrecision(18, 5);

            modelBuilder.Entity<QuoteHistory>()
                .Property(e => e.Ask)
                .HasPrecision(18, 5);
        }
    }
}
