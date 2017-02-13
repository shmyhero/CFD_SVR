using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CFD_COMMON.Models.Entities
{
    [Table("DepositHistory")]
    public class DepositHistory
    {
        [Key]
        public int Id { get; set; }
        public string TransferID { get; set; }
        public int UserID { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
