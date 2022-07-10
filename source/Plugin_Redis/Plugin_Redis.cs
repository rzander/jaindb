using jaindb;
using JainDBProvider;
using Newtonsoft.Json.Linq;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Plugin_Redis
{
    public class Plugin_Redis : IStore
    {
        private bool bReadOnly = false;
        private IDatabase cache0;
        private IDatabase cache1;
        private IDatabase cache2;
        private IDatabase cache3;
        private IDatabase cache4;
        private bool CacheFull = true;
        private bool CacheKeys = true;
        private bool ContinueAfterWrite = true;
        private JObject JConfig = new JObject();
        private string RedisConnectionString = "localhost:6379";
        private bool RedisEnabled = false;
        private int SlidingExpiration = -1;
        private IServer srv;
        private ConnectionMultiplexer redis;

        public string Name
        {
            get
            {
                return Assembly.GetExecutingAssembly().ManifestModule.Name;
            }
        }

        public Dictionary<string, string> Settings { get; set; }

        public async Task<List<string>> GetAllIDsAsync(CancellationToken ct = default(CancellationToken))
        {
            if (!RedisEnabled)
                return new List<string>();

            List<string> lResult = new List<string>();

            try
            {
                await foreach (var oObj in srv.KeysAsync(3, "*"))
                {
                    lResult.Add(oObj.ToString());
                }
            }
            catch { }

            return lResult;
        }

        public async IAsyncEnumerable<JObject> GetRawAssetsAsync(string paths, [EnumeratorCancellation] CancellationToken ct = default(CancellationToken))
        {
            if (RedisEnabled)
            {
                foreach (var oObj in srv.Keys(4, "*"))
                {
                    var oAsset = await jDB.ReadHashAsync(oObj, "_assets", ct); //get raw asset

                    JObject jObj = new JObject();

                    if (paths.Contains("*") || paths.Contains(".."))
                    {
                        try
                        {
                            jObj = await jDB.GetFullAsync(jObj["#id"].Value<string>(), jObj["_index"].Value<int>(), "", false, ct);
                        }
                        catch { }
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(paths))
                            jObj = await jDB.GetRawAsync(oAsset, paths, ct); //load only the path
                        else
                            jObj = JObject.Parse(oAsset); //if not paths, we only return the raw data
                    }

                    if (jObj["_hash"] == null)
                        jObj.Add(new JProperty("_hash", oObj.ToString()));

                    yield return jObj;
                }
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
                    redis = ConnectionMultiplexer.Connect(RedisConnectionString);

                    cache0 =  redis.GetDatabase(0);
                    cache1 = redis.GetDatabase(1);
                    cache2 = redis.GetDatabase(2);
                    cache3 = redis.GetDatabase(3);
                    cache4 = redis.GetDatabase(4);

                    //RedisConnectorHelper.ConnectionString = RedisConnectionString;

                    //if (cache0 == null)
                    //{
                    //    cache0 = RedisConnectorHelper.Connection.GetDatabase(0);
                    //    cache1 = RedisConnectorHelper.Connection.GetDatabase(1);
                    //    cache2 = RedisConnectorHelper.Connection.GetDatabase(2);
                    //    cache3 = RedisConnectorHelper.Connection.GetDatabase(3);
                    //    cache4 = RedisConnectorHelper.Connection.GetDatabase(4);
                    //}

                    if (srv == null) {
                        srv = redis.GetServer(redis.GetEndPoints(true)[0]);
                        //srv = RedisConnectorHelper.Connection.GetServer(RedisConnectorHelper.Connection.GetEndPoints(true)[0]);
                    }

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

        public async Task<string> LookupIDAsync(string name, string value, CancellationToken ct = default(CancellationToken))
        {
            if (!RedisEnabled)
                return "";

            string sResult = null;
            try
            {
                sResult = await cache1.StringGetAsync(name.ToLower().TrimStart('#', '@') + "/" + value.ToLower());
            }
            catch { }

            return sResult;
        }

        public async Task<string> ReadHashAsync(string Hash, string Collection, CancellationToken ct = default(CancellationToken))
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
                        return await cache0.StringGetAsync(Hash);

                    case "_chain":
                        return await cache3.StringGetAsync(Hash);

                    case "_assets":
                        return await cache4.StringGetAsync(Hash);

                    default:
                        sResult = await cache2.StringGetAsync(Hash);
                        return sResult;
                }
            }
            catch { }

            return sResult;
        }

        public async Task<int> totalDeviceCountAsync(string sPath = "", CancellationToken ct = default(CancellationToken))
        {
            if (!RedisEnabled)
                return -1;

            try
            {
                return await srv.KeysAsync(3, "*").CountAsync();
            }
            catch { }

            return -1;
        }

        public async Task<bool> WriteHashAsync(string Hash, string Data, string Collection, CancellationToken ct = default)
        {
            if (!RedisEnabled)
                return false;

            if (bReadOnly)
                return false;

            if (string.IsNullOrEmpty(Hash))
                return false;

            if (string.IsNullOrEmpty(Data) || Data == "null")
            {
                if (ContinueAfterWrite)
                    return false;
                else
                    return true;
            }

            switch (Collection.ToLower())
            {
                case "_full":
                    JObject jObj = new JObject();
                    if (CacheFull)
                    {
                        //DB 0 = Full Inv
                        jObj = JObject.Parse(Data);
                        //await jDB.JSortAsync(jObj, false, ct);

                        if (jObj["#id"] == null)
                            jObj.Add("#id", Hash);

                        string sID = jObj["#id"].ToString();

                        if (SlidingExpiration <= 0)
                            _ = cache0.StringSetAsync(sID, jObj.ToString(Newtonsoft.Json.Formatting.None));
                        else
                            _ = cache0.StringSetAsync(sID, jObj.ToString(Newtonsoft.Json.Formatting.None), new TimeSpan(0, 0, 0, SlidingExpiration));
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
                                            _ = WriteLookupIDAsync(oSub.Name.ToLower(), oSubSub.ToString().ToLower(), sID, ct);
                                        }
                                    }
                                }
                                else
                                {
                                    if (!string.IsNullOrEmpty((string)oSub.Value))
                                    {
                                        if (oSub.Value.ToString() != sID)
                                        {
                                            _ = WriteLookupIDAsync(oSub.Name.ToLower(), oSub.Value.ToString().ToLower(), sID, ct);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    break;
                case "_chain":
                    var jObj3 = JObject.Parse(Data);
                    //await jDB.JSortAsync(jObj3, false, ct);

                    if (SlidingExpiration <= 0)
                        _ = cache3.StringSetAsync(Hash, jObj3.ToString(Newtonsoft.Json.Formatting.None));
                    else
                        _ = cache3.StringSetAsync(Hash, jObj3.ToString(Newtonsoft.Json.Formatting.None), new TimeSpan(0, 0, 0, SlidingExpiration));
                    break;

                case "_assets":
                    var jObj4 = JObject.Parse(Data);
                    //await jDB.JSortAsync(jObj4, false, ct);

                    if (SlidingExpiration <= 0)
                        _ = cache4.StringSetAsync(Hash, jObj4.ToString(Newtonsoft.Json.Formatting.None));
                    else
                        _ = cache4.StringSetAsync(Hash, jObj4.ToString(Newtonsoft.Json.Formatting.None), new TimeSpan(0, 0, 0, SlidingExpiration));
                    break;

                default:
                    //var jObj2 = JObject.Parse(Data);
                    //await jDB.JSortAsync(jObj2, false, ct);

                    if (SlidingExpiration <= 0)
                        _ = cache2.StringSetAsync(Hash, Data);
                    else
                        _ = cache2.StringSetAsync(Hash, Data, new TimeSpan(0, 0, 0, SlidingExpiration));
                    break;
            }

            await Task.CompletedTask;

            if (ContinueAfterWrite)
                return false;
            else
                return true;

            //return true;
        }

        public async Task<bool> WriteLookupIDAsync(string name, string value, string id, CancellationToken ct = default(CancellationToken))
        {
            if (bReadOnly)
                return false;

            if (!RedisEnabled)
                return false;

            if (SlidingExpiration <= 0)
                _ = cache1.StringSetAsync(name.ToLower().TrimStart('#') + "/" + value.ToLower(), id);
            else
                _ = cache1.StringSetAsync(name.ToLower().TrimStart('#') + "/" + value.ToLower(), id, new TimeSpan(0, 0, 0, SlidingExpiration));

            await Task.CompletedTask;
            return false;
        }

        public class RedisConnectorHelper
        {
            //public static string RedisServer = "localhost";
            //public static int RedisPort = 6379;
            public static string ConnectionString = "";

            private static Lazy<ConnectionMultiplexer> lazyConnection;

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

            public static ConnectionMultiplexer Connection => lazyConnection.Value;
        }
    }
}