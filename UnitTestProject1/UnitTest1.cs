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
            string sHash = jDB.UploadFullAsync(sTST, "test1").Result;

            //var jRes = jDB.GetFull("test1", 1);
            //jDB.JSort(jRes);
            //string s1 = jRes.ToString(Newtonsoft.Json.Formatting.Indented);
            //s1.ToString();

            Console.WriteLine("... received Hash:" + sHash);
            bool bHash = (sHash == "9qZLSzgry3DXTCoFLk4nQRzBf");
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
            jaindb.jDB.loadPlugins();
            Console.WriteLine("Compare cached vs blockchain data...");

            var oFull = jDB.GetFullAsync("test1", -1).Result; //get data from cache
            var oChain = jDB.GetFullAsync("test1", 1).Result; //get data from blockchain id=1

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

            string sChain = jDB.CalculateHashAsync(oChain.ToString(Newtonsoft.Json.Formatting.None)).Result;
            string sFull = jDB.CalculateHashAsync(oFull.ToString(Newtonsoft.Json.Formatting.None)).Result;

            bool bValid = (sChain == sFull);
            if (bValid)
                Console.WriteLine("Blockchain data does match with cached data.");
            Assert.IsTrue(bValid);
        }

        [TestMethod]
        [Priority(2)]
        public void Test_Query()
        {
            jaindb.jDB.loadPlugins();
            Console.WriteLine("Query data...");

            var test = jDB.QueryAsync("OS", "", "", "").Result;
            test.ToString();

            //int i = jDB.QueryAsync("obj1", "", "", "").Result.Count();
            int i = jDB.QueryAsync("", "%23Name;CloudJoin.%23UserEmail;CloudJoin.TenantId", "", "").Result.Count();
            //int i = jDB.QueryAsync("OS", "", "", "").Result.Count();
            Assert.IsTrue(i > 0);
        }

        [TestMethod]
        [Priority(3)]
        public void Test_QueryAll()
        {
            jaindb.jDB.loadPlugins();
            Console.WriteLine("QueryAll data...");

            int i = jDB.QueryAllAsync("obj1", "", "", "").Result.Count();
            Assert.IsTrue(i > 0);
        }
        [TestMethod]
        [Priority(3)]
        public void Test_Changes()
        {
            jaindb.jDB.loadPlugins();
            Console.WriteLine("get changes...");

            int i = jDB.GetChangesAsync(new TimeSpan(1, 0, 0)).Result.Count();
            Assert.IsTrue(i > 0);
        }

        //[Ignore]
        [TestMethod]
        [Priority(99)]
        public void Bulk_Upload()
        {
            if (System.IO.Directory.Exists("wwwroot"))
                System.IO.Directory.Delete("wwwroot", true); //cleanup existing data

            jaindb.jDB.loadPlugins();

            Console.WriteLine("Upload Test Object...");
            string sTST = System.IO.File.ReadAllText("./test.json");


            var oDATA = JObject.Parse(sTST);
            oDATA.Add("TSTKey", 0);

            for (int i = 0; i < 100; i++)
            {
                oDATA["TSTKey"] = i;
                string sHash = jaindb.jDB.UploadFullAsync(oDATA.ToString(Newtonsoft.Json.Formatting.None), "test1").Result;
                Console.WriteLine("... Hash:" + sHash);
                //Thread.Sleep(100); //wait 3s to store all files..
            }

        }
    }
}
