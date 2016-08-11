namespace CFD_COMMON.Models.Entities
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("CompetitionUserPosition")]
    public partial class CompetitionUserPosition
    {
        [Key]
        [Column(Order = 0)]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int CompetitionId { get; set; }

        [Key]
        [Column(Order = 1)]
        public DateTime Date { get; set; }

        [Key]
        [Column(Order = 2)]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long PositionId { get; set; }

        public int? UserId { get; set; }

        public int? SecurityId { get; set; }

        [StringLength(200)]
        public string SecurityName { get; set; }

        public decimal? Invest { get; set; }

        public decimal? PL { get; set; }
    }
}
