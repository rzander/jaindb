using JainDBProvider;
using Newtonsoft.Json.Linq;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Plugin_Redis
{
    public class Plugin_Redis : IStore
    {
        private bool bReadOnly = false;
        private int SlidingExpiration = -1;
        private bool ContinueAfterWrite = true;
        private bool CacheFull = true;
        private bool CacheKeys = true;
        private bool RedisEnabled = false;
        private string RedisConnectionString = "localhost:6379";

        private IDatabase cache0;
        private IDatabase cache1;
        private IDatabase cache2;
        private IDatabase cache3;
        private IDatabase cache4;
        private IServer srv;

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
                    File.WriteAllText(Assembly.GetExecutingAssembly().Location.Replace(".dll", ".json"), Properties.Resources.Plugin_Redis);
                }

                if (File.Exists(Assembly.GetExecutingAssembly().Location.Replace(".dll", ".json")))
                {
                    JConfig = JObject.Parse(File.ReadAllText(Assembly.GetExecutingAssembly().Location.Replace(".dll", ".json")));
                    bReadOnly = JConfig["ReadOnly"].Value<bool>();
                    SlidingExpiration = JConfig["SlidingExpiration"].Value<int>();
                    ContinueAfterWrite = JConfig["ContinueAfterWrite"].Value<bool>();
                    CacheFull = JConfig["CacheFull"].Value<bool>();
                    CacheKeys = JConfig["CacheKeys"].Value<bool>();
                    RedisConnectionString = JConfig["RedisConnectionString"].Value<string>();
                }
                else
                {
                    JConfig = new JObject();
                }

                try
                {
                    RedisConnectorHelper.ConnectionString = RedisConnectionString;

                    if (cache0 == null)
                    {
                        cache0 = RedisConnectorHelper.Connection.GetDatabase(0);
                        cache1 = RedisConnectorHelper.Connection.GetDatabase(1);
                        cache2 = RedisConnectorHelper.Connection.GetDatabase(2);
                        cache3 = RedisConnectorHelper.Connection.GetDatabase(3);
                        cache4 = RedisConnectorHelper.Connection.GetDatabase(4);
                    }

                    if (srv == null)
                        srv = RedisConnectorHelper.Connection.GetServer(RedisConnectorHelper.Connection.GetEndPoints(true)[0]);

                    RedisEnabled = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR: " + ex.Message);
                    Console.WriteLine("Redis = disabled !!!");
                    RedisEnabled = false;
                }
            }
            catch { }
        }

        public bool WriteHash(string Hash, string Data, string Collection)
        {
            if (!RedisEnabled)
                return false;

            if (bReadOnly)
                return false;

            if (string.IsNullOrEmpty(Data) || Data == "null")
            {
                if (ContinueAfterWrite)
                    return false;
                else
                    return true;
            }

            Collection = Collection.ToLower();

            switch (Collection)
            {
                case "_full":
                    JObject jObj = new JObject();
                    if (CacheFull)
                    {
                        //DB 0 = Full Inv
                        jObj = JObject.Parse(Data);
                        jaindb.jDB.JSort(jObj);

                        if (jObj["#id"] == null)
                            jObj.Add("#id", Hash);

                        string sID = jObj["#id"].ToString();

                        if (SlidingExpiration <= 0)
                            cache0.StringSetAsync(sID, jObj.ToString(Newtonsoft.Json.Formatting.None));
                        else
                            cache0.StringSetAsync(sID, jObj.ToString(Newtonsoft.Json.Formatting.None), new TimeSpan(0, 0, 0, SlidingExpiration));
                    }

                    if (CacheKeys)
                    {
                        string sID = jObj["#id"].ToString();
                        //Store KeyNames for 90Days
                        foreach (JProperty oSub in jObj.Properties())
                        {
                            if (oSub.Name.StartsWith("#"))
                            {
                                if (oSub.Value.Type == JTokenType.Array)
                                {
                                    foreach (var oSubSub in oSub.Values())
                                    {
                                        if (oSubSub.ToString() != sID)
                                        {
                                            WriteLookupID(oSub.Name.ToLower(), oSubSub.ToString().ToLower(), sID);
                                        }
                                    }
                                }
                                else
                                {
                                    if (!string.IsNullOrEmpty((string)oSub.Value))
                                    {
                                        if (oSub.Value.ToString() != sID)
                                        {
                                            WriteLookupID(oSub.Name.ToLower(), oSub.Value.ToString().ToLower(), sID);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    break;

                case "_chain":
                    var jObj3 = JObject.Parse(Data);
                    jaindb.jDB.JSort(jObj3);

                    if(SlidingExpiration <= 0)
                        cache3.StringSetAsync(Hash, jObj3.ToString(Newtonsoft.Json.Formatting.None));
                    else
                        cache3.StringSetAsync(Hash, jObj3.ToString(Newtonsoft.Json.Formatting.None), new TimeSpan(0, 0, 0, SlidingExpiration));
                    break;

                case "_assets":
                    var jObj4 = JObject.Parse(Data);
                    jaindb.jDB.JSort(jObj4);

                    if (SlidingExpiration <= 0)
                        cache4.StringSetAsync(Hash, jObj4.ToString(Newtonsoft.Json.Formatting.None));
                    else
                        cache4.StringSetAsync(Hash, jObj4.ToString(Newtonsoft.Json.Formatting.None), new TimeSpan(0, 0, 0, SlidingExpiration));
                    break;

                default:
                    var jObj2 = JObject.Parse(Data);
                    jaindb.jDB.JSort(jObj2);

                    if (SlidingExpiration <= 0)
                        cache2.StringSetAsync(Hash, jObj2.ToString(Newtonsoft.Json.Formatting.None));
                    else
                        cache2.StringSetAsync(Hash, jObj2.ToString(Newtonsoft.Json.Formatting.None), new TimeSpan(0, 0, 0, SlidingExpiration));
                    break;
            }

            if (ContinueAfterWrite)
                return false;
            else
                return true;
        }

        public string ReadHash(string Hash, string Collection)
        {
            if (!RedisEnabled)
                return "";

            string sResult = "";

            try
            {
                Collection = Collection.ToLower();

                switch (Collection)
                {
                    case "_full":
                        return cache0.StringGet(Hash);

                    case "_chain":
                        return cache3.StringGet(Hash);

                    case "_assets":
                        return cache4.StringGet(Hash);

                    default:
                        sResult = cache2.StringGet(Hash);
                        return sResult;
                }
            }
            catch { }

            return sResult;
        }

        public int totalDeviceCount(string sPath = "")
        {
            if (!RedisEnabled)
                return -1;

            try
            {
                return srv.Keys(3, "*").Count();
            }
            catch { }

            return -1;
        }

        public IEnumerable<JObject> GetRawAssets(string paths)
        {
            if (RedisEnabled)
            {
                foreach (var oObj in srv.Keys(4, "*"))
                {
                    JObject jObj = jaindb.jDB.GetRaw(ReadHash(oObj, "_assets"), paths);

                    if (paths.Contains("*") || paths.Contains(".."))
                    {
                        try
                        {
                            jObj = jaindb.jDB.GetFull(jObj["#id"].Value<string>(), jObj["_index"].Value<int>());
                        }
                        catch { }
                    }
                    yield return jObj;
                }
            }
        }

        public string LookupID(string name, string value)
        {
            if (!RedisEnabled)
                return "";

            string sResult = null;
            try
            {
                sResult = cache1.StringGet(name.ToLower().TrimStart('#', '@') + "/" + value.ToLower());
            }
            catch { }

            return sResult;
        }

        public bool WriteLookupID(string name, string value, string id)
        {
            if (bReadOnly)
                return false;

            if (!RedisEnabled)
                return false;

            if (SlidingExpiration <= 0)
                cache1.StringSetAsync(name.ToLower().TrimStart('#') + "/" + value.ToLower(), id);
            else
                cache1.StringSetAsync(name.ToLower().TrimStart('#') + "/" + value.ToLower(), id, new TimeSpan(0, 0, 0, SlidingExpiration));

            return false;
        }

        public List<string> GetAllIDs()
        {
            if (!RedisEnabled)
                return new List<string>();

            List<string> lResult = new List<string>();

            try
            {
                foreach (var oObj in srv.Keys(3, "*"))
                {
                    lResult.Add(oObj.ToString());
                }
            }
            catch { }

            return lResult;
        }

        public class RedisConnectorHelper
        {
            //public static string RedisServer = "localhost";
            //public static int RedisPort = 6379;
            public static string ConnectionString = "";

            static RedisConnectorHelper()
            {
                RedisConnectorHelper.lazyConnection = new Lazy<ConnectionMultiplexer>(() =>
                {
                    //ConfigurationOptions oOpt = new ConfigurationOptions();
                    //oOpt.EndPoints.Add(RedisServer + ":" + RedisPort.ToString());
                    //oOpt.AbortOnConnectFail = true;

                    return ConnectionMultiplexer.Connect(ConnectionString);
                });
            }

            private static Lazy<ConnectionMultiplexer> lazyConnection;

            public static ConnectionMultiplexer Connection => lazyConnection.Value;
        }
    }


}

