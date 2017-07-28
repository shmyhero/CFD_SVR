using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CFD_COMMON.Models.Entities
{
    [Table("PartnerView")]
    public class PartnerView
    {
        [Key]
        public string PartnerCode { get; set; }

        public string Name { get; set; }

        public string ParentCode { get; set; }

        public string RootCode { get; set; }

        public int UserCount { get; set; }

        public string PartnerGUID { get; set; }

        public DateTime? PartnerCreatedAt { get; set; }

        public int? UserId { get; set; }

        public string Nickname { get; set; }

        public string Phone { get; set; }

        public long? AyondoAccountId { get; set; }

        public DateTime? UserCreatedAt { get; set; }

        public DateTime? LastHitAt { get; set; }

        public DateTime? AyLiveApplyAt { get; set; }

        public string AyLiveUsername { get; set; }

        public int? TradeCount { get; set; }

        public string IsDeposit { get; set; }

        public decimal? Amount { get; set; }
    }
}
