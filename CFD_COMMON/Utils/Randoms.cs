using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CFD_COMMON.Utils
{
    public class Randoms
    {
        private static readonly char[] Alphanumeric = new char[]
            {
                'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
                'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
                '0','1','2','3','4','5','6','7','8','9'
            };
        private static readonly char[] Alphabetic = new char[]
            {
                'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
                'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z'
            };
        private static readonly char[] AlphabeticLower = new char[]
            {
                'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z'
            };

        public static string GetRandomAlphabeticLowerString(int length)
        {
            Random r=new Random();

            IList<char> result=new List<char>();
            for (int i = 0; i < length; i++)
            {
                var randomIndex = r.Next(0, AlphabeticLower.Length);
                result.Add(AlphabeticLower[randomIndex]);
            }

            return new string(result.ToArray());
        }

        public static string GetRandomAlphabeticString(int length)
        {
            Random r = new Random();

            IList<char> result = new List<char>();
            for (int i = 0; i < length; i++)
            {
                var randomIndex = r.Next(0, Alphabetic.Length);
                result.Add(Alphabetic[randomIndex]);
            }

            return new string(result.ToArray());
        }

        public static string GetRandomAlphanumericString(int length)
        {
            
            Random r = new Random();

            IList<char> result = new List<char>();
            for (int i = 0; i < length; i++)
            {
                var randomIndex = r.Next(0, Alphanumeric.Length);
                result.Add(Alphanumeric[randomIndex]);
            }

            return new string(result.ToArray());
        }
    }
}
