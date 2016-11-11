namespace CFD_COMMON.Models.Entities
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public class UserAlertBase
    {
        [Key]
        [Column(Order = 0)]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int UserId { get; set; }

        [Key]
        [Column(Order = 1)]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int SecurityId { get; set; }

        public decimal? HighPrice { get; set; }
        public decimal? LowPrice { get; set; }
        public bool? HighEnabled { get; set; }
        public bool? LowEnabled { get; set; }
    }

    [Table("UserAlert")]
    public partial class UserAlert : UserAlertBase
    {
    }
}
