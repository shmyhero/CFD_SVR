using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CFD_COMMON.Utils
{
   public class Converts
    {
       public static byte[] HexStringToBytes(string hex)
       {
           return Enumerable.Range(0, hex.Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                .ToArray();
        }

       public static string BytesToHexString(byte[] bytes)
       {
            StringBuilder hex = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
                hex.AppendFormat("{0:x2}", b);
           return hex.ToString();
       }
    }
}
