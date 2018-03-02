using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JainDBTest
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TST_Upload()
        {
            string sTST = System.IO.File.ReadAllText("./test.json");

        }
    }
}
