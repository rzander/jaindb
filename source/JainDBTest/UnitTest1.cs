using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace JainDBTest
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void Test_Upload()
        {
            Console.WriteLine("Upload Test Object...");
            string sTST = System.IO.File.ReadAllText("./test.json");
            string sHash = jaindb.jDB.UploadFull(sTST, "test1");
            Console.WriteLine("... received Hash:" + sHash);
            bool bHash = (sHash == "9qZd8Wcv8yvHZaNHjPsrjUZ36");
            Assert.IsTrue(bHash);
            if (bHash)
                Console.WriteLine("Hash is valid.");
        }
    }
}
