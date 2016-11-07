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

        public virtual DbSet<NewPositionHistory> NewPositionHistories { get; set; }
        public virtual DbSet<NewPositionHistory_live> NewPositionHistory_live { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NewPositionHistory>()
                .Property(e => e.SettlePrice)
                .HasPrecision(18, 5);

            modelBuilder.Entity<NewPositionHistory>()
                .Property(e => e.LongQty)
                .HasPrecision(18, 5);

            modelBuilder.Entity<NewPositionHistory>()
                .Property(e => e.ShortQty)
                .HasPrecision(18, 5);

            modelBuilder.Entity<NewPositionHistory>()
                .Property(e => e.Leverage)
                .HasPrecision(18, 5);

            modelBuilder.Entity<NewPositionHistory>()
                .Property(e => e.InvestUSD)
                .HasPrecision(18, 5);

            modelBuilder.Entity<NewPositionHistory>()
                .Property(e => e.PL)
                .HasPrecision(18, 5);

            modelBuilder.Entity<NewPositionHistory>()
                .Property(e => e.ClosedPrice)
                .HasPrecision(18, 5);

            modelBuilder.Entity<NewPositionHistory_live>()
                .Property(e => e.SettlePrice)
                .HasPrecision(18, 5);

            modelBuilder.Entity<NewPositionHistory_live>()
                .Property(e => e.LongQty)
                .HasPrecision(18, 5);

            modelBuilder.Entity<NewPositionHistory_live>()
                .Property(e => e.ShortQty)
                .HasPrecision(18, 5);

            modelBuilder.Entity<NewPositionHistory_live>()
                .Property(e => e.Leverage)
                .HasPrecision(18, 5);

            modelBuilder.Entity<NewPositionHistory_live>()
                .Property(e => e.InvestUSD)
                .HasPrecision(18, 5);

            modelBuilder.Entity<NewPositionHistory_live>()
                .Property(e => e.PL)
                .HasPrecision(18, 5);

            modelBuilder.Entity<NewPositionHistory_live>()
                .Property(e => e.ClosedPrice)
                .HasPrecision(18, 5);
        }
    }
}
