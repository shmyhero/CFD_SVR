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

        public virtual DbSet<UserInfo> UserInfoes { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserInfo>()
                .Property(e => e.FaceCheckSimilarity)
                .HasPrecision(18, 5);

            modelBuilder.Entity<UserInfo>()
                .Property(e => e.AppropriatenessScore)
                .HasPrecision(18, 5);
        }
    }
}
