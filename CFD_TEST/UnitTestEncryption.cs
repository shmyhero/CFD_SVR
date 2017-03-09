using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CFD_COMMON.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CFD_TEST
{
    [TestClass]
    public class UnitTestEncryption
    {
        [TestMethod]
        public void Should_encrypt_and_decrypt_using_3DES_having_different_cyphertext_each_time()
        {
            var sharedKey = "25F790A0989547ECBA21BEE96DFA3EBC";


            var textToProtect = "protect me";

            var c1 = Encryption.GetCypherText_3DES_CBC_MD5ofPW_IVPrefixed(textToProtect, sharedKey);
            var c2 = Encryption.GetCypherText_3DES_CBC_MD5ofPW_IVPrefixed(textToProtect, sharedKey);
            Assert.AreNotEqual(c1, c2, "Same secret, but different cyphertext");

            var p1 = Encryption.GetPlainText_3DES_CBC_MD5ofPW_IVPrefixed(c1, sharedKey);
            var p2 = Encryption.GetPlainText_3DES_CBC_MD5ofPW_IVPrefixed(c2, sharedKey);

            Assert.AreEqual("protect me", p1);
            Assert.AreEqual("protect me", p2);
            Assert.AreEqual(p1, p2);
        }
    }
}
