using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CFD_COMMON.Models.Entities
{
    /// <summary>
    /// 模拟盘注册奖励领取记录表
    /// </summary>
    [Table("DemoRegisterReward")]
    public class DemoRegisterReward
    {
        public int id { get; set; }
        public int UserId { get; set; }
        public decimal Amount { get; set; }
        public DateTime? ClaimedAt { get; set; }
    }
}
