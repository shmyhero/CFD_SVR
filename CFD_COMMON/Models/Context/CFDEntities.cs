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

        public virtual DbSet<Device> Devices { get; set; }
        public virtual DbSet<VerifyCode> VerifyCodes { get; set; }
        public virtual DbSet<User> Users { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
        }
    }
}
