using JainDBProvider;
//using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Caching;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Plugin_MemoryCache
{
    public class Plugin_MemoryCache : IStore
    {
        private MemoryCache _cache;
        private bool bReadOnly = false;
        private bool CacheFull = true;
        private bool CacheKeys = true;
        private bool ContinueAfterWrite = true;
        private JObject JConfig = new JObject();
        private long SlidingExpiration = -1;
        public string Name
        {
            get
            {
                return Assembly.GetExecutingAssembly().ManifestModule.Name;
            }
        }

        public Dictionary<string, string> Settings { get; set; }

        public async Task<List<string>> GetAllIDsAsync(CancellationToken ct)
        {
            await Task.CompletedTask;
            return new List<string>();
        }

        public async IAsyncEnumerable<JObject> GetRawAssetsAsync(string paths, [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask;
            yield break;
            //return null; //We cannot list objects
        }

        public void Init()
        {
            if (Settings == null)
                Settings = new Dictionary<string, string>();

            try
            {
                if (!File.Exists(Assembly.GetExecutingAssembly().Location.Replace(".dll", ".json")))
                {
                    File.WriteAllText(Assembly.GetExecutingAssembly().Location.Replace(".dll", ".json"), Properties.Resources.Plugin_MemoryCache);
                }

                if (File.Exists(Assembly.GetExecutingAssembly().Location.Replace(".dll", ".json")))
                {
                    JConfig = JObject.Parse(File.ReadAllText(Assembly.GetExecutingAssembly().Location.Replace(".dll", ".json")));
                    bReadOnly = JConfig["ReadOnly"].Value<bool>();
                    SlidingExpiration = JConfig["SlidingExpiration"].Value<long>();
                    ContinueAfterWrite = JConfig["ContinueAfterWrite"].Value<bool>();
                    CacheFull = JConfig["CacheFull"].Value<bool>();
                    CacheKeys = JConfig["CacheKeys"].Value<bool>();
                }
                else
                {
                    JConfig = new JObject();
                }

                if (_cache == null)
                    _cache = new MemoryCache("JainDB");

            }
            catch { }
        }

        public async Task<string> LookupIDAsync(string name, string value, CancellationToken ct = default(CancellationToken))
        {
            return await Task.Run(() =>
            {
                string sResult = _cache["ID-" + name.ToLower() + value.ToLower()] as string;

                //Check in MemoryCache
                if (!string.IsNullOrEmpty(sResult))
                {
                    return sResult;
                }
                else
                {
                    return null;
                }
            });
        }

        public async Task<string> ReadHashAsync(string Hash, string Collection, CancellationToken ct = default(CancellationToken))
        {
            string PartitionKey = "";

            if (Hash.Contains(';'))
            {
                PartitionKey = Hash.Split(';')[1];
                Hash = Hash.Split(';')[0];
            }

            return await Task.Run(() =>
            {
                string sResult = _cache["RH-" + Collection + "-" + Hash] as string;

                if (!string.IsNullOrEmpty(sResult))
                {
                    return sResult;
                }
                else
                {
                    return "";
                }
            }, ct);
        }

        public async Task<int> totalDeviceCountAsync(string sPath = "", CancellationToken ct = default(CancellationToken))
        {
            return await Task.Run(() =>
            {
                string sCount = _cache["RH-totaldevicecount-"] as string; 

                //Check in MemoryCache
                if (!string.IsNullOrEmpty(sCount))
                {
                    return int.Parse(sCount);
                }

                return -1;
            });
        }

        public async Task<bool> WriteHashAsync(string Hash, string Data, string Collection, CancellationToken ct = default(CancellationToken))
        {
            string PartitionKey = "";

            if (Hash.Contains(';'))
            {
                PartitionKey = Hash.Split(';')[1];
                Hash = Hash.Split(';')[0];
            }

            return await Task.Run(async () => {
                if (bReadOnly)
                    return false;

                if (string.IsNullOrEmpty(Data) || Data == "null")
                    return true;

                Collection = Collection.ToLower();

                if (Collection == "_full")
                {
                    if (!CacheFull) //exit if ChacheFull is not set
                    {
                        if (ContinueAfterWrite)
                            return false;
                        else
                            return true;
                    }

                    if (CacheKeys)
                    {
                        try
                        {
                            var jObj = JObject.Parse(Data);

                            if (jObj["#id"] != null)
                            {

                                await jaindb.jDB.JSortAsync(jObj, false, ct);

                                string sID = jObj["#id"].ToString();

                                //Store KeyNames
                                foreach (JProperty oSub in jObj.Properties())
                                {
                                    if (oSub.Name.StartsWith("#"))
                                    {
                                        if (oSub.Value.Type == JTokenType.Array)
                                        {
                                            foreach (var oSubSub in oSub.Values())
                                            {
                                                try
                                                {
                                                    if (oSubSub.ToString() != sID)
                                                    {
                                                        _ = WriteLookupIDAsync(oSub.Name.ToLower(), (string)oSub.Value, sID, ct);
                                                    }
                                                }
                                                catch { }
                                            }

                                        }
                                        else
                                        {
                                            if (!string.IsNullOrEmpty((string)oSub.Value))
                                            {
                                                if (oSub.Value.ToString() != sID)
                                                {
                                                    try
                                                    {
                                                        _ = WriteLookupIDAsync(oSub.Name.ToLower(), (string)oSub.Value, sID, ct);
                                                    }
                                                    catch { }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                    }
                }

                //Try to get value from Memory
                //string sResult = _cache["RH-" + Collection + "-" + Hash] as string;

                //if (!string.IsNullOrEmpty(sResult))
                //{
                //    if (sResult == Data)
                //    {
                //        if (ContinueAfterWrite)
                //            return false;
                //        else
                //            return true;
                //    }
                //}

                //Cache Data
                if (SlidingExpiration >= 0)
                {
                    CacheItemPolicy cPol = new CacheItemPolicy() { SlidingExpiration = TimeSpan.FromSeconds(SlidingExpiration) };
                    _cache.Set("RH-" + Collection + "-" + Hash, Data, cPol);
                }
                else
                {
                    //var cacheEntryOptions = new MemoryCacheEntryOptions(); //cache hash forever
                    _cache.Set("RH-" + Collection + "-" + Hash, Data, new CacheItemPolicy());
                }

                if (ContinueAfterWrite)
                    return false;
                else
                    return true;
            }, ct);
        }

        public async Task<bool> WriteLookupIDAsync(string name, string value, string id, CancellationToken ct = default(CancellationToken))
        {
            return await Task.Run(() =>
            {
                if (bReadOnly)
                    return false;

                try
                {
                    //var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(SlidingExpiration)); //cache hash for x Seconds
                    _cache.Set("ID-" + name.ToLower() + value.ToLower(), id, new CacheItemPolicy() { SlidingExpiration = TimeSpan.FromSeconds(SlidingExpiration) });

                    return true;
                }
                catch
                {
                    return false;
                }
            });
        }
    }


}

