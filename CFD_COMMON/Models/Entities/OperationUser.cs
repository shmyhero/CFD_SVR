namespace CFD_COMMON.Models.Entities
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("OperationUser")]
    public partial class OperationUser
    {
        public int Id { get; set; }

        [StringLength(100)]
        public string UserName { get; set; }

        [StringLength(100)]
        public string Password { get; set; }

        public int UserType { get; set; }

        public DateTime? Created { get; set; }

        [StringLength(100)]
        public string ExpiredBy { get; set; }

        public DateTime? Expiration { get; set; }
    }
}
