namespace CFD_COMMON.Models.Entities
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("WithdrawalHistory")]
    public partial class WithdrawalHistory
    {
        public int Id { get; set; }

        public long? TransferId { get; set; }

        public int? UserId { get; set; }

        public DateTime? CreateAt { get; set; }

        public decimal? RequestAmount { get; set; }

        [StringLength(50)]
        public string BankCardNumber { get; set; }

        public decimal? Amount { get; set; }

        public DateTime? ApprovalTime { get; set; }
    }
}
