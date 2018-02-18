// ************************************************************************************
//          jaindb (c) Copyright 2018 by Roger Zander
// ************************************************************************************

using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using System.IO;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Documents;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using System.Text;

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

        [HttpPost]
        [Route("uploadxml/{Id}")]
        public string UploadXML(string XML, string Id)
        {
            var oGet = new StreamReader(Request.Body, true).ReadToEndAsync();

            string sJSON = xml2json.ConvertXMLToJSON(oGet.Result.ToString());

            //Check if we have a value in sJSON
            if (!string.IsNullOrEmpty(sJSON))
            {
                return jDB.UploadFull(sJSON, Id);
            }

            return "";
        }

        [HttpPost]
        [Route("xml2json")]
        public JObject XML2JSON(string XML)
        {
            var oGet = new StreamReader(Request.Body, true).ReadToEndAsync();

            string sJSON = xml2json.ConvertXMLToJSON(oGet.Result.ToString());

            //Check if we have a value in sJSON
            if (!string.IsNullOrEmpty(sJSON))
            {
                return JObject.Parse(sJSON);
            }

            return null;
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

            var query = QueryHelpers.ParseQuery(sQuery);
            string sKey = query.FirstOrDefault(t => t.Key.ToLower() == "id").Value;

            if (string.IsNullOrEmpty(sKey))
                sKey = jDB.LookupID(query.First().Key, query.First().Value);
            //int index = -1;
            if (!int.TryParse(query.FirstOrDefault(t => t.Key.ToLower() == "index").Value, out int index))
                index = -1;

            return jDB.GetFull(sKey, index);
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
                    index = 0;

                if (!int.TryParse(query.FirstOrDefault(t => t.Key.ToLower() == "lindex").Value, out int lindex))
                    lindex = 0;

                if (index == 0)
                    index = lindex;

                if (!int.TryParse(query.FirstOrDefault(t => t.Key.ToLower() == "rindex").Value, out int rindex))
                    rindex = -1;

                if (!int.TryParse(query.FirstOrDefault(t => t.Key.ToLower() == "mode").Value, out int mode))
                    mode = 0;

                return jDB.GetDiff(sKey, index, mode, rindex);
            }
            return null;
        }

        [HttpGet]
        [Route("diffvis")]
        public ActionResult DiffVis()
        {
            string sPath = ((Microsoft.AspNetCore.Http.Internal.DefaultHttpRequest)this.Request).Path;
            string sQuery = ((Microsoft.AspNetCore.Http.Internal.DefaultHttpRequest)this.Request).QueryString.ToString();

            var query = QueryHelpers.ParseQuery(sQuery);
            string sKey = query.FirstOrDefault(t => t.Key.ToLower() == "id").Value;

            if (string.IsNullOrEmpty(sKey))
                sKey = jDB.LookupID(query.First().Key, query.First().Value);

            if (!int.TryParse(query.FirstOrDefault(t => t.Key.ToLower() == "index").Value, out int index))
                index = 0;

            if (!int.TryParse(query.FirstOrDefault(t => t.Key.ToLower() == "lindex").Value, out int lindex))
                lindex = 0;

            if (index == 0)
                index = lindex;

            if (!int.TryParse(query.FirstOrDefault(t => t.Key.ToLower() == "rindex").Value, out int rindex))
                rindex = -1;

            if (!int.TryParse(query.FirstOrDefault(t => t.Key.ToLower() == "mode").Value, out int mode))
                mode = 0;

            var right = jDB.GetFull(sKey, rindex);


            jDB.JSort(right);
            if (index == 0)
                index = ((int)right["_index"]) - 1;

            var left = jDB.GetFull(sKey, index);
            jDB.JSort(left);

            string sRes = Properties.Resources.diffvis;
            sRes = sRes.Replace("$$left$$", left.ToString(Newtonsoft.Json.Formatting.None));
            sRes = sRes.Replace("$$right$$", right.ToString(Newtonsoft.Json.Formatting.None));

            return new ContentResult()
            {
                Content = sRes,
                ContentType = "text/HTML"
            };
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
                return Json(jDB.Search(query.FirstOrDefault(t => string.IsNullOrEmpty(t.Value)).Key, query.FirstOrDefault(t => t.Key.ToLower() == "$select").Value));
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

                return jDB.Query(string.Join(";", query.Where(t => string.IsNullOrEmpty(t.Value)).Select(t => t.Key).ToList()), query.FirstOrDefault(t => t.Key.ToLower() == "$select").Value, query.FirstOrDefault(t => t.Key.ToLower() == "$exclude").Value);
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

                return jDB.QueryAll(string.Join(";", query.Where(t => string.IsNullOrEmpty(t.Value)).Select(t => t.Key).ToList()), query.FirstOrDefault(t => t.Key.ToLower() == "$select").Value, query.FirstOrDefault(t => t.Key.ToLower() == "$exclude").Value);
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
        [Route("changes")]
        public JArray Changes()
        {
            string sPath = ((Microsoft.AspNetCore.Http.Internal.DefaultHttpRequest)this.Request).Path;
            string sQuery = ((Microsoft.AspNetCore.Http.Internal.DefaultHttpRequest)this.Request).QueryString.ToString();

            var query = QueryHelpers.ParseQuery(sQuery);
            string sAge = query.FirstOrDefault(t => t.Key.ToLower() == "age").Value;
            string sType = query.FirstOrDefault(t => t.Key.ToLower() == "changetype").Value;
            int iType = -1;

            TimeSpan tAge = new TimeSpan(24, 0, 0);
            TimeSpan.TryParse(sAge.Replace("-", ":"), out tAge);
            if(!string.IsNullOrEmpty(sType))
                int.TryParse(sType, out iType);

            return jDB.GetChanges(tAge, iType);
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

        [HttpGet]
        [Route("validate")]
        public bool Validate()
        {
            try
            {
                string sPath = ((Microsoft.AspNetCore.Http.Internal.DefaultHttpRequest)this.Request).Path;
                string sQuery = ((Microsoft.AspNetCore.Http.Internal.DefaultHttpRequest)this.Request).QueryString.ToString();

                var query = QueryHelpers.ParseQuery(sQuery);
                string sKey = query.FirstOrDefault(t => t.Key.ToLower() == "id").Value;

                if (string.IsNullOrEmpty(sKey))
                    sKey = jDB.LookupID(query.First().Key, query.First().Value);

                /*if (!int.TryParse(query.FirstOrDefault(t => t.Key.ToLower() == "index").Value, out int index))
                    index = -1;*/

                int index = -1;

                //get the latest index from full
                var jFull = jDB.GetFull(sKey, index);

                //remove all # and @ objects
                foreach (var oKey in jFull.Descendants().Where(t => t.Type == JTokenType.Property && ((JProperty)t).Name.StartsWith("@")).ToList())
                {
                    try
                    {
                        oKey.Remove();
                    }
                    catch { }
                }

                //Remove NULL values
                foreach (var oTok in jFull.Descendants().Where(t => t.Parent.Type == (JTokenType.Property) && t.Type == JTokenType.Null).ToList())
                {
                    try
                    {
                        oTok.Parent.Remove();
                    }
                    catch { }
                }
                int rawindex = jFull["_index"].Value<int>();

                var jFromChain = jDB.GetFull(sKey, rawindex);
                //Remove NULL values
                foreach (var oTok in jFromChain.Descendants().Where(t => t.Parent.Type == (JTokenType.Property) && t.Type == JTokenType.Null).ToList())
                {
                    try
                    {
                        oTok.Parent.Remove();
                    }
                    catch { }
                }

                jDB.JSort(jFull);
                jDB.JSort(jFromChain);

                string sCalc = jDB.CalculateHash(jFromChain.ToString(Newtonsoft.Json.Formatting.None));
                string sChain = jDB.CalculateHash(jFull.ToString(Newtonsoft.Json.Formatting.None));
                if (sCalc == sChain)
                {
                    return true;
                }
                else
                    return false;
            }
            catch { }
            return false;
        }

        [HttpGet]
        [Route("html/{*.}")]
        public ActionResult Html()
        {
            string sPath = ((Microsoft.AspNetCore.Http.Internal.DefaultHttpRequest)this.Request).Path;
            string sQuery = ((Microsoft.AspNetCore.Http.Internal.DefaultHttpRequest)this.Request).QueryString.ToString();

            JToken jData = new JObject();

            switch (sPath.Replace("/html/", "").ToLower())
            {
                case "full":
                    jData = Full();
                    break;
                case "query":
                    jData = Query();
                    break;
                case "queryall":
                    jData = QueryAll();
                    break;
                case "diff":
                    jData = Diff();
                    break;
                case "history":
                    jData = History();
                    break;
            }

            StringBuilder htmlTable = new StringBuilder();
            htmlTable.Append("<!DOCTYPE html>");
            htmlTable.Append("<html xmlns=\"http://www.w3.org/1999/xhtml\">");
            htmlTable.Append("<head>");
            htmlTable.Append("<meta charset=\"utf-8\">");
            htmlTable.Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
            htmlTable.Append("<script src=\"https://ajax.googleapis.com/ajax/libs/jquery/3.3.1/jquery.min.js\"></script>");
            htmlTable.Append("<script src=\"https://maxcdn.bootstrapcdn.com/bootstrap/3.3.7/js/bootstrap.min.js\"></script>");
            htmlTable.Append("<link rel=\"stylesheet\" href=\"https://maxcdn.bootstrapcdn.com/bootstrap/3.3.7/css/bootstrap.min.css\">");

            htmlTable.Append("</head>");
            htmlTable.Append("<body>");
            htmlTable.Append("<div class=\"container-fluid\">");
            htmlTable.Append("<table class=\"table table-bordered table-responsive\">");



            foreach (var oChild in jData.Children())
            {
                htmlTable.Append(GetChild(oChild));
            }

            htmlTable.Append("</table>");
            htmlTable.Append("</div>");
            htmlTable.Append("</body>");
            htmlTable.Append("</html>");

            return new ContentResult()
            {
                Content = htmlTable.ToString(),
                ContentType = "text/HTML"
            };
        }

        public string GetChild(JToken jChild)
        {
            StringBuilder htmlTable = new StringBuilder();

            if (jChild.Type == JTokenType.String || jChild.Type == JTokenType.Integer)
            {
                htmlTable.Append("<td>");
                htmlTable.Append(jChild.ToString());
                htmlTable.Append("</td>");
            }

            if (jChild.Type == JTokenType.Array)
            {
                foreach (var oChild in jChild.Children())
                {
                    htmlTable.Append("<tr>");
                    htmlTable.Append(GetChild(oChild));
                    htmlTable.Append("</tr>");
                }
            }

            if (jChild.Type == JTokenType.Object)
            {
                foreach (var oChild in jChild.Children())
                {
                    htmlTable.Append("<tr>");
                    htmlTable.Append(GetChild(oChild));
                    htmlTable.Append("</tr>");
                }
            }

            if (jChild.Type == JTokenType.Property)
            {
                htmlTable.Append("<tr>");
                htmlTable.Append("<th>");
                htmlTable.Append(((JProperty)jChild).Name);
                htmlTable.Append("</th>");
                htmlTable.Append("<td>");

                if (((JProperty)jChild).Value.Type == JTokenType.String)
                {
                    htmlTable.Append(((JProperty)jChild).Value);
                }
                if (((JProperty)jChild).Value.Type == JTokenType.Integer)
                {
                    htmlTable.Append(((JProperty)jChild).Value);
                }
                if (((JProperty)jChild).Value.Type == JTokenType.Date)
                {
                    htmlTable.Append(((JProperty)jChild).Value);
                }
                if (((JProperty)jChild).Value.Type == JTokenType.Boolean)
                {
                    htmlTable.Append(((JProperty)jChild).Value);
                }
                if (((JProperty)jChild).Value.Type == JTokenType.Guid)
                {
                    htmlTable.Append(((JProperty)jChild).Value);
                }
                if (((JProperty)jChild).Value.Type == JTokenType.Bytes)
                {
                    htmlTable.Append(((JProperty)jChild).Value);
                }
                if (((JProperty)jChild).Value.Type == JTokenType.TimeSpan)
                {
                    htmlTable.Append(((JProperty)jChild).Value);
                }
                if (((JProperty)jChild).Value.Type == JTokenType.Uri)
                {
                    htmlTable.Append(((JProperty)jChild).Value);
                }
                if (((JProperty)jChild).Value.Type == JTokenType.Undefined)
                {
                    htmlTable.Append(((JProperty)jChild).Value);
                }

                if (((JProperty)jChild).Value.Type == JTokenType.Array)
                {
                    htmlTable.Append("<table class=\"table table-bordered table-hover table-striped table-sm\">");
                    htmlTable.Append(GetChild(((JProperty)jChild).Value));
                    htmlTable.Append("</table>");
                }

                if (((JProperty)jChild).Value.Type == JTokenType.Object)
                {
                    htmlTable.Append("<table class=\"table table-bordered table-hover table-striped table-sm\">");
                    htmlTable.Append(GetChild(((JProperty)jChild).Value));
                    htmlTable.Append("</table>");
                }

                htmlTable.Append("</td>");
                htmlTable.Append("</tr>");
            }

            return htmlTable.ToString();
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
