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
        public static bool IsTradeSizeZero(decimal? d)
        {
            if (d == null)
                return false;

            return Math.Abs(d.Value) < (decimal) 0.000001;
        }

        public static decimal RoundIfExceed(decimal d, int decimalCount)
        {
            int c = BitConverter.GetBytes(decimal.GetBits(d)[3])[2];

            if (c > decimalCount)
                return Math.Round(d, decimalCount, MidpointRounding.AwayFromZero);
            else
                return d;
        }
    }
}
