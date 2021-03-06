namespace CFD_COMMON.Models.Entities
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("DepositHistory")]
    public partial class DepositHistory
    {
        public int Id { get; set; }

        public long? TransferID { get; set; }

        public int? UserID { get; set; }

        [StringLength(50)]
        public string Type { get; set; }

        public DateTime? CreatedAt { get; set; }

        public decimal? ClaimAmount { get; set; }

        public decimal? Amount { get; set; }

        public DateTime? ApprovalTime { get; set; }
    }
}
