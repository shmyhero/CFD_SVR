using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CFD_COMMON.Utils
{
    public class Encryption
    {
        public const string SHARED_SECRET = "25F790A0989547ECBA21BEE96DFA3EBC";

        #region CBC + MD5ofPW + IVPrefixed

        public static string GetCypherText_3DES_CBC_MD5ofPW_IVPrefixed(string plainText, string sharedKey)
        {
            if (string.IsNullOrEmpty(plainText))
                return null;
            var key = MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(sharedKey));

            byte[] encrypted;
            var csp = new TripleDESCryptoServiceProvider
            {
                Key = key,
                Mode = CipherMode.CBC,
                Padding = PaddingMode.PKCS7
            };
            ICryptoTransform transformer = csp.CreateEncryptor();

            using (var msEncrypt = new MemoryStream())
            {
                using (var csEncrypt = new CryptoStream(msEncrypt, transformer, CryptoStreamMode.Write)
                )
                {
                    using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                    {
                        swEncrypt.Write(plainText);
                    }
                    encrypted = msEncrypt.ToArray();
                }
            }
            // we prepend the IV and send it along with the cyphertext
            var readable = Convert.ToBase64String(csp.IV) + Convert.ToBase64String(encrypted);
            return readable;
        }

        public static string GetPlainText_3DES_CBC_MD5ofPW_IVPrefixed(
          string cypherTextWithIV,
          string sharedKey)
        {
            if (string.IsNullOrWhiteSpace(cypherTextWithIV))
                return null;
            var key = MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(sharedKey));

            var csp = new TripleDESCryptoServiceProvider
            {
                Key = key,
                Mode = CipherMode.CBC,
                Padding = PaddingMode.PKCS7
            };
            // we cut payload into IV and cyphertext and proceed
            var ivBase64Size = Convert.ToBase64String(csp.IV).Length;
            var ivBase64 = cypherTextWithIV.Substring(0, ivBase64Size);
            var iv = Convert.FromBase64String(ivBase64);
            var cypherText = cypherTextWithIV.Substring(ivBase64Size);

            string plainText;

            ICryptoTransform transformer = csp.CreateDecryptor(key, iv);

            var encrypted = Convert.FromBase64String(cypherText);
            using (var msDecrypt = new MemoryStream(encrypted))
            {
                using (var csDecrypt = new CryptoStream(msDecrypt, transformer, CryptoStreamMode.Read))
                {
                    using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                    {
                        plainText = srDecrypt.ReadToEnd();
                    }
                }
            }
            return plainText;
        }

        #endregion
    }
}
