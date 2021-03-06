﻿namespace CFD_COMMON.Models.Entities
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("Banner2")]
    public partial class Banner2
    {
        //[DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        [StringLength(200)]
        public string Url { get; set; }

        [StringLength(200)]
        public string ImgUrl { get; set; }

        [StringLength(200)]
        public string ImgUrlBig { get; set; }

        [StringLength(200)]
        public string Header { get; set; }

        [StringLength(int.MaxValue)]
        public string Body { get; set; }

        [StringLength(100)]
        public string Digest { get; set; }

        public int? IsTop { get; set; }

        public DateTime? TopAt { get; set; }

        public int? BannerType { get; set; }

        [StringLength(200)]
        public string CreatedBy { get; set; }

        public DateTime? CreatedAt { get; set; }

        [StringLength(200)]
        public string ExpiredBy { get; set; }

        public DateTime? Expiration { get; set; }

        public string Color { get; set; }
        /// <summary>
        /// 模拟盘、实盘或全部
        /// </summary>
        public string DisplayFor { get; set; }

        public string Version { get; set; }
        /// <summary>
        /// 中文或英文
        /// </summary>
        public string Language { get; set; }
    }
}
