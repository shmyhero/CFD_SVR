using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CFD_COMMON.Utils
{
    public class Decimals
    {
        /// <summary>
        /// return false if the value is null
        /// </summary>
        /// <param name="d"></param>
        /// <returns></returns>
        public static bool IsEqualToZero(decimal? d)
        {
            if (d == null)
                return false;

            return Math.Abs(d.Value) < (decimal) 0.0000000001;
        }
    }
}
