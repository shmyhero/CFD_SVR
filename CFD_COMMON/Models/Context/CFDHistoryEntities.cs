using CFD_COMMON.Models.Entities;

namespace CFD_COMMON.Models.Context
{
    using System;
    using System.Data.Entity;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Linq;

    public partial class CFDHistoryEntities : DbContext
    {
        public CFDHistoryEntities()
            : base("name=CFDHistoryEntities")
        {
        }

        public virtual DbSet<ApiHit> ApiHits { get; set; }
        public virtual DbSet<HitIP> HitIPs { get; set; }
        public virtual DbSet<QuoteHistory> QuoteHistories { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
        }
    }
}
