using jaindb;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading;

namespace JainDBTest
{
    [TestClass]
    public class UnitTest1
    {
        /// <summary>
        /// Upload a sample JSON to JainDB
        /// </summary>
        [TestMethod]
        [Priority(0)]
        public void Test_Upload()
        {
            if(System.IO.Directory.Exists("wwwroot"))
                System.IO.Directory.Delete("wwwroot", true); //cleanup existing data

            Console.WriteLine("Upload Test Object...");
            string sTST = System.IO.File.ReadAllText("./test.json");
            jaindb.jDB.UseFileStore = true;
            string sHash = jaindb.jDB.UploadFull(sTST, "test1");
            Thread.Sleep(3000); //wait 3s to store all files..
            Console.WriteLine("... received Hash:" + sHash);
            bool bHash = (sHash == "9qZUmPQTbQgZFdRvD9KHLiAJo");
            Assert.IsTrue(bHash);
            if (bHash)
                Console.WriteLine("Hash is valid.");
        }

        /// <summary>
        /// Compare the results from Blockcahin and Cache
        /// </summary>
        [TestMethod]
        [Priority(1)]
        public void Test_CompareFullvsChain()
        {
            Console.WriteLine("Compare cached vs blockchain data...");
            jDB.UseFileStore = true;
            var oFull = jDB.GetFull("test1", -1); //get data from cache
            var oChain = jDB.GetFull("test1", 1); //get data from blockchain id=1

            //remove all # and @ objects from cached data
            foreach (var oKey in oFull.Descendants().Where(t => t.Type == JTokenType.Property && ((JProperty)t).Name.StartsWith("@")).ToList())
            {
                try
                {
                    oKey.Remove();
                }
                catch { }
            }
            jDB.JSort(oFull);
            jDB.JSort(oChain);

            string sChain = jDB.CalculateHash(oChain.ToString(Newtonsoft.Json.Formatting.None));
            string sFull = jDB.CalculateHash(oFull.ToString(Newtonsoft.Json.Formatting.None));

            bool bValid = (sChain == sFull);
            Assert.IsTrue(bValid);
            if (bValid)
                Console.WriteLine("Blockchain data does match with cached data.");

        }
    }
}
