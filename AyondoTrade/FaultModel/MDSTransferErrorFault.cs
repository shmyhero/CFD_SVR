using System.Runtime.Serialization;

namespace AyondoTrade.FaultModel
{
    [DataContract]
    public class MDSTransferErrorFault
    {
        [DataMember]
        public string Text { get; set; }
    }
}