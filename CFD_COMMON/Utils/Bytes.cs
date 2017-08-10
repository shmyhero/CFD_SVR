using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CFD_COMMON.Utils
{
    public class Bytes
    {
        public static bool IsFormerBiggerOrEqual(byte[] former, byte[] latter)
        {
            if(former.Length!=latter.Length)
                throw new ArgumentException("the 2 byte arrays to compare must have equal lenth");

            for (int i = 0; i < former.Length; i++)
            {
                if(former[i]== latter[i])
                    continue;
                if (former[i] > latter[i])
                    return true;
                if (former[i] < latter[i])
                    return false;
            }
            return true;
        }
    }
}
