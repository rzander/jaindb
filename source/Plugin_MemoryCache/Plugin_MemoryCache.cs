using JainDBProvider;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Plugin_MemoryCache
{
    public class Plugin_MemoryCache : IStore
    {
        private IMemoryCache _cache;
        private bool bReadOnly = false;
        private long SlidingExpiration = -1;
        private bool ContinueAfterWrite = true;
        private bool CacheFull = true;
        private bool CacheKeys = true;

        private JObject JConfig = new JObject();

        public Dictionary<string, string> Settings { get; set; }

        public string Name
        {
            get
            {
                return Assembly.GetExecutingAssembly().ManifestModule.Name;
            }
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

                _cache = new MemoryCache(new MemoryCacheOptions());
            }
            catch { }
        }

        public bool WriteHash(string Hash, string Data, string Collection)
        {
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
                        jaindb.jDB.JSort(jObj);

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
                                                WriteLookupID(oSub.Name.ToLower(), (string)oSub.Value, sID);
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
                                                WriteLookupID(oSub.Name.ToLower(), (string)oSub.Value, sID);
                                            }
                                            catch { }
                                        }
                                    }
                                }
                            }
                        }
                    }catch(Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }

            //Check if MemoryCache is initialized
            if (_cache == null)
            {
                _cache = new MemoryCache(new MemoryCacheOptions());
            }

            string sResult = "";

            //Try to get value from Memory
            if (_cache.TryGetValue("RH-" + Collection + "-" + Hash, out sResult))
            {
                if (sResult == Data)
                {
                    if (ContinueAfterWrite)
                        return false;
                    else
                        return true;
                }
            }

            //Cache Data
            if (SlidingExpiration >= 0)
            {
                var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(SlidingExpiration)); //cache hash for x Seconds
                _cache.Set("RH-" + Collection + "-" + Hash, Data, cacheEntryOptions);
            }
            else
            {
                var cacheEntryOptions = new MemoryCacheEntryOptions(); //cache hash forever
                _cache.Set("RH-" + Collection + "-" + Hash, Data, cacheEntryOptions);
            }

            if (ContinueAfterWrite)
                return false;
            else
                return true;
        }

        public string ReadHash(string Hash, string Collection)
        {
            string sResult = "";

            //Check if MemoryCache is initialized
            if (_cache == null)
            {
                _cache = new MemoryCache(new MemoryCacheOptions());
            }

            //Try to get value from Memory
            if (_cache.TryGetValue("RH-" + Collection + "-" + Hash, out sResult))
            {
                return sResult;
            }
            else
            {
                return "";
            }
        }

        public int totalDeviceCount(string sPath = "")
        {
            string sCount = "";

            //Check in MemoryCache
            if (_cache.TryGetValue("RH-totaldevicecount-", out sCount))
            {
                return int.Parse(sCount);
            }

            return -1;
        }

        public IAsyncEnumerable<JObject> GetRawAssetsAsync(string paths)
        {
            return null; //We cannot list objects
        }

        public string LookupID(string name, string value)
        {
            string sResult = null;
            //Check in MemoryCache
            if (_cache.TryGetValue("ID-" + name.ToLower() + value.ToLower(), out sResult))
            {
                return sResult;
            }
            else
            {
                return null;
            }
        }

        public bool WriteLookupID(string name, string value, string id)
        {
            if (bReadOnly)
                return false;

            try
            {
                var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(SlidingExpiration)); //cache hash for x Seconds
                _cache.Set("ID-" + name.ToLower() + value.ToLower(), id, cacheEntryOptions);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public List<string> GetAllIDs()
        {
            return new List<string>();
        }
    }


}

