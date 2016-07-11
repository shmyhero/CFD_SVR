using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CFD_COMMON.Utils
{
    public class Phone
    {
        public static bool IsValidPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber)) return false;

            const string regexString = @"^(\d){11}$";
            var regex = new Regex(regexString);
            var match = regex.Match(phoneNumber);
            if (!match.Success)
            {
                //JobsGlobal.LogLine("Invalid phone number: " + phoneNumber);
            }

            return match.Success;
        }

        /// <summary>
        /// for apple app review
        /// </summary>
        /// <param name="phone"></param>
        /// <returns></returns>
        public static bool IsTrustedPhone(string phone)
        {
            if (phone == "11111110001")
                return true;

            return false;
        }
    }
}
