using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace CFD_COMMON.Models.Entities
{
    /// <summary>
    /// 好友推荐历史纪录
    /// </summary>
    [Table("ReferHistory")]
    public class ReferHistory
    {
        public int ID { get; set; }
        /// <summary>
        /// 推荐人ID
        /// </summary>
        public int RefereeID { get; set; }

        public string ApplicantNumber { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
