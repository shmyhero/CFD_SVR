using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CFD_COMMON.IdentityVerify
{
    public class OcrFaceCheckFormDTO
    {
        public string accessId { get; set; }
        public string accessKey { get; set; }

        public string transaction_id { get; set; }
        public string userId { get; set; }
        public string userName { get; set; }
        public string firstName { get; set; }
        public string lastName { get; set; }

        public string timeStamp { get; set; }
        public string sign { get; set; }
    }
}
