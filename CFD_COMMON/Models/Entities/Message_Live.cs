namespace CFD_COMMON.Models.Entities
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("Message_Live")]
    public partial class Message_Live : Message
    {
        //public int Id { get; set; }

        //public int? UserId { get; set; }

        //public int? MsgType { get; set; }

        //[StringLength(50)]
        //public string Title { get; set; }

        //[StringLength(500)]
        //public string Body { get; set; }

        //public DateTime? CreatedAt { get; set; }

        //public bool? IsReaded { get; set; }
    }
}
