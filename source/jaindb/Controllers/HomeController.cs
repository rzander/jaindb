using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using System.IO;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Newtonsoft.Json.Linq;

namespace jaindb.Controllers
{
    [Produces("application/json")]
    public class HomeController : Controller
    {
        private readonly IConfiguration _config;
        public HomeController(IConfiguration config)
        {
            this._config = config;

            if ((int.Parse(config.GetSection("UseRedis").Value ?? config.GetSection("jaindb:UseRedis").Value) == 1) || (Environment.GetEnvironmentVariable("UseRedis")) == "1")
            {
                try
                {
                    if (Inv.cache0 == null)
                    {
                        Inv.cache0 = RedisConnectorHelper.Connection.GetDatabase(0);
                        Inv.cache1 = RedisConnectorHelper.Connection.GetDatabase(1);
                        Inv.cache2 = RedisConnectorHelper.Connection.GetDatabase(2);
                        Inv.cache3 = RedisConnectorHelper.Connection.GetDatabase(3);
                        Inv.cache4 = RedisConnectorHelper.Connection.GetDatabase(4);
                    }
                    if(Inv.srv == null)
                        Inv.srv = RedisConnectorHelper.Connection.GetServer("127.0.0.1", 6379);

                    Inv.UseRedis = true;

                }
                catch(Exception ex)
                {
                    Console.WriteLine("ERROR: " + ex.Message);
                }
            }

            if ((int.Parse(config.GetSection("UseCosmosDB").Value ?? config.GetSection("jaindb:UseCosmosDB").Value) == 1) || (Environment.GetEnvironmentVariable("UseCosmosDB") == "1"))
            {
                try
                {
                    Inv.databaseId = "Assets";
                    Inv.endpointUrl = "https://localhost:8081";
                    Inv.authorizationKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
                    Inv.CosmosDB = new DocumentClient(new Uri(Inv.endpointUrl), Inv.authorizationKey);

                    Inv.CosmosDB.OpenAsync();

                    Inv.UseCosmosDB = true;
                }
                catch { }
            }

            if ((int.Parse(config.GetSection("UseFileSystem").Value ?? config.GetSection("jaindb:UseFileSystem").Value) == 1) || (Environment.GetEnvironmentVariable("UseFileSystem") == "1"))
            {
                Inv.UseFileStore = true;
            }

        }

        /*
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult About()
        {
            ViewData["Message"] = "Your application description page.";

            return View();
        }

        public IActionResult Contact()
        {
            ViewData["Message"] = "Your contact page.";

            return View();
        }

        public IActionResult Error()
        {
            return View();
        } */

        [HttpGet]
        public string get()
        {
            string sVersion = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
            return "RZInv (c) 2017 by Roger Zander; Version: " + sVersion;
        }

        [HttpPost]
        [Route("upload/{Id}")]
        public string Upload(string JSON, string Id)
        {
            var oGet = new StreamReader(Request.Body, true).ReadToEndAsync();

            return Inv.UploadFull(oGet.Result.ToString(), Id);
        }

        [HttpGet]
        [Route("GetPS")]
        public string GetPS()
        {
            string sFile = System.IO.File.ReadAllText("/app/wwwroot/inventory.ps1");
            return sFile.Replace("%LocalURL%", Environment.GetEnvironmentVariable("localURL"));
        }

        [HttpGet]
        [Route("full")]
        public JObject Full()
        {
            string sPath = ((Microsoft.AspNetCore.Http.Internal.DefaultHttpRequest)this.Request).Path;
            string sQuery = ((Microsoft.AspNetCore.Http.Internal.DefaultHttpRequest)this.Request).QueryString.ToString();
            if (sPath != "/favicon.ico")
            {
                var query = QueryHelpers.ParseQuery(sQuery);
                string sKey = query.FirstOrDefault(t => t.Key == "id").Key;

                if (string.IsNullOrEmpty(sKey))
                    sKey = Inv.LookupID(query.First().Key, query.First().Value);
                //int index = -1;
                if (!int.TryParse(query.FirstOrDefault(t => t.Key == "index").Value, out int index))
                    index = -1;

                return Inv.GetFull(sKey, index);
            }
            return null;
        }

