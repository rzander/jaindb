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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace jaindb.Controllers
{
    [Produces("application/json")]
    public class HomeController : Controller
    {
        private readonly IConfiguration _config;
        private readonly ILogger _logger;
        private IMemoryCache _cache;

        public HomeController(IConfiguration config, ILogger<HomeController> logger, IMemoryCache memoryCache)
        {
            _config = config;
            _logger = logger;
            _cache = memoryCache;
            jDB._cache = memoryCache;
        }

        [HttpGet]
        public ActionResult get()
        {
            string sVersion = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
            return Content("JainDB (c) 2018 by Roger Zander; Version: " + sVersion);
        }

        [HttpPost]
        [Route("upload/{Id}")]
        public string Upload(string JSON, string Id)
        {
            var oGet = new StreamReader(Request.Body, true).ReadToEndAsync();

            return jDB.UploadFull(oGet.Result.ToString(), Id);
        }

        [HttpGet]
        [Route("GetPS")]
        public string GetPS()
        {
            string sResult = "";
            
            //Check in MemoryCache
            if (_cache.TryGetValue("GetPS", out sResult))
            {
                return sResult;
            }

            if (System.IO.File.Exists("/app/wwwroot/inventory.ps1"))
            {
                string sFile = System.IO.File.ReadAllText("/app/wwwroot/inventory.ps1");
                sResult = sFile.Replace("%LocalURL%", Environment.GetEnvironmentVariable("localURL")).Replace("%WebPort%", Environment.GetEnvironmentVariable("WebPort"));
                
                //Cache result in Memory
                if (!string.IsNullOrEmpty(sResult))
                {
                    var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(300)); //cache ID for 5min
                    _cache.Set("GetPS", sResult, cacheEntryOptions);
                }

                return sResult;
            }

            string sCurrDir = System.IO.Directory.GetCurrentDirectory();
            if (System.IO.File.Exists(sCurrDir + "/wwwroot/inventory.ps1"))
            {
                string sFile = System.IO.File.ReadAllText(sCurrDir + "/wwwroot/inventory.ps1");
                sResult = sFile.Replace("%LocalURL%", Environment.GetEnvironmentVariable("localURL")).Replace("%WebPort%", Environment.GetEnvironmentVariable("WebPort"));

                //Cache result in Memory
                if (!string.IsNullOrEmpty(sResult))
                {
                    var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(300)); //cache ID for 5min
                    _cache.Set("GetPS", sResult, cacheEntryOptions);
                }

                return sResult;
            }

            try
            {
                string sFile2 = System.IO.File.ReadAllText("wwwroot/inventory.ps1");
                sResult = sFile2.Replace("%LocalURL%", "http://localhost").Replace("%WebPort%", "5000");

                //Cache result in Memory
                if (!string.IsNullOrEmpty(sResult))
                {
                    var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(300)); //cache ID for 5min
                    _cache.Set("GetPS", sResult, cacheEntryOptions);
                }

                return sResult;
            }
            catch { }

            return sResult;

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
                string sKey = query.FirstOrDefault(t => t.Key.ToLower() == "id").Value;

                if (string.IsNullOrEmpty(sKey))
                    sKey = jDB.LookupID(query.First().Key, query.First().Value);
                //int index = -1;
                if (!int.TryParse(query.FirstOrDefault(t => t.Key.ToLower() == "index").Value, out int index))
                    index = -1;

                return jDB.GetFull(sKey, index);
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
                string sKey = query.FirstOrDefault(t => t.Key.ToLower() == "id").Value;

                if (string.IsNullOrEmpty(sKey))
                    sKey = jDB.LookupID(query.First().Key, query.First().Value);

                if (!int.TryParse(query.FirstOrDefault(t => t.Key.ToLower() == "index").Value, out int index))
                    index = 1;

                if (!int.TryParse(query.FirstOrDefault(t => t.Key.ToLower() == "mode").Value, out int mode))
                    mode = 0;

                return jDB.GetDiff(sKey, index, mode);
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
                return Json(jDB.search(query.FirstOrDefault(t => string.IsNullOrEmpty(t.Value)).Key, query.FirstOrDefault(t => t.Key.ToLower() == "$select").Value));
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

                return jDB.query(string.Join(",", query.Where(t => string.IsNullOrEmpty(t.Value)).Select(t => t.Key).ToList()), query.FirstOrDefault(t => t.Key.ToLower() == "$select").Value);
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

                return jDB.queryAll(string.Join(",", query.Where(t => string.IsNullOrEmpty(t.Value)).Select(t => t.Key).ToList()), query.FirstOrDefault(t => t.Key.ToLower() == "$select").Value);
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
                string sKey = query.FirstOrDefault(t => t.Key.ToLower() == "id").Value;

                if (string.IsNullOrEmpty(sKey))
                    sKey = jDB.LookupID(query.First().Key, query.First().Value);

                return jDB.GetHistory(sKey);
            }
            return null;
        }

        [HttpGet]
        [Route("export")]
        public JObject Export()
        {
            string sPath = ((Microsoft.AspNetCore.Http.Internal.DefaultHttpRequest)this.Request).Path;
            string sQuery = ((Microsoft.AspNetCore.Http.Internal.DefaultHttpRequest)this.Request).QueryString.ToString();
            try
            {
                var query = QueryHelpers.ParseQuery(sQuery);
                string sTarget = query.FirstOrDefault(t => t.Key.ToLower() == "url").Value;

                string sRemove = query.FirstOrDefault(t => t.Key.ToLower() == "remove").Value;

                if (!string.IsNullOrEmpty(sTarget))
                    jDB.Export(sTarget, sRemove ?? "");
                else
                    jDB.Export("http://localhost:5000", sRemove ?? "");
            }
            catch { }

            return null;
        }

        //Handle all other Requests
        [HttpGet]
        [Route("{*.}")]
        public ActionResult Thing()
        {
            string sVersion = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
            return Content("JainDB (c) 2018 by Roger Zander; Version: " + sVersion);
        }
    }
}
