using JainDBProvider;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Plugin_Forwarder
{
    public class Plugin_Forwarder : IStore
    {
        private static readonly object locker = new object();
        private bool bReadOnly = false;
        private bool ContinueAfterWrite = true;
        private string FilePath = "";
        private string jaindburl = "";
        private JObject JConfig = new JObject();
        private string reportpassword = "";
        private string reportuser = "";
        public string Name
        {
            get
            {
                return Assembly.GetExecutingAssembly().ManifestModule.Name;
            }
        }

        public Dictionary<string, string> Settings { get; set; }
        public List<string> GetAllIDs()
        {
            List<string> lResult = new List<string>();
            return lResult;
        }

        public IAsyncEnumerable<JObject> GetRawAssetsAsync(string paths)
        {
            return null;
        }

        public void Init()
        {
            if (Settings == null)
                Settings = new Dictionary<string, string>();


            try
            {
                if (!File.Exists(Assembly.GetExecutingAssembly().Location.Replace(".dll", ".json")))
                {
                    File.WriteAllText(Assembly.GetExecutingAssembly().Location.Replace(".dll", ".json"), Properties.Resources.Plugin_Forwarder);
                }

                if (File.Exists(Assembly.GetExecutingAssembly().Location.Replace(".dll", ".json")))
                {
                    JConfig = JObject.Parse(File.ReadAllText(Assembly.GetExecutingAssembly().Location.Replace(".dll", ".json")));
                    bReadOnly = JConfig["ReadOnly"].Value<bool>();
                    ContinueAfterWrite = JConfig["ContinueAfterWrite"].Value<bool>();
                    jaindburl = JConfig["jaindburl"].Value<string>();
                    reportuser = JConfig["reportuser"].Value<string>();
                    reportpassword = JConfig["reportpassword"].Value<string>();
                }
                else
                {
                    JConfig = new JObject();
                }
            }
            catch { }

            if (string.IsNullOrEmpty(jaindburl))
                bReadOnly = true;
        }

        public string LookupID(string name, string value)
        {
            string sResult = "";
            return sResult;
        }

        public string ReadHash(string Hash, string Collection)
        {
            string sResult = "";
            try
            {
                Collection = Collection.ToLower();
                switch (Collection)
                {
                    case "_full":
                        using (HttpClient oClient = new HttpClient())
                        {
                            oClient.DefaultRequestHeaders.Clear();
                            if (!string.IsNullOrEmpty(reportuser))
                            {
                                var byteArray = Encoding.ASCII.GetBytes(reportuser + ":" + reportpassword);
                                oClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
                            }
                            oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                            var response = oClient.GetStringAsync(jaindburl + "/full?id=" + Hash);
                            response.Wait(180000);
                            if (response.IsCompleted)
                            {
                                return response.Result;
                            }
                        }
                        break;
                    default:
                        return "";
                }

            }
            catch { }

            return sResult;
        }

        public int totalDeviceCount(string sPath = "")
        {
            int iCount = -1;
            return iCount;
        }

        public bool WriteHash(string Hash, string Data, string Collection)
        {
            Collection = Collection.ToLower();

            if(bReadOnly)
                if (ContinueAfterWrite)
                    return false;
                else
                    return true;

            string sCol = Path.Combine(FilePath, Collection);
            if (!Directory.Exists(sCol))
                Directory.CreateDirectory(sCol);

            switch (Collection)
            {
                case "_full":
                    using (HttpClient oClient = new HttpClient())
                    {
                        oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        HttpContent oCont = new StringContent(Data);
                        var response = oClient.PostAsync((jaindburl + "/upload/" + Hash), oCont);
                        response.Wait(180000);
                        if (response.IsCompleted)
                        {
                            if(!string.IsNullOrEmpty(response.Result.Content.ReadAsStringAsync().Result));
                            {
                                if (ContinueAfterWrite)
                                    return false;
                                else
                                    return true;
                            }
                        }
                    }
                    return false;
                default:
                    return false;
            }
        }
        public bool WriteLookupID(string name, string value, string id)
        {
            return false;
        }
    }


}

