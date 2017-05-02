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

        public virtual DbSet<IP2Country> IP2Country { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IP2Country>()
                .Property(e => e.CountryCode)
                .IsFixedLength()
                .IsUnicode(false);
        }
    }
}
