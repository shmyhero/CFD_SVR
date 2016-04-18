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
        public virtual DbSet<Bookmark> Bookmarks { get; set; }
        public virtual DbSet<Device> Devices { get; set; }
        public virtual DbSet<User> Users { get; set; }
        public virtual DbSet<VerifyCode> VerifyCodes { get; set; }
        //public virtual DbSet<UserAyondo> UserAyondos { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AyondoSecurity>()
                .Property(e => e.DisplayDecimals)
                .HasPrecision(18, 5);

            modelBuilder.Entity<AyondoSecurity>()
                .Property(e => e.Bid)
                .HasPrecision(18, 5);

            modelBuilder.Entity<AyondoSecurity>()
                .Property(e => e.Ask)
                .HasPrecision(18, 5);

            modelBuilder.Entity<AyondoSecurity>()
                .Property(e => e.MaxSizeLong)
                .HasPrecision(18, 5);

            modelBuilder.Entity<AyondoSecurity>()
                .Property(e => e.MinSizeLong)
                .HasPrecision(18, 5);

            modelBuilder.Entity<AyondoSecurity>()
                .Property(e => e.MaxSizeShort)
                .HasPrecision(18, 5);

            modelBuilder.Entity<AyondoSecurity>()
                .Property(e => e.MinSizeShort)
                .HasPrecision(18, 5);

            modelBuilder.Entity<AyondoSecurity>()
                .Property(e => e.MaxLeverage)
                .HasPrecision(18, 5);

            modelBuilder.Entity<AyondoSecurity>()
                .Property(e => e.LotSize)
                .HasPrecision(18, 5);

            modelBuilder.Entity<AyondoSecurity>()
                .Property(e => e.BaseMargin)
                .HasPrecision(18, 5);

            modelBuilder.Entity<AyondoSecurity>()
                .Property(e => e.PerUnit)
                .HasPrecision(18, 5);

            modelBuilder.Entity<AyondoSecurity>()
                .Property(e => e.PerUnitEquals)
                .HasPrecision(18, 5);
        }
    }
}
