namespace CFD_COMMON.Models.Entities
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("UserInfo")]
    public partial class UserInfo
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int UserId { get; set; }

        [Column(TypeName = "ntext")]
        public string IdFrontImg { get; set; }

        [StringLength(10)]
        public string IdFrontImgExt { get; set; }

        [Column(TypeName = "ntext")]
        public string IdBackImg { get; set; }

        [StringLength(10)]
        public string IdBackImgExt { get; set; }

        [StringLength(10)]
        public string OcrRealName { get; set; }

        [StringLength(20)]
        public string OcrIdCode { get; set; }

        [StringLength(50)]
        public string OcrAddr { get; set; }

        public bool? OcrGender { get; set; }

        [StringLength(10)]
        public string OcrEthnic { get; set; }

        [Column(TypeName = "ntext")]
        public string OcrFaceImg { get; set; }

        [StringLength(50)]
        public string OcrIssueAuth { get; set; }

        [StringLength(50)]
        public string OcrValidPeriod { get; set; }

        [StringLength(50)]
        public string OcrTransId { get; set; }

        public DateTime? OcrCalledAt { get; set; }

        [StringLength(10)]
        public string RealName { get; set; }

        [StringLength(10)]
        public string FirstName { get; set; }

        [StringLength(10)]
        public string LastName { get; set; }

        [StringLength(20)]
        public string IdCode { get; set; }

        public DateTime? FaceCheckAt { get; set; }

        public decimal? FaceCheckSimilarity { get; set; }

        [StringLength(50)]
        public string Email { get; set; }

        public bool? Gender { get; set; }

        [StringLength(20)]
        public string Birthday { get; set; }

        [StringLength(10)]
        public string Ethnic { get; set; }

        [StringLength(50)]
        public string Addr { get; set; }

        [StringLength(50)]
        public string IssueAuth { get; set; }

        [StringLength(50)]
        public string ValidPeriod { get; set; }

        public int? AnnualIncome { get; set; }

        public int? NetWorth { get; set; }

        public int? InvestPct { get; set; }

        [StringLength(20)]
        public string EmpStatus { get; set; }

        public int? InvestFrq { get; set; }

        public bool? HasProExp { get; set; }

        public bool? HasAyondoExp { get; set; }

        public bool? HasOtherQualif { get; set; }

        public bool? ExpOTCDeriv { get; set; }

        public bool? ExpDeriv { get; set; }

        public bool? ExpShareBond { get; set; }

        [StringLength(50)]
        public string SourceOfFunds { get; set; }

        [StringLength(50)]
        public string EmployerName { get; set; }

        [StringLength(50)]
        public string EmployerSector { get; set; }

        public int? MonthlyIncome { get; set; }

        public int? Investments { get; set; }

        public bool? HasTraining { get; set; }

        public bool? HasDemoAcc { get; set; }

        [StringLength(50)]
        public string OtherQualif { get; set; }

        public bool? HasTradedHighLev { get; set; }

        public bool? HasTradedMidLev { get; set; }

        public bool? HasTradedNoLev { get; set; }

        public int? HighLevBalance { get; set; }

        public int? HighLevFrq { get; set; }

        public int? HighLevRisk { get; set; }

        public int? MidLevBalance { get; set; }

        public int? MidLevFrq { get; set; }

        public int? MidLevRisk { get; set; }

        public int? NoLevBalance { get; set; }

        public int? NoLevFrq { get; set; }

        public int? NoLevRisk { get; set; }

        [StringLength(50)]
        public string MifidGuid { get; set; }

        [StringLength(50)]
        public string MifidRulesetId { get; set; }

        public decimal? AppropriatenessScore { get; set; }

        [StringLength(50)]
        public string AppropriatenessResolution { get; set; }

        [Column(TypeName = "ntext")]
        public string ProofOfAddress { get; set; }
    }
}
