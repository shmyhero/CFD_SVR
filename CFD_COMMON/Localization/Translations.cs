using System.Collections.Generic;

namespace CFD_COMMON.Localization
{
    public class Translations
    {
        public static Dictionary<TransKey, string> Values = new Dictionary<TransKey, string>
        {
            {TransKey.INVALID_PHONE_NUMBER, "无效的手机号码"},
            {TransKey.INVALID_VERIFY_CODE, "验证码错误"},
            {TransKey.NICKNAME_EXISTS,"昵称已存在"},
            {TransKey.ORDER_REJECTED,"下单失败"},
        };
    }
}