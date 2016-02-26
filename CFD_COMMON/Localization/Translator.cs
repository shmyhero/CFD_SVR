using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CFD_COMMON.Localization
{
    public class Translator
    {
        public static string Translate(TransKey transKey)
        {
            if (Translations.Values.ContainsKey(transKey))
                return Translations.Values[transKey];
            else
                return transKey.ToString();
        }
    }
}
