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
        public virtual DbSet<Activity> Activities { get; set; }
        public virtual DbSet<Area> Areas { get; set; }
        public virtual DbSet<AyondoSecurity> AyondoSecurities { get; set; }
        public virtual DbSet<AyondoTradeHistory> AyondoTradeHistories { get; set; }
        public virtual DbSet<AyondoTransferHistory> AyondoTransferHistories { get; set; }
        public virtual DbSet<Bank> Banks { get; set; }
        public virtual DbSet<Banner> Banners { get; set; }
        public virtual DbSet<Banner2> Banners2 { get; set; }
        public virtual DbSet<Bookmark> Bookmarks { get; set; }
        public virtual DbSet<Card> Cards { get; set; }
        public virtual DbSet<Channel> Channels { get; set; }
        public virtual DbSet<Competition> Competitions { get; set; }
        public virtual DbSet<CompetitionResult> CompetitionResults { get; set; }
        public virtual DbSet<CompetitionUserPosition> CompetitionUserPositions { get; set; }
        public virtual DbSet<CompetitionUser> CompetitionUsers { get; set; }
        public virtual DbSet<DailySign> DailySigns { get; set; }
        public virtual DbSet<DailyTransaction> DailyTransactions { get; set; }
        public virtual DbSet<DemoRegisterReward> DemoRegisterRewards { get; set; }
        public virtual DbSet<DemoProfitReward> DemoProfitRewards { get; set; }
        public virtual DbSet<DepositReward> DepositRewards { get; set; }
        public virtual DbSet<DepositHistory> DepositHistories { get; set; }
        public virtual DbSet<Device> Devices { get; set; }
        public virtual DbSet<Feedback> Feedbacks { get; set; }
        public virtual DbSet<Headline> Headlines { get; set; }
        public virtual DbSet<IP2City> IP2City { get; set; }
        public virtual DbSet<IP2Country> IP2Country { get; set; }
        public virtual DbSet<KuaiQianOrder> KuaiQianOrders { get; set; }
        public virtual DbSet<LikeHistory> LikeHistories { get; set; }
        public virtual DbSet<LiveRank> LiveRanks { get; set; }
        public virtual DbSet<LiveRegisterReward> LiveRegisterRewards { get; set; }
        public virtual DbSet<Message> Messages { get; set; }
        public virtual DbSet<Misc> Miscs { get; set; }
        public virtual DbSet<NewPositionHistory> NewPositionHistories { get; set; }
        public virtual DbSet<OperationUser> OperationUsers { get; set; }
        public virtual DbSet<OrderRewardUsage> OrderRewardUsages { get; set; }
        public virtual DbSet<Partner> Partners { get; set; }
        public virtual DbSet<PartnerView> PartnerViews { get; set; }
        public virtual DbSet<PartnerUserView> PartnerUserViews { get; set; }
        public virtual DbSet<PingOrder> PingOrders { get; set; }
        public virtual DbSet<ProdSetting_Live> ProdSettings { get; set; }
        public virtual DbSet<PhoneSignupHistory> PhoneSignupHistories { get; set; }
        public virtual DbSet<Quiz> Quizzes { get; set; }
        public virtual DbSet<QuizBet> QuizBets { get; set; }
        public virtual DbSet<QuoteHistory> QuoteHistories { get; set; }
        public virtual DbSet<QuoteSnapshot> QuoteSnapshots { get; set; }
        public virtual DbSet<RewardTransfer> RewardTransfers { get; set; }
        public virtual DbSet<RewardPhoneHistory> RewardPhoneHistorys { get; set; }
        public virtual DbSet<ReferReward> ReferRewards { get; set; }
        public virtual DbSet<ReferHistory> ReferHistorys { get; set; }
        public virtual DbSet<PartnerReferHistory> PartnerReferHistorys { get; set; }
        public virtual DbSet<ScoreHistory> ScoreHistorys { get; set; }
        public virtual DbSet<ScoreConsumptionHistory> ScoreConsumptionHistorys { get; set; }
        public virtual DbSet<ScorePrizeList> ScorePrizeLists { get; set; }
        public virtual DbSet<SystemSetting> SystemSettings { get; set; }
        public virtual DbSet<TimeStampNonce> TimeStampNonces { get; set; }
        //public virtual DbSet<TransferHistory> TransferHistorys { get; set; }
        public virtual DbSet<Trend> Trends { get; set; }
        public virtual DbSet<TrendLikeHistory> TrendLikeHistorys { get; set; }
        public virtual DbSet<TrendRewardHistory> TrendRewardHistorys { get; set; }
        public virtual DbSet<UserCard> UserCards { get; set; }
        public virtual DbSet<UserFollow> UserFollows { get; set; }
        public virtual DbSet<User> Users { get; set; }
        public virtual DbSet<UserAlert> UserAlerts { get; set; }
        public virtual DbSet<UserInfo> UserInfos { get; set; }
        public virtual DbSet<UserImage> UserImages { get; set; }

        public virtual DbSet<VerifyCode> VerifyCodes { get; set; }
        public virtual DbSet<Entities.Version> Versions { get; set; }
        //public virtual DbSet<UserAyondo> UserAyondos { get; set; }
        public virtual DbSet<WithdrawalHistory> WithdrawalHistories { get; set; }

        public virtual DbSet<AyondoTradeHistory_Live> AyondoTradeHistory_Live { get; set; }
        public virtual DbSet<AyondoTransferHistory_Live> AyondoTransferHistory_Live { get; set; }
        public virtual DbSet<Bookmark_Live> Bookmark_Live { get; set; }
        public virtual DbSet<NewPositionHistory_live> NewPositionHistory_live { get; set; }
        public virtual DbSet<Message_Live> Message_Live { get; set; }
        public virtual DbSet<UserAlert_Live> UserAlert_Live { get; set; }
        public virtual DbSet<UserCard_Live> UserCards_Live { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AyondoSecurity>()
                .Property(e => e.DisplayDecimals)
                .HasPrecision(18, 5);


            modelBuilder.Entity<AyondoTradeHistory>()
                .Property(e => e.Quantity)
                .HasPrecision(18, 8);

            modelBuilder.Entity<AyondoTradeHistory>()
                .Property(e => e.TradePrice)
                .HasPrecision(18, 8);

            modelBuilder.Entity<AyondoTradeHistory>()
                .Property(e => e.PL)
                .HasPrecision(18, 8);

            modelBuilder.Entity<AyondoTradeHistory>()
                .Property(e => e.StopLoss)
                .HasPrecision(18, 8);

            modelBuilder.Entity<AyondoTradeHistory>()
                .Property(e => e.TakeProfit)
                .HasPrecision(18, 8);


            modelBuilder.Entity<AyondoTransferHistory>()
                .Property(e => e.Amount)
                .HasPrecision(18, 8);

            modelBuilder.Entity<AyondoTransferHistory>()
                .Property(e => e.Quantity)
                .HasPrecision(18, 8);

            modelBuilder.Entity<AyondoTransferHistory>()
                .Property(e => e.FinancingRate)
                .HasPrecision(18, 8);


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


            modelBuilder.Entity<DepositHistory>()
                .Property(e => e.ClaimAmount)
                .HasPrecision(18, 5);

            modelBuilder.Entity<DepositHistory>()
                .Property(e => e.Amount)
                .HasPrecision(18, 5);


            modelBuilder.Entity<IP2City>()
                .Property(e => e.CountryCode)
                .IsFixedLength()
                .IsUnicode(false);

            modelBuilder.Entity<IP2Country>()
                .Property(e => e.CountryCode)
                .IsFixedLength()
                .IsUnicode(false);


            modelBuilder.Entity<NewPositionHistory>()
                .Property(e => e.SettlePrice)
                .HasPrecision(18, 8);

            modelBuilder.Entity<NewPositionHistory>()
                .Property(e => e.LongQty)
                .HasPrecision(18, 8);

            modelBuilder.Entity<NewPositionHistory>()
                .Property(e => e.ShortQty)
                .HasPrecision(18, 8);

            modelBuilder.Entity<NewPositionHistory>()
                .Property(e => e.Leverage)
                .HasPrecision(18, 5);

            modelBuilder.Entity<NewPositionHistory>()
                .Property(e => e.InvestUSD)
                .HasPrecision(18, 8);

            modelBuilder.Entity<NewPositionHistory>()
                .Property(e => e.ClosedPrice)
                .HasPrecision(18, 8);

            modelBuilder.Entity<NewPositionHistory>()
                .Property(e => e.PL)
                .HasPrecision(18, 8);

            modelBuilder.Entity<NewPositionHistory>()
                .Property(e => e.StopPx)
                .HasPrecision(18, 8);

            modelBuilder.Entity<NewPositionHistory>()
                .Property(e => e.TakePx)
                .HasPrecision(18, 8);


            modelBuilder.Entity<QuoteHistory>()
                .Property(e => e.Bid)
                .HasPrecision(18, 5);

            modelBuilder.Entity<QuoteHistory>()
                .Property(e => e.Ask)
                .HasPrecision(18, 5);

            modelBuilder.Entity<QuoteSnapshot>()
                .Property(e => e.Bid)
                .HasPrecision(18, 5);

            modelBuilder.Entity<QuoteSnapshot>()
                .Property(e => e.Ask)
                .HasPrecision(18, 5);
            //modelBuilder.Entity<TransferHistory>()
            //    .Property(e => e.Amount)
            //    .HasPrecision(18, 5);


            modelBuilder.Entity<UserAlert>()
                .Property(e => e.HighPrice)
                .HasPrecision(18, 8);

            modelBuilder.Entity<UserAlert>()
                .Property(e => e.LowPrice)
                .HasPrecision(18, 8);

            modelBuilder.Entity<UserCard>()
                .Property(e => e.Qty)
                .HasPrecision(18, 5);

            modelBuilder.Entity<UserCard>()
                .Property(e => e.Invest)
                .HasPrecision(18, 5);

            modelBuilder.Entity<UserCard>()
                .Property(e => e.TradePrice)
                .HasPrecision(18, 5);

            modelBuilder.Entity<UserCard>()
                .Property(e => e.SettlePrice)
                .HasPrecision(18, 5);

            modelBuilder.Entity<UserCard_Live>()
               .Property(e => e.Qty)
               .HasPrecision(18, 5);

            modelBuilder.Entity<UserCard_Live>()
                .Property(e => e.Invest)
                .HasPrecision(18, 5);

            modelBuilder.Entity<UserCard_Live>()
                .Property(e => e.TradePrice)
                .HasPrecision(18, 5);

            modelBuilder.Entity<UserCard_Live>()
                .Property(e => e.SettlePrice)
                .HasPrecision(18, 5);


            modelBuilder.Entity<UserInfo>()
                .Property(e => e.FaceCheckSimilarity)
                .HasPrecision(18, 5);

            modelBuilder.Entity<UserInfo>()
                .Property(e => e.AppropriatenessScore)
                .HasPrecision(18, 5);


            modelBuilder.Entity<WithdrawalHistory>()
                .Property(e => e.RequestAmount)
                .HasPrecision(18, 5);

            modelBuilder.Entity<WithdrawalHistory>()
                .Property(e => e.Amount)
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


            modelBuilder.Entity<AyondoTradeHistory_Live>()
                .Property(e => e.Quantity)
                .HasPrecision(18, 8);

            modelBuilder.Entity<AyondoTradeHistory_Live>()
                .Property(e => e.TradePrice)
                .HasPrecision(18, 8);

            modelBuilder.Entity<AyondoTradeHistory_Live>()
                .Property(e => e.PL)
                .HasPrecision(18, 8);

            modelBuilder.Entity<AyondoTradeHistory_Live>()
                .Property(e => e.StopLoss)
                .HasPrecision(18, 8);

            modelBuilder.Entity<AyondoTradeHistory_Live>()
                .Property(e => e.TakeProfit)
                .HasPrecision(18, 8);


            modelBuilder.Entity<NewPositionHistory_live>()
                .Property(e => e.SettlePrice)
                .HasPrecision(18, 8);

            modelBuilder.Entity<NewPositionHistory_live>()
                .Property(e => e.LongQty)
                .HasPrecision(18, 8);

            modelBuilder.Entity<NewPositionHistory_live>()
                .Property(e => e.ShortQty)
                .HasPrecision(18, 8);

            modelBuilder.Entity<NewPositionHistory_live>()
                .Property(e => e.Leverage)
                .HasPrecision(18, 5);

            modelBuilder.Entity<NewPositionHistory_live>()
                .Property(e => e.InvestUSD)
                .HasPrecision(18, 8);

            modelBuilder.Entity<NewPositionHistory_live>()
                .Property(e => e.PL)
                .HasPrecision(18, 8);

            modelBuilder.Entity<NewPositionHistory_live>()
                .Property(e => e.ClosedPrice)
                .HasPrecision(18, 8);

            modelBuilder.Entity<NewPositionHistory_live>()
                .Property(e => e.StopPx)
                .HasPrecision(18, 8);

            modelBuilder.Entity<NewPositionHistory_live>()
                .Property(e => e.TakePx)
                .HasPrecision(18, 8);

            modelBuilder.Entity<PingOrder>()
                .Property(e => e.FxRate)
                .HasPrecision(18, 6);

            modelBuilder.Entity<UserAlert_Live>()
                .Property(e => e.HighPrice)
                .HasPrecision(18, 8);

            modelBuilder.Entity<UserAlert_Live>()
                .Property(e => e.LowPrice)
                .HasPrecision(18, 8);
        }
    }
}
