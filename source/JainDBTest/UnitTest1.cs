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
            if (System.IO.Directory.Exists("wwwroot"))
                System.IO.Directory.Delete("wwwroot", true); //cleanup existing data

            jaindb.jDB.loadPlugins();

            Console.WriteLine("Upload Test Object...");
            string sTST = System.IO.File.ReadAllText("./test.json");
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
            if (bValid)
                Console.WriteLine("Blockchain data does match with cached data.");
            Assert.IsTrue(bValid);
        }

        [TestMethod]
        [Priority(2)]
        public void Test_Query()
        {
            Console.WriteLine("Query data...");

            int i = jDB.QueryAsync("obj1", "", "", "").Result.Count();
            Assert.IsTrue(i > 0);
        }

        [TestMethod]
        [Priority(3)]
        public void Test_QueryAll()
        {
            Console.WriteLine("QueryAll data...");

            int i = jDB.QueryAll("obj1", "", "", "").Count();
            Assert.IsTrue(i > 0);
        }
        [TestMethod]
        [Priority(3)]
        public void Test_Changes()
        {
            Console.WriteLine("get changes...");

            int i = jDB.GetChanges(new TimeSpan(1,0,0)).Count();
            Assert.IsTrue(i > 0);
        }

        [Ignore]
        [TestMethod]
        [Priority(99)]
        public void Bulk_Upload()
        {
            if (System.IO.Directory.Exists("wwwroot"))
                System.IO.Directory.Delete("wwwroot", true); //cleanup existing data

            Console.WriteLine("Upload Test Object...");
            string sTST = System.IO.File.ReadAllText("./test.json");


            var oDATA = JObject.Parse(sTST);
            oDATA.Add("TSTKey", 0);

            for(int i = 0; i < 100; i++ )
            {
                oDATA["TSTKey"] = i;
                string sHash = jaindb.jDB.UploadFull(oDATA.ToString(Newtonsoft.Json.Formatting.None), "test1");
                Console.WriteLine("... Hash:" + sHash);
                //Thread.Sleep(100); //wait 3s to store all files..
            }

        }
    }
}
