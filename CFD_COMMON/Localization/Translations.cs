using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CFD_COMMON.Localization
{
    public class Translations
    {
        public static Dictionary<TransKeys, string> Values = new Dictionary<TransKeys, string>
        {
            { TransKeys.INVALID_PHONE_NUMBER, "无效的手机号码，请输入正确的11位手机号码" }
        };
    }
}
