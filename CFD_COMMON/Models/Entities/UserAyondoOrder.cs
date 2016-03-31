namespace CFD_COMMON.Models.Entities
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("UserAyondoOrder")]
    public partial class UserAyondoOrder
    {
        public long Id { get; set; }

        public int? UserId { get; set; }

        public decimal? BalanceCash { get; set; }
    }
}
