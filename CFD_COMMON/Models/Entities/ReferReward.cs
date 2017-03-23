using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CFD_COMMON.Models.Entities
{
    [Table("ReferReward")]
    public class ReferReward
    {
        public int ID { get; set; }
        /// <summary>
        /// 推荐人ID
        /// </summary>
        public int RefereeID { get; set; }
        /// <summary>
        /// 被推荐人电话
        /// </summary>
        public string ApplicantNumber { get; set; }
        public decimal Amount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
