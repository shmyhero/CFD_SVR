using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CFD_COMMON.Models.Entities
{
    /// <summary>
    /// 模拟盘收益奖励交易金
    /// </summary>
    [Table("DemoProfitReward")]
    public class DemoProfitReward
    {

        public int id { get; set; }
        public int UserId { get; set; }
        public decimal Amount { get; set; }
        public decimal TransactionAmount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
