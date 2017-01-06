using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CFD_COMMON.Models.Entities
{
    [Table("TransferHistory")]
    public class TransferHistory
    {
        [Key]
        public int Id { get; set; }

        public string TransferType { get; set; }

        public decimal Amount { get; set; }

        public int UserID { get; set; }

        public string BankCard { get; set; }

        public DateTime? CreatedAt { get; set; }
    }
}
