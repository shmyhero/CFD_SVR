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
        }
    }
}
