using System.ComponentModel.DataAnnotations.Schema;

namespace CFD_COMMON.Models.Entities
{
    [Table("Device")]
    public partial class Device
    {
        public int Id { get; set; }
    }
}
