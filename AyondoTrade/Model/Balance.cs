using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace AyondoTrade.Model
{
    [DataContract]
    public class BalanceReport
    {
        [DataMember]
        public string BalanceId { get; set; }
        [DataMember]
        public string ActorId { get; set; }
        [DataMember]
        public decimal Value { get; set; }

    }
}