        [HttpGet]
        [Route("diff")]
        public JObject Diff()
        {
            this.Url.ToString();
            string sPath = ((Microsoft.AspNetCore.Http.Internal.DefaultHttpRequest)this.Request).Path;
            string sQuery = ((Microsoft.AspNetCore.Http.Internal.DefaultHttpRequest)this.Request).QueryString.ToString();
            if (sPath != "/favicon.ico")
            {
                var query = QueryHelpers.ParseQuery(sQuery);
                string sKey = query.FirstOrDefault(t => t.Key == "id").Key;

                if (string.IsNullOrEmpty(sKey))
                    sKey = Inv.LookupID(query.First().Key, query.First().Value);

                if (!int.TryParse(query.FirstOrDefault(t => t.Key == "index").Value, out int index))
                    index = 1;

                return Inv.GetDiff(sKey, index);
            }
            return null;
        }

        [HttpGet]
        [Route("search")]
        public JsonResult Search()
        {
            this.Url.ToString();
            string sPath = ((Microsoft.AspNetCore.Http.Internal.DefaultHttpRequest)this.Request).Path;
            string sQuery = ((Microsoft.AspNetCore.Http.Internal.DefaultHttpRequest)this.Request).QueryString.ToString();
            if (sPath != "/favicon.ico")
            {
                var query = QueryHelpers.ParseQuery(sQuery);
                return Json(Inv.search(query.FirstOrDefault(t => string.IsNullOrEmpty(t.Value)).Key, query.FirstOrDefault(t => t.Key == "$select").Value));
            }
            return null;
        }

        [HttpGet]
        [Route("query")]
        public JArray Query()
        {
            DateTime dStart = DateTime.Now;

            string sPath = ((Microsoft.AspNetCore.Http.Internal.DefaultHttpRequest)this.Request).Path;
            string sQuery = ((Microsoft.AspNetCore.Http.Internal.DefaultHttpRequest)this.Request).QueryString.ToString();
            if (sPath != "/favicon.ico")
            {
                //string sUri = Microsoft.AspNetCore.Http.Extensions.UriHelper.GetDisplayUrl(Request);
                var query = QueryHelpers.ParseQuery(sQuery);

                return Inv.query(string.Join(",", query.Where(t => string.IsNullOrEmpty(t.Value)).Select(t => t.Key).ToList()), query.FirstOrDefault(t => t.Key == "$select").Value);
            }
            return null;
        }

        [HttpGet]
        [Route("queryAll")]
        public JArray QueryAll()
        {
            this.Url.ToString();
            string sPath = ((Microsoft.AspNetCore.Http.Internal.DefaultHttpRequest)this.Request).Path;
            string sQuery = ((Microsoft.AspNetCore.Http.Internal.DefaultHttpRequest)this.Request).QueryString.ToString();
            if (sPath != "/favicon.ico")
            {
                //string sUri = Microsoft.AspNetCore.Http.Extensions.UriHelper.GetDisplayUrl(Request);
                var query = QueryHelpers.ParseQuery(sQuery);

                return Inv.queryAll(string.Join(",", query.Where(t => string.IsNullOrEmpty(t.Value)).Select(t => t.Key).ToList()), query.FirstOrDefault(t => t.Key == "$select").Value);
            }
            return null;
        }

        [HttpGet]
        [Route("history")]
        public JObject History()
        {
            string sPath = ((Microsoft.AspNetCore.Http.Internal.DefaultHttpRequest)this.Request).Path;
            string sQuery = ((Microsoft.AspNetCore.Http.Internal.DefaultHttpRequest)this.Request).QueryString.ToString();
            if (sPath != "/favicon.ico")
            {
                var query = QueryHelpers.ParseQuery(sQuery);
                string sKey = query.FirstOrDefault(t => t.Key == "id").Key;

                if (string.IsNullOrEmpty(sKey))
                    sKey = Inv.LookupID(query.First().Key, query.First().Value);

                return Inv.GetHistory(sKey);
            }
            return null;
        }

        //Handle all other Requests
        [HttpGet]
        [Route("{*.}")]
        public string Thing()
        {
            //Inv.TST();
            this.Url.ToString();
            string sPath = ((Microsoft.AspNetCore.Http.Internal.DefaultHttpRequest)this.Request).Path;
            string sQuery = ((Microsoft.AspNetCore.Http.Internal.DefaultHttpRequest)this.Request).QueryString.ToString();
            if (sPath != "/favicon.ico")
            {
                string sVersion = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
                return "RZInv (c) 2017 by Roger Zander; Version: " + sVersion + " Path:" + sPath + sQuery;
            }
            return null;
        }
    }
}
