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
        }

        [HttpGet]
        public string get()
        {
            string sVersion = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
            return "JainDB (c) 2018 by Roger Zander; Version: " + sVersion;
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
            if (System.IO.File.Exists("/app/wwwroot/inventory.ps1"))
            {
                string sFile = System.IO.File.ReadAllText("/app/wwwroot/inventory.ps1");
                return sFile.Replace("%LocalURL%", Environment.GetEnvironmentVariable("localURL"));
            }

            string sFile2 = System.IO.File.ReadAllText("wwwroot/inventory.ps1");
            return sFile2.Replace("%LocalURL%", "http://localhost");

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
                    sKey = Inv.LookupID(query.First().Key, query.First().Value);
                //int index = -1;
                if (!int.TryParse(query.FirstOrDefault(t => t.Key.ToLower() == "index").Value, out int index))
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
                string sKey = query.FirstOrDefault(t => t.Key.ToLower() == "id").Value;

                if (string.IsNullOrEmpty(sKey))
                    sKey = Inv.LookupID(query.First().Key, query.First().Value);

                if (!int.TryParse(query.FirstOrDefault(t => t.Key.ToLower() == "index").Value, out int index))
                    index = 1;

                if (!int.TryParse(query.FirstOrDefault(t => t.Key.ToLower() == "mode").Value, out int mode))
                    mode = 0;

                return Inv.GetDiff(sKey, index, mode);
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
                return Json(Inv.search(query.FirstOrDefault(t => string.IsNullOrEmpty(t.Value)).Key, query.FirstOrDefault(t => t.Key.ToLower() == "$select").Value));
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

                return Inv.query(string.Join(",", query.Where(t => string.IsNullOrEmpty(t.Value)).Select(t => t.Key).ToList()), query.FirstOrDefault(t => t.Key.ToLower() == "$select").Value);
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

                return Inv.queryAll(string.Join(",", query.Where(t => string.IsNullOrEmpty(t.Value)).Select(t => t.Key).ToList()), query.FirstOrDefault(t => t.Key.ToLower() == "$select").Value);
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
                    sKey = Inv.LookupID(query.First().Key, query.First().Value);

                return Inv.GetHistory(sKey);
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
                if(!string.IsNullOrEmpty(sTarget))
                    Inv.Export(sTarget);
                else
                    Inv.Export("http://localhost:5000");
            }
            catch { }

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
