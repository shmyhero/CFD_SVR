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

        public virtual DbSet<UserAlert> UserAlerts { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserAlert>()
                .Property(e => e.HighPrice)
                .HasPrecision(18, 5);

            modelBuilder.Entity<UserAlert>()
                .Property(e => e.LowPrice)
                .HasPrecision(18, 5);
        }
    }
}
