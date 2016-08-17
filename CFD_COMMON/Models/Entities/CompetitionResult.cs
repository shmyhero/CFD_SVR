namespace CFD_COMMON.Models.Entities
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("CompetitionResult")]
    public partial class CompetitionResult
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
        public int UserId { get; set; }

        [StringLength(50)]
        public string Nickname { get; set; }

        [StringLength(20)]
        public string Phone { get; set; }

        public int? Rank { get; set; }

        public int? PositionCount { get; set; }

        public decimal? Invest { get; set; }

        public decimal? PL { get; set; }

        //public virtual User User { get; set; }
    }
}
