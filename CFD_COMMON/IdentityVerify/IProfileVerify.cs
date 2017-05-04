using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CFD_COMMON.IdentityVerify
{
    public interface IProfileVerify
    {
        JObject Verify(OcrFaceCheckFormDTO form);
    }
}
