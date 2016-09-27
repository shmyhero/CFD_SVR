using CFD_COMMON.Models.Entities;

namespace CFD_COMMON.Models.Context
{
    using System;
    using System.Data.Entity;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Linq;

    public partial class CFDEntities : DbContext
    {
        public CFDEntities()
            : base("name=CFDEntities")
        {
        }

        public virtual DbSet<AyondoSecurity> AyondoSecurities { get; set; }
        public virtual DbSet<AyondoTradeHistory> AyondoTradeHistories { get; set; }
        public virtual DbSet<Banner> Banners { get; set; }
        public virtual DbSet<Banner2> Banners2 { get; set; }
        public virtual DbSet<Bookmark> Bookmarks { get; set; }
        public virtual DbSet<Competition> Competitions { get; set; }
        public virtual DbSet<CompetitionResult> CompetitionResults { get; set; }
        public virtual DbSet<CompetitionUserPosition> CompetitionUserPositions { get; set; }
        public virtual DbSet<CompetitionUser> CompetitionUsers { get; set; }
        public virtual DbSet<DailySign> DailySigns { get; set; }
        public virtual DbSet<DailyTransaction> DailyTransactions { get; set; }
        public virtual DbSet<DemoRegisterReward> DemoRegisterRewards { get; set; }
        public virtual DbSet<Device> Devices { get; set; }
        public virtual DbSet<Feedback> Feedbacks { get; set; }
        public virtual DbSet<Headline> Headlines { get; set; }
        public virtual DbSet<Message> Messages { get; set; }
        public virtual DbSet<NewPositionHistory> NewPositionHistories { get; set; }
        public virtual DbSet<OperationUser> OperationUsers { get; set; }
        public virtual DbSet<PhoneSignupHistory> PhoneSignupHistories { get; set; }
        public virtual DbSet<QuoteHistory> QuoteHistories { get; set; }
        public virtual DbSet<User> Users { get; set; }
        public virtual DbSet<UserAlert> UserAlerts { get; set; }
        public virtual DbSet<VerifyCode> VerifyCodes { get; set; }
        //public virtual DbSet<UserAyondo> UserAyondos { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AyondoSecurity>()
                .Property(e => e.DisplayDecimals)
                .HasPrecision(18, 5);


            modelBuilder.Entity<AyondoTradeHistory>()
                .Property(e => e.Quantity)
                .HasPrecision(18, 5);

            modelBuilder.Entity<AyondoTradeHistory>()
                .Property(e => e.TradePrice)
                .HasPrecision(18, 5);

            modelBuilder.Entity<AyondoTradeHistory>()
                .Property(e => e.PL)
                .HasPrecision(18, 5);

            modelBuilder.Entity<AyondoTradeHistory>()
                .Property(e => e.StopLoss)
                .HasPrecision(18, 5);

            modelBuilder.Entity<AyondoTradeHistory>()
                .Property(e => e.TakeProfit)
                .HasPrecision(18, 5);


            modelBuilder.Entity<CompetitionResult>()
               .Property(e => e.Invest)
               .HasPrecision(18, 5);

            modelBuilder.Entity<CompetitionResult>()
                .Property(e => e.PL)
                .HasPrecision(18, 5);


            modelBuilder.Entity<CompetitionUserPosition>()
                .Property(e => e.Invest)
                .HasPrecision(18, 5);

            modelBuilder.Entity<CompetitionUserPosition>()
                .Property(e => e.PL)
                .HasPrecision(18, 5);


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


            modelBuilder.Entity<QuoteHistory>()
                .Property(e => e.Bid)
                .HasPrecision(18, 5);

            modelBuilder.Entity<QuoteHistory>()
                .Property(e => e.Ask)
                .HasPrecision(18, 5);


            modelBuilder.Entity<UserAlert>()
                .Property(e => e.HighPrice)
                .HasPrecision(18, 5);

            modelBuilder.Entity<UserAlert>()
                .Property(e => e.LowPrice)
                .HasPrecision(18, 5);

            //modelBuilder.Entity<AyondoSecurity>()
            //    .Property(e => e.Bid)
            //    .HasPrecision(18, 5);

            //modelBuilder.Entity<AyondoSecurity>()
            //    .Property(e => e.Ask)
            //    .HasPrecision(18, 5);

            //modelBuilder.Entity<AyondoSecurity>()
            //    .Property(e => e.MaxSizeLong)
            //    .HasPrecision(18, 5);

            //modelBuilder.Entity<AyondoSecurity>()
            //    .Property(e => e.MinSizeLong)
            //    .HasPrecision(18, 5);

            //modelBuilder.Entity<AyondoSecurity>()
            //    .Property(e => e.MaxSizeShort)
            //    .HasPrecision(18, 5);

            //modelBuilder.Entity<AyondoSecurity>()
            //    .Property(e => e.MinSizeShort)
            //    .HasPrecision(18, 5);

            //modelBuilder.Entity<AyondoSecurity>()
            //    .Property(e => e.MaxLeverage)
            //    .HasPrecision(18, 5);

            //modelBuilder.Entity<AyondoSecurity>()
            //    .Property(e => e.LotSize)
            //    .HasPrecision(18, 5);

            //modelBuilder.Entity<AyondoSecurity>()
            //    .Property(e => e.BaseMargin)
            //    .HasPrecision(18, 5);

            //modelBuilder.Entity<AyondoSecurity>()
            //    .Property(e => e.PerUnit)
            //    .HasPrecision(18, 5);

            //modelBuilder.Entity<AyondoSecurity>()
            //    .Property(e => e.PerUnitEquals)
            //    .HasPrecision(18, 5);
        }
    }
}
