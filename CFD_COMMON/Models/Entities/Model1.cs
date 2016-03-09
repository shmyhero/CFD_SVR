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

        public virtual DbSet<AyondoSecurity> AyondoSecurities { get; set; }
        public virtual DbSet<Bookmark> Bookmarks { get; set; }
        public virtual DbSet<Device> Devices { get; set; }
        public virtual DbSet<User> Users { get; set; }
        public virtual DbSet<VerifyCode> VerifyCodes { get; set; }

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
        }
    }
}
