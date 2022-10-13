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
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
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

        public async Task<List<string>> GetAllIDsAsync(CancellationToken ct = default(CancellationToken))
        {
            await Task.CompletedTask;
            List<string> lResult = new List<string>();
            return lResult;
        }

        public async IAsyncEnumerable<JObject> GetRawAssetsAsync(string paths, [EnumeratorCancellation] CancellationToken ct = default(CancellationToken))
        {
            await Task.CompletedTask;
            yield break;
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


        public async Task<string> LookupIDAsync(string name, string value, CancellationToken ct = default(CancellationToken))
        {
            await Task.CompletedTask;
            return "";
        }

        public string ReadHash(string Hash, string Collection)
        {
            string sResult = "";
            try
            {
                string PartitionKey = "";

                if (Hash.Contains(';'))
                {
                    PartitionKey = Hash.Split(';')[1];
                    Hash = Hash.Split(';')[0];
                }

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

        public async Task<string> ReadHashAsync(string Hash, string Collection, CancellationToken ct = default(CancellationToken))
        {
            string sResult = "";
            try
            {
                string PartitionKey = "";

                if (Hash.Contains(';'))
                {
                    PartitionKey = Hash.Split(';')[1];
                    Hash = Hash.Split(';')[0];
                }

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
                            var response = await oClient.GetStringAsync(jaindburl + "/full?id=" + Hash, ct);

                            return response;
                        }
                    default:
                        return "";
                }

            }
            catch { }

            return sResult;
        }

        public async Task<int> totalDeviceCountAsync(string sPath = "", CancellationToken ct = default(CancellationToken))
        {
            await Task.CompletedTask;
            int iCount = -1;
            return iCount;
        }


        public async Task<bool> WriteHashAsync(string Hash, string Data, string Collection, CancellationToken ct = default(CancellationToken))
        {
            string PartitionKey = "";

            if (Hash.Contains(';'))
            {
                PartitionKey = Hash.Split(';')[1];
                Hash = Hash.Split(';')[0];
            }

            Collection = Collection.ToLower();

            if (bReadOnly)
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
                        var response = await oClient.PostAsync((jaindburl + "/upload/" + Hash), oCont, ct);

                        if (!string.IsNullOrEmpty(await response.Content.ReadAsStringAsync(ct)))
                        {
                            if (ContinueAfterWrite)
                                return false;
                            else
                                return true;
                        }
                    }
                    return false;
                default:
                    return false;
            }
        }

        public async Task<bool> WriteLookupIDAsync(string name, string value, string id, CancellationToken ct = default(CancellationToken))
        {
            await Task.CompletedTask;
            return false;
        }
    }


}

