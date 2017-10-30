using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.Spatial;

namespace CFD_COMMON.Models.Entities
{
    /// <summary>
    /// 竞猜表
    /// </summary>
    [Table("Quiz")]
    public class Quiz
    {
        public int ID { get; set; }
        /// <summary>
        /// 竞猜标的/产品ID
        /// </summary>
        public int ProdID { get; set; }
        /// <summary>
        /// 竞猜标的/产品名称
        /// </summary>
        public string ProdName { get; set; }
        /// <summary>
        /// 竞猜活动开始时间
        /// </summary>
        public DateTime? OpenAt { get; set; }
        /// <summary>
        /// 竞猜活动结束时间
        /// </summary>
        public DateTime? ClosedAt { get; set; }
        
        /// <summary>
        /// 竞猜活动的创建时间
        /// </summary>
        public DateTime? CreatedAt { get; set; }
        /// <summary>
        /// 竞猜活动的失效时间(如果被删除的话)
        /// </summary>
        public DateTime? ExpiredAt { get; set; }
    }
}
