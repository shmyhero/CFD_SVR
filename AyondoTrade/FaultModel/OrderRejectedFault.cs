using System.Runtime.Serialization;

namespace AyondoTrade.FaultModel
{
    [DataContract]
    public class OrderRejectedFault
    {
        [DataMember]
        public string Text { get; set; }
    }
}