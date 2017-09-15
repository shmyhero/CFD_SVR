using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace AyondoTrade.Model
{
    public enum TransferType
    {
        CASH_TRANSFER = 0,
        ADYEN_CC_DEPOSIT = 6,
        CUP_DEPOSIT = 11,
    }

    public enum StatusCode
    {
        ERROR = 0,
        SENT = 1,
        CREATED = 2,
        WAITING_APPROVAL = 3,
        COMPLETE = 4,
        CANCELLED = 5,
    }

    [DataContract]
    public class TransferReport
    {
        [DataMember]
        public string RequestId { get; set; }
        [DataMember]
        public string TransferId { get; set; }
        //[DataMember]
        //public string Account { get; set; }
        [DataMember]
        public StatusCode StatusCode { get; set; }
        [DataMember]
        public string Text { get; set; }
        [DataMember]
        public DateTime Timestamp { get; set; }
    }
}
