using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CFD_COMMON.Utils
{
    public class Maths
    {
        /// <summary>
        /// Do Math.Ceiling at a certain decimal position
        /// </summary>
        /// <param name="d"></param>
        /// <param name="decimalCount"></param>
        /// <returns></returns>
        public static decimal Ceiling(decimal d, int decimalCount)
        {
            decimal magnifier = 1;
            for (int i = 0; i < decimalCount; i++)
            {
                magnifier *= 10;
            }

            return Math.Ceiling(d*magnifier)/magnifier;
        }

        /// <summary>
        /// Do Math.Floor at a certain decimal position
        /// </summary>
        /// <param name="d"></param>
        /// <param name="decimalCount"></param>
        /// <returns></returns>
        public static decimal Floor(decimal d, int decimalCount)
        {
            decimal magnifier = 1;
            for (int i = 0; i < decimalCount; i++)
            {
                magnifier *= 10;
            }

            return Math.Floor(d * magnifier) / magnifier;
        }
    }
}
