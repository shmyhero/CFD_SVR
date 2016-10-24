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

        public virtual DbSet<AyondoTradeHistory_Live> AyondoTradeHistory_Live { get; set; }
        public virtual DbSet<NewPositionHistory_live> NewPositionHistory_live { get; set; }
        public virtual DbSet<Message_Live> Message_Live { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AyondoTradeHistory_Live>()
                .Property(e => e.Quantity)
                .HasPrecision(18, 5);

            modelBuilder.Entity<AyondoTradeHistory_Live>()
                .Property(e => e.TradePrice)
                .HasPrecision(18, 5);

            modelBuilder.Entity<AyondoTradeHistory_Live>()
                .Property(e => e.PL)
                .HasPrecision(18, 5);

            modelBuilder.Entity<AyondoTradeHistory_Live>()
                .Property(e => e.StopLoss)
                .HasPrecision(18, 5);

            modelBuilder.Entity<AyondoTradeHistory_Live>()
                .Property(e => e.TakeProfit)
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
