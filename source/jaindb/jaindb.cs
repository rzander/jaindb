// ************************************************************************************
//          jaindb (c) Copyright 2018 by Roger Zander
// ************************************************************************************

using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using static jaindb.BlockChain;

namespace jaindb
{
    public class RedisConnectorHelper
    {
        public static string RedisServer = "localhost";
        public static int RedisPort = 6379;

        static RedisConnectorHelper()
        {
            RedisConnectorHelper.lazyConnection = new Lazy<ConnectionMultiplexer>(() =>
            {
                ConfigurationOptions oOpt = new ConfigurationOptions();
                oOpt.EndPoints.Add(RedisServer + ":" + RedisPort.ToString());
                oOpt.AbortOnConnectFail = true;

                return ConnectionMultiplexer.Connect(oOpt);
            });
        }

        private static Lazy<ConnectionMultiplexer> lazyConnection;

        public static ConnectionMultiplexer Connection => lazyConnection.Value;
    }

    public static class jDB
    {
        public enum hashType { MD5, SHA2_256 } //Implemented Hash types
        private static readonly object locker = new object();
        private static HttpClient oClient = new HttpClient();

        public static bool UseCosmosDB;
        public static bool UseRedis;
        public static bool UseFileStore;

        internal static string databaseId;
        internal static string endpointUrl;
        internal static string authorizationKey;
        internal static DocumentClient CosmosDB;
        internal static Database database;

        internal static IDatabase cache0;
        internal static IDatabase cache1;
        internal static IDatabase cache2;
        internal static IDatabase cache3;
        internal static IDatabase cache4;
        internal static IServer srv;

        internal static IMemoryCache _cache;

        public static hashType HashType = hashType.MD5;

        public static string BlockType = "INV";
        public static int PoWComplexitity = 0; //Proof of Work complexity; 0 = no PoW; 8 = 8 trailing bits of the block hash must be '0'

        public static string CalculateHash(string input)
        {
            switch (HashType)
            {
                case hashType.MD5:
                    return Hash.CalculateMD5HashString(input);
                case hashType.SHA2_256:
                    return Hash.CalculateSHA2_256HashString(input);
                default:
                    return Hash.CalculateMD5HashString(input); ;
            }
        }

        /// <summary>
        /// Lookup Key ID's to search for Objects based on their Key
        /// </summary>
        /// <param name="name">Key name to search. E.g. "name"</param>
        /// <param name="value">Value to search. E.g. "computer01"</param>
        /// <returns>Hash ID of the Object</returns>
        public static string LookupID(string name, string value)
        {
            string sResult = "";
            try
            {
                //Check in MemoryCache
                if (_cache.TryGetValue("ID-" + name, out sResult))
                {
                    return sResult;
                }
                else
                {
                    if (UseRedis)
                    {
                        sResult = cache1.StringGet(name.ToLower().TrimStart('#', '@') + "/" + value.ToLower());

                        //Cache result in Memory
                        if (!string.IsNullOrEmpty(sResult))
                        {
                            var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(60)); //cache ID for 1min
                            _cache.Set("ID-" + name, sResult, cacheEntryOptions);
                        }

                        return sResult;
                    }

                    if (UseFileStore || UseCosmosDB)
                    {

                        sResult = File.ReadAllText("wwwroot\\" + "_Key" + "\\" + name.TrimStart('#', '@') + "\\" + value + ".json");

                        //Cache result in Memory
                        if (!string.IsNullOrEmpty(sResult))
                        {
                            var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(300)); //cache ID for 5min
                            _cache.Set("ID-" + name, sResult, cacheEntryOptions);
                        }

                        return sResult;

                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error LookupID_1: " + ex.Message.ToString());
            }

            return sResult;
        }

        public static void WriteHash(ref JToken oRoot, ref JObject oStatic, string Collection)
        {
            try
            {
                //Remove NULL values
                /*foreach (var oTok in ((JContainer)oRoot).Descendants().Where(t => t.Type == (JTokenType.Object) && t.HasValues == false).ToList())
                {
                    try
                    {
                        if (oTok.Parent.Type != JTokenType.Array)
                            oTok.Parent.Remove();
                        if (oTok.Parent.Type == JTokenType.Array)
                        {
                            if (oTok.Parent.Parent.Count == 1)
                                oTok.Parent.Parent.Remove();
                        }

                    }
                    catch (Exception ex)
                    {
                        ex.Message.ToString();
                    }
                }*/

                //JSort(oStatic);
                string sHash = CalculateHash(oRoot.ToString(Newtonsoft.Json.Formatting.None));
                if (string.IsNullOrEmpty(sHash))
                    return;
                string sPath = oRoot.Path;

                var oClass = oStatic.SelectToken(sPath);// as JObject;

                if (oClass != null)
                {
                    if (oClass.Type == JTokenType.Object)
                    {
                        ((JObject)oClass).Add("##hash", sHash);

                        WriteHashAsync(sHash, oRoot.ToString(Formatting.None), Collection);

                        oRoot = oClass;
                    }
                }

            }
            catch (Exception ex)
            {
                ex.Message.ToString();
            }
        }

        public static bool WriteHash(string Hash, string Data, string Collection)
        {
            try
            {
                //Cache result in Memory
                if (!string.IsNullOrEmpty(Data))
                {
                    var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(60)); //cache hash for 60s
                    _cache.Set("RH-" + Collection + "-" + Hash, Data, cacheEntryOptions);
                }

                if (string.IsNullOrEmpty(Data) || Data == "null")
                    return true;

                if (UseRedis)
                {
                    switch (Collection.ToLower())
                    {
                        case "_full":
                            //DB 0 = Full Inv
                            var jObj = JObject.Parse(Data);
                            JSort(jObj);

                            string sID = jObj["#id"].ToString();

                            //Store FullContent for 30Days
                            cache0.StringSetAsync(sID, jObj.ToString(Newtonsoft.Json.Formatting.None), new TimeSpan(30, 0, 0, 0));

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
                                                //Store Keys in lower case
                                                cache1.StringSetAsync(oSub.Name.ToLower().TrimStart('#') + "/" + oSubSub.ToString().ToLower(), sID, new TimeSpan(90, 0, 0, 0));
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (!string.IsNullOrEmpty((string)oSub.Value))
                                        {
                                            if (oSub.Value.ToString() != sID)
                                            {
                                                //Store Keys in lower case
                                                cache1.StringSetAsync(oSub.Name.ToLower().TrimStart('#') + "/" + oSub.Value.ToString().ToLower(), sID, new TimeSpan(90, 0, 0, 0));
                                            }
                                        }
                                    }
                                }
                            }

                            if (UseFileStore)
                            {
                                if (!Directory.Exists("wwwroot\\" + Collection))
                                    Directory.CreateDirectory("wwwroot\\" + Collection);

                                if (!File.Exists("wwwroot\\" + Collection + "\\" + Hash + ".json")) //We do not have to create the same hash file twice...
                                {
                                    lock (locker) //only one write operation
                                    {
                                        File.WriteAllText("wwwroot\\" + Collection + "\\" + Hash + ".json", Data);
                                    }
                                }
                            }
                            break;

                        case "chain":
                            var jObj3 = JObject.Parse(Data);
                            JSort(jObj3);

                            cache3.StringSetAsync(Hash, jObj3.ToString(Newtonsoft.Json.Formatting.None));
                            break;

                        case "assets":
                            var jObj4 = JObject.Parse(Data);
                            JSort(jObj4);

                            cache4.StringSetAsync(Hash, jObj4.ToString(Newtonsoft.Json.Formatting.None));
                            break;

                        default:
                            var jObj2 = JObject.Parse(Data);
                            JSort(jObj2);

                            cache2.StringSetAsync(Hash, jObj2.ToString(Newtonsoft.Json.Formatting.None));
                            break;
                    }

                }

                if (UseCosmosDB)
                {
                    string sColl = Collection;
                    if (Collection == "_full")
                        sColl = "Full";

                    if (database == null)
                    {
                        database = CosmosDB.CreateDatabaseQuery().Where(db => db.Id == databaseId).AsEnumerable().FirstOrDefault();
                        if (database == null)
                            database = CosmosDB.CreateDatabaseAsync(new Database { Id = databaseId }).Result;
                    }

                    CosmosDB.CreateDocumentCollectionIfNotExistsAsync(database.SelfLink, new DocumentCollection { Id = sColl }).Wait();

                    //string sJ = "{ \"Id\" : \"" + Hash + "\"," + Data.TrimStart('{');
                    var jObj = JObject.Parse(Data);
                    jObj.Add("id", Hash);
                    jObj.Remove("#id");
                    CosmosDB.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(databaseId, sColl), jObj).Wait();
                }

                if (UseCosmosDB || UseRedis)
                    return true;

                if (UseFileStore)
                {
                    //Remove invalid Characters in Path and Hash
                    foreach (var sChar in Path.GetInvalidPathChars())
                    {
                        Collection = Collection.Replace(sChar.ToString(), "");
                        Hash= Hash.Replace(sChar.ToString(), "");
                    }

                    if (!Directory.Exists("wwwroot\\" + Collection))
                        Directory.CreateDirectory("wwwroot\\" + Collection);

                    switch (Collection.ToLower())
                    {
                        case "_full":
                            //DB 0 = Full Inv
                            var jObj = JObject.Parse(Data);
                            JSort(jObj);

                            string sID = jObj["#id"].ToString();

                            if (!Directory.Exists("wwwroot\\" + "_Key"))
                                Directory.CreateDirectory("wwwroot\\" + "_Key");

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
                                                    string sDir = "wwwroot\\" + "_Key" + "\\" + oSub.Name.ToLower().TrimStart('#');

                                                    //Remove invalid Characters in Path
                                                    foreach (var sChar in Path.GetInvalidPathChars())
                                                    {
                                                        sDir = sDir.Replace(sChar.ToString(), "");
                                                    }

                                                    if (!Directory.Exists(sDir))
                                                        Directory.CreateDirectory(sDir);

                                                    File.WriteAllText(sDir + "\\" + oSubSub.ToString() + ".json", sID);
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
                                                    string sDir = "wwwroot\\" + "_Key" + "\\" + oSub.Name.ToLower().TrimStart('#');

                                                    //Remove invalid Characters in Path
                                                    foreach (var sChar in Path.GetInvalidPathChars())
                                                    {
                                                        sDir = sDir.Replace(sChar.ToString(), "");
                                                    }

                                                    if (!Directory.Exists(sDir))
                                                        Directory.CreateDirectory(sDir);

                                                    File.WriteAllText(sDir + "\\" + (string)oSub.Value + ".json", sID);
                                                }
                                                catch { }
                                            }
                                        }
                                    }
                                }
                            }

                            lock (locker) //only one write operation
                            {
                                File.WriteAllText("wwwroot\\" + Collection + "\\" + Hash + ".json", Data);
                            }
                            break;

                        case "chain":
                            lock (locker) //only one write operation
                            {
                                File.WriteAllText("wwwroot\\" + Collection + "\\" + Hash + ".json", Data);
                            }
                            break;

                        default:
                            if (!File.Exists("wwwroot\\" + Collection + "\\" + Hash + ".json")) //We do not have to create the same hash file twice...
                            {
                                lock (locker) //only one write operation
                                {
                                    File.WriteAllText("wwwroot\\" + Collection + "\\" + Hash + ".json", Data);
                                }
                            }
                            break;
                    }

                    return true;
                }



            }
            catch
            {
                if (!Directory.Exists("wwwroot\\" + Collection))
                    Directory.CreateDirectory("wwwroot\\" + Collection);

                if (!File.Exists("wwwroot\\" + Collection + "\\" + Hash + ".json")) //We do not have to create the same hash file twice...
                {
                    lock (locker) //only one write operation
                    {
                        File.WriteAllText("wwwroot\\" + Collection + "\\" + Hash + ".json", Data);
                    }
                }

                return true;
            }

            return false;
        }

        public static async Task<bool> WriteHashAsync(string Hash, string Data, string Collection)
        {
            return await Task.Run(() =>
            {
                return WriteHash(Hash, Data, Collection);
            });
        }

        public static string ReadHash(string Hash, string Collection)
        {
            string sResult = "";
            try
            {
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
                    if (UseRedis)
                    {
                        switch (Collection.ToLower())
                        {
                            case "_full":
                                return cache0.StringGet(Hash);

                            case "chain":
                                return cache3.StringGet(Hash);

                            case "assets":
                                return cache4.StringGet(Hash);

                            default:
                                sResult = cache2.StringGet(Hash);

#if DEBUG
                                //Check if hashes are valid...
                                var jData = JObject.Parse(sResult);
                                /*if (jData["#id"] != null)
                                    jData.Remove("#id");*/
                                if (jData["_date"] != null)
                                    jData.Remove("_date");
                                if (jData["_index"] != null)
                                    jData.Remove("_index");

                                string s1 = CalculateHash(jData.ToString(Formatting.None));
                                if (Hash != s1)
                                {
                                    s1.ToString();
                                    return "";
                                }
#endif

                                //Cache result in Memory
                                if (!string.IsNullOrEmpty(sResult))
                                {
                                    var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(30)); //cache hash for 30s
                                    _cache.Set("RH-" + Collection + "-" + Hash, sResult, cacheEntryOptions);
                                }
                                return sResult;
                        }
                    }

                    if (UseFileStore)
                    {
                        string Coll2 = Collection;
                        //Remove invalid Characters in Path anf File
                        foreach (var sChar in Path.GetInvalidPathChars())
                        {
                            Coll2 = Coll2.Replace(sChar.ToString(), "");
                            Hash = Hash.Replace(sChar.ToString(), "");
                        }

                        sResult = File.ReadAllText("wwwroot\\" + Coll2 + "\\" + Hash + ".json");

#if DEBUG
                        //Check if hashes are valid...
                        if (Collection.ToLower() != "_full" && Collection.ToLower() != "chain")
                        {
                            var jData = JObject.Parse(sResult);
                            /*if (jData["#id"] != null)
                                jData.Remove("#id");*/
                            if (jData["_date"] != null)
                                jData.Remove("_date");
                            if (jData["_index"] != null)
                                jData.Remove("_index");

                            string s1 = CalculateHash(jData.ToString(Formatting.None));
                            if (Hash != s1)
                            {
                                s1.ToString();
                                return "";
                            }
                        }
#endif


                        //Cache result in Memory
                        if (!string.IsNullOrEmpty(sResult))
                        {
                            var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(60)); //cache hash for 60s
                            _cache.Set("RH-" + Collection + "-" + Hash, sResult, cacheEntryOptions);
                        }

                        return sResult;
                    }

                    if (UseCosmosDB)
                    {
                        if (database == null)
                        {
                            database = CosmosDB.CreateDatabaseQuery().Where(db => db.Id == databaseId).AsEnumerable().FirstOrDefault();
                            if (database == null)
                                database = CosmosDB.CreateDatabaseAsync(new Database { Id = databaseId }).Result;
                        }

                        var sRes = CosmosDB.ReadDocumentAsync(UriFactory.CreateDocumentUri(databaseId, Collection, Hash)).Result.Resource;
                        JObject jRes = JObject.Parse(sRes.ToString());
                        jRes.Remove("id");
                        jRes.Remove("_rid");
                        jRes.Remove("_ts");
                        jRes.Remove("_etag");
                        jRes.Remove("_self");
                        jRes.Remove("_attachments");

                        sResult = jRes.ToString(Newtonsoft.Json.Formatting.None);

                        //Cache result in Memory
                        if (!string.IsNullOrEmpty(sResult))
                        {
                            var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(60)); //cache hash for 60s
                            _cache.Set("RH-" + Collection + "-" + Hash, sResult, cacheEntryOptions);
                        }
                        return sResult;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error ReadHash_1: " + ex.Message.ToString());
            }

            return sResult;
        }

        public static Blockchain GetChain(string DeviceID)
        {
            Blockchain oChain;
            string sData = ReadHash(DeviceID, "Chain");
            if (string.IsNullOrEmpty(sData))
            {
                oChain = new Blockchain("", "root", 0);
            }
            else
            {
                JsonSerializerSettings oSettings = new JsonSerializerSettings();
                var oC = JsonConvert.DeserializeObject(sData, typeof(Blockchain), oSettings);
                oChain = oC as Blockchain;
            }

            return oChain;
        }

        public static string UploadFull(string JSON, string DeviceID)
        {
            try
            {
                JObject oObj = JObject.Parse(JSON);

                //Remove NULL values
                foreach (var oTok in oObj.Descendants().Where(t => t.Parent.Type == (JTokenType.Property) && t.Type == JTokenType.Null).ToList())
                {
                    try
                    {
                        oTok.Parent.Remove();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Error UploadFull_1: " + ex.Message.ToString());
                    }
                }

                //Remove empty values
                foreach (var oTok in oObj.Descendants().Where(t => t.Type == (JTokenType.Object) && !t.HasValues).ToList())
                {
                    try
                    {
                        if (oTok.Parent.Type == JTokenType.Property)
                        {
                            oTok.Parent.Remove();
                            continue;
                        }

                        if (oTok.Parent.Type == JTokenType.Array)
                        {
                            if (oTok.Parent.Count == 1) //Parent is array with one empty child
                            {
                                if (oTok.Parent.Parent.Type == JTokenType.Property)
                                    oTok.Parent.Parent.Remove(); //remove parent
                            }
                            else
                                oTok.Remove(); //remove empty array item
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Error UploadFull_1: " + ex.Message.ToString());
                    }
                }


                JSort(oObj, true); //Enforce full sort

                JObject oStatic = oObj.ToObject<JObject>();
                JObject jTemp = oObj.ToObject<JObject>();

                //Load BlockChain
                Blockchain oChain = GetChain(DeviceID);


                JSort(oObj);
                JSort(oStatic);

                var jObj = oObj;

                //Loop through all ChildObjects
                foreach (var oChild in jObj.Descendants().Where(t => t.Type == JTokenType.Object).Reverse())
                {
                    try
                    {
                        JToken tRef = oObj.SelectToken(oChild.Path, false);

                        //check if tRfe is valid..
                        if (tRef == null)
                            continue;


                        string sName = "misc";
                        if (oChild.Parent.Type == JTokenType.Property)
                            sName = ((Newtonsoft.Json.Linq.JProperty)oChild.Parent).Name;
                        else
                            sName = ((Newtonsoft.Json.Linq.JProperty)oChild.Parent.Parent).Name; //it's an array

                        if (sName.StartsWith('@'))
                            continue;

                        foreach (JProperty jProp in oStatic.SelectToken(oChild.Path).Children().Where(t => t.Type == JTokenType.Property).ToList())
                        {
                            try
                            {
                                if (!jProp.Name.StartsWith('#'))
                                {
                                    if (jProp.Descendants().Where(t => t.Type == JTokenType.Property && ((JProperty)t).Name.StartsWith("#")).Count() == 0)
                                    {
                                        jProp.Remove();
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("Error UploadFull_2: " + ex.Message.ToString());
                            }
                        }


                        //remove all # and @ attributes
                        foreach (var oKey in tRef.Parent.Descendants().Where(t => t.Type == JTokenType.Property && (((JProperty)t).Name.StartsWith("#") || ((JProperty)t).Name.StartsWith("@"))).ToList())
                        {
                            try
                            {
                                oKey.Remove();
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("Error UploadFull_3: " + ex.Message.ToString());
                            }
                        }

                        WriteHash(ref tRef, ref oStatic, sName);
                        oObj.SelectToken(oChild.Path).Replace(tRef);

                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Error UploadFull_4: " + ex.Message.ToString());
                    }
                }

                //remove all # and @ objects
                foreach (var oKey in oStatic.Descendants().Where(t => t.Type == JTokenType.Property && ((JProperty)t).Name.StartsWith("@")).ToList())
                {
                    try
                    {
                        oKey.Remove();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Error UploadFull_5: " + ex.Message.ToString());
                    }
                }

                JSort(oStatic);
                //JSort(oStatic, true);

                string sResult = CalculateHash(oStatic.ToString(Newtonsoft.Json.Formatting.None));

                var oBlock = oChain.GetLastBlock();
                if (oBlock.data != sResult)
                {
                    var oNew = oChain.MineNewBlock(oBlock, BlockType);
                    oChain.UseBlock(sResult, oNew);

                    if (oChain.ValidateChain())
                    {
                        //Console.WriteLine(JsonConvert.SerializeObject(tChain));
                        if (oNew.index == 1)
                        {
                            Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +  " - new " + DeviceID);
                        }
                        else
                        {
                            Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " - update " + DeviceID);
                        }

                        WriteHashAsync(DeviceID, JsonConvert.SerializeObject(oChain), "Chain");

                        //Add missing attributes
                        if (oStatic["_date"] == null)
                            oStatic.AddFirst(new JProperty("_date", new DateTime(oNew.timestamp).ToUniversalTime()));
                        if (oStatic["_index"] == null)
                            oStatic.AddFirst(new JProperty("_index", oNew.index));
                        if (oStatic["#id"] == null)
                            oStatic.AddFirst(new JProperty("#id", DeviceID));

                        if (jTemp["_index"] == null)
                            jTemp.AddFirst(new JProperty("_index", oNew.index));
                        if (jTemp["_hash"] == null)
                            jTemp.AddFirst(new JProperty("_hash", oNew.data));
                        if (jTemp["_date"] == null)
                            jTemp.AddFirst(new JProperty("_date", new DateTime(oNew.timestamp).ToUniversalTime()));
                        if (jTemp["#id"] == null)
                            jTemp.AddFirst(new JProperty("#id", DeviceID));

                        //JSort(jTemp);

                        WriteHashAsync(DeviceID, jTemp.ToString(Formatting.None), "_Full");
                    }
                    else
                    {
                        Console.WriteLine("Blockchain is NOT valid... " + DeviceID);
                    }
                }

                //JSort(oStatic);
                WriteHashAsync(sResult, oStatic.ToString(Newtonsoft.Json.Formatting.None), "Assets");


                return sResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error UploadFull_6: " + ex.Message.ToString());
            }

            return "";
        }

        public static JObject GetFull(string DeviceID, int Index = -1)
        {
            try
            {
                JObject oInv = new JObject();
                /*if (_cache.TryGetValue("FULL-" + DeviceID + "-" + Index.ToString(), out oInv))
                {
                    return oInv;
                }*/

                //Chech if we have the full data in cache0
                if (Index == -1)
                {
                    string sFull = ReadHash(DeviceID, "_full");
                    //string sFull = cache0.StringGet(DeviceID);
                    if (!string.IsNullOrEmpty(sFull))
                    {
                        return JObject.Parse(sFull);
                    }
                }

                JObject oRaw = GetRawId(DeviceID, Index);
                string sData = ReadHash(oRaw["_hash"].ToString(), "Assets");

                if (!string.IsNullOrEmpty(sData))
                {
                    oInv = JObject.Parse(sData);
                    try
                    {
                        if (oInv["_index"] == null)
                            oInv.Add(new JProperty("_index", oRaw["_index"]));
                        if (oInv["_date"] == null)
                            oInv.Add(new JProperty("_date", oRaw["_date"]));
                        if (oInv["_hash"] == null)
                            oInv.Add(new JProperty("_hash", oRaw["_hash"]));
                    }
                    catch { }

                    List<string> lHashes = new List<string>();

                    //Load hashed values
                    foreach (JProperty oTok in oInv.Descendants().Where(t => t.Type == JTokenType.Property && ((JProperty)t).Name.StartsWith("##hash")).ToList())
                    {
                        lHashes.Add(oTok.Path);
                    }

                    //Remove merge ##hash with hasehd value
                    foreach (string sHash in lHashes)
                    {
                        try
                        {
                            JProperty oTok = oInv.SelectToken(sHash).Parent as JProperty;
                            string sH = oTok.Value.ToString();

                            List<string> aPathItems = oTok.Path.Split('.').ToList();
                            aPathItems.Reverse();
                            string sRoot = "";
                            if (aPathItems.Count > 1)
                                sRoot = aPathItems[1].Split('[')[0];

                            string sObj = ReadHash(sH, sRoot);
                            if (!string.IsNullOrEmpty(sObj))
                            {
                                var jStatic = JObject.Parse(sObj);
                                oTok.Parent.Merge(jStatic);
                                bool bLoop = true;
                                int i = 0;
                                //Remove NULL values as a result from merge
                                while (bLoop)
                                {
                                    bLoop = false;
                                    foreach (var jObj in (oTok.Parent.Descendants().Where(t => (t.Type == (JTokenType.Object) || t.Type == (JTokenType.Array)) && t.HasValues == false).Reverse().ToList()))
                                    {
                                        try
                                        {
                                            if ((jObj.Type == JTokenType.Object || jObj.Type == JTokenType.Array) && jObj.Parent.Type == JTokenType.Property)
                                            {
                                                jObj.Parent.Remove();
                                                bLoop = true;
                                                continue;
                                            }

                                            jObj.Remove();
                                            bLoop = true;
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.WriteLine("Error GetFull_1: " + ex.Message.ToString());
                                            if(i <= 100)
                                                bLoop = true;
                                            i++;
                                        }
                                    }
                                }

                                /*foreach (var jObj in (oTok.Parent.Descendants().Where(t => t.Type == (JTokenType.Object) && t.HasValues == false).Reverse().ToList()))
                                {
                                    try
                                    {
                                         if (jObj.Parent.Count == 1 && jObj.Parent.Type == JTokenType.Array)
                                        {
                                            jObj.Parent.Parent.Remove();
                                            continue;
                                        }
                                        if (jObj.Parent.Parent.Count == 1 && jObj.Parent.Type == JTokenType.Property)
                                        {
                                            jObj.Parent.Parent.Remove();
                                            continue;
                                        }
                                        if (jObj.Parent.Count == 1 && jObj.Parent.Type == JTokenType.Property)
                                        {
                                            jObj.Parent.Remove();
                                            continue;
                                        }
                                        else
                                        {
                                            jObj.Remove();
                                        }
                                    }
                                    catch(Exception ex)
                                    {
                                        Debug.WriteLine("Error GetFull_1: " + ex.Message.ToString());
                                    }
                                }*/
                            }
                            else
                            {
                                sObj.ToString();
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("Error GetFull_2: " + ex.Message.ToString());
                        }
                    }

                    //Remove ##hash
                    foreach (var oTok in oInv.Descendants().Where(t => t.Type == JTokenType.Property && ((JProperty)t).Name.StartsWith("##hash")).ToList())
                    {
                        try
                        {
                            if (oInv.SelectToken(oTok.Path).Parent.Parent.Children().Count() == 1)
                                oInv.SelectToken(oTok.Path).Parent.Parent.Remove();
                            else
                                oInv.SelectToken(oTok.Path).Parent.Remove();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("Error GetFull_3: " + ex.Message.ToString());
                        }
                    }

                    try
                    {
                        //Remove NULL values
                        foreach (var oTok in (oInv.Descendants().Where(t => t.Type == (JTokenType.Object) && t.HasValues == false).Reverse().ToList()))
                        {
                            try
                            {
                                if (oTok.Parent.Count == 1)
                                {
                                    oTok.Parent.Remove();
                                }

                                if (oTok.HasValues)
                                    oTok.Remove();
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("Error GetFull_4: " + ex.Message.ToString());
                            }
                        }
                    }
                    catch { }
                    JSort(oInv, true);

                    if (Index == -1)
                    {
                        WriteHashAsync(DeviceID, oInv.ToString(), "_full");
                    }

                    /*var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(60)); //cache full for 60s
                    _cache.Set("FULL-" + DeviceID + "-" + Index.ToString(), oInv, cacheEntryOptions);*/

                    return oInv;
                }

            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error GetFull_5: " + ex.Message.ToString());
            }

            return new JObject();
        }

        public static JObject GetRawId(string DeviceID, int Index = -1)
        {
            JObject jResult = new JObject();

            try
            {
                Blockchain oChain;

                block lBlock = null;

                if (Index == -1)
                {
                    oChain = GetChain(DeviceID);
                    lBlock = oChain.GetLastBlock();

                }
                else
                {
                    oChain = GetChain(DeviceID);
                    lBlock = oChain.GetBlock(Index);
                }


                int index = lBlock.index;
                DateTime dInvDate = new DateTime(lBlock.timestamp);
                string sRawId = lBlock.data;

                jResult.Add(new JProperty("_index", index));
                jResult.Add(new JProperty("_inventoryDate", dInvDate));
                jResult.Add(new JProperty("_hash", sRawId));
            }
            catch { }

            return jResult;
        }

        public static JObject GetHistory(string DeviceID)
        {
            JObject jResult = new JObject();
            try
            {

                string sChain = ReadHash(DeviceID, "chain");
                var oChain = JsonConvert.DeserializeObject<Blockchain>(sChain);
                foreach (block oBlock in oChain.Chain.Where(t => t.blocktype != "root"))
                {
                    try
                    {
                        JArray jArr = new JArray();
                        JObject jTemp = new JObject();
                        jTemp.Add("index", oBlock.index);
                        jTemp.Add("timestamp", new DateTime(oBlock.timestamp).ToUniversalTime());
                        jTemp.Add("data", oBlock.data);
                        jArr.Add(jTemp);
                        jResult.Add(oBlock.index.ToString(), jArr);
                    }
                    catch { }
                }

            }
            catch { }

            JSort(jResult);
            return jResult;
        }

        public static JObject GetRaw(string RawID, string path = "")
        {
            JObject jResult = new JObject();
            try
            {
                JObject oInv = JObject.Parse(RawID);

                if (!path.Contains("*") && !path.Contains("..")) //Skip if using wildcards; we have to get the full content to filter
                {
                    if (!string.IsNullOrEmpty(path))
                    {
                        foreach (string spath in path.Split(';'))
                        {
                            string sLookupPath = spath.Replace(".##hash", "");
                            var aPath = spath.Split('.');
                            if (aPath.Length > 1) //at least 2 segments...
                            {
                                sLookupPath = sLookupPath.Substring(0, sLookupPath.LastIndexOf('.'));
                            }
                            //foreach (JProperty oTok in oInv.Descendants().Where(t => t.Path.StartsWith(spath.Split('.')[0]) && t.Type == JTokenType.Property && ((JProperty)t).Name.StartsWith("##hash")).ToList())
                            foreach (JProperty oTok in oInv.Descendants().Where(t => t.Path.StartsWith(sLookupPath) && t.Type == JTokenType.Property && ((JProperty)t).Name.StartsWith("##hash")).ToList())
                            {

                                string sH = oTok.Value.ToString();
                                string sRoot = oTok.Path.Split('.').Reverse().ToList()[1]; //second last as last is ##hash
                                                                                           //string sRoot = oTok.Path.Split('.')[0].Split('[')[0]; //AppMgmtDigest.Application.DisplayInfo.Info.##hash
                                string sObj = ReadHash(sH, sRoot);
                                if (!string.IsNullOrEmpty(sObj))
                                {
                                    var jStatic = JObject.Parse(sObj);
                                    oTok.Parent.Merge(jStatic);
                                    oTok.Remove();
                                }
                            }
                        }
                    }
                    else
                    {
                        foreach (JProperty oTok in oInv.Descendants().Where(t => t.Type == JTokenType.Property && ((JProperty)t).Name.StartsWith("##hash")).ToList())
                        {
                            string sH = oTok.Value.ToString();
                            string sRoot = oTok.Path.Split('.')[0].Split('[')[0];
                            string sObj = ReadHash(sH, sRoot);
                            if (!string.IsNullOrEmpty(sObj))
                            {
                                var jStatic = JObject.Parse(sObj);
                                oTok.Parent.Merge(jStatic);
                                oTok.Remove();
                            }
                        }
                    }

                    JSort(oInv);
                }

                return oInv;
            }
            catch (Exception ex)
            {
                ex.Message.ToString();
            }

            return jResult;
        }

        public static JObject GetDiff(string DeviceId, int IndexLeft, int mode = -1, int IndexRight = -1)
        {
            try
            {
                var right = GetFull(DeviceId, IndexRight);

                if (IndexLeft == 0)
                {
                    IndexLeft = ((int)right["_index"]) - 1;
                }
                var left = GetFull(DeviceId, IndexLeft);

                foreach (var oTok in right.Descendants().Where(t => t.Type == JTokenType.Property && ((JProperty)t).Name.StartsWith("@")).ToList())
                {
                    oTok.Remove();
                }
                foreach (var oTok in left.Descendants().Where(t => t.Type == JTokenType.Property && ((JProperty)t).Name.StartsWith("@")).ToList())
                {
                    oTok.Remove();
                }

                //Remove NULL values
                foreach (var oTok in left.Descendants().Where(t => t.Parent.Type == (JTokenType.Property) && t.Type == JTokenType.Null).ToList())
                {
                    oTok.Parent.Remove();
                }
                foreach (var oTok in right.Descendants().Where(t => t.Parent.Type == (JTokenType.Property) && t.Type == JTokenType.Null).ToList())
                {
                    oTok.Parent.Remove();
                }

                JSort(right);
                JSort(left);

                if (mode <= 1)
                {
                    var optipons = new JsonDiffPatchDotNet.Options();
                    if (mode == 0)
                    {
                        optipons.ArrayDiff = JsonDiffPatchDotNet.ArrayDiffMode.Simple;
                        optipons.TextDiff = JsonDiffPatchDotNet.TextDiffMode.Simple;
                    }
                    if (mode == 1)
                    {
                        optipons.ArrayDiff = JsonDiffPatchDotNet.ArrayDiffMode.Efficient;
                        optipons.TextDiff = JsonDiffPatchDotNet.TextDiffMode.Efficient;
                    }

                    var jpf = new JsonDiffPatchDotNet.JsonDiffPatch(optipons);

                    var oDiff = jpf.Diff(left, right);

                    if (oDiff == null)
                        return new JObject();

                    GC.Collect();
                    return JObject.Parse(oDiff.ToString());
                }
            }
            catch { }

            return new JObject();
        }

        /*      /// <summary>
                /// Convert Topic /Key/Key/[0]/val to JSON Path format Key.Key[0].val
                /// </summary>
                /// <param name="Topic"></param>
                /// <returns></returns>
                public static string Topic2JPath(string Topic)
                {
                    try
                    {
                        string sPath = "";
                        List<string> lItems = Topic.Split('/').ToList();
                        for (int i = 0; i < lItems.Count(); i++)
                        {
                            bool bArray = false;
                            int iVal = -1;
                            if (i + 1 < lItems.Count())
                            {
                                if (lItems[i + 1].Contains("[") && int.TryParse(lItems[i + 1].TrimStart('[').TrimEnd(']'), out iVal))
                                    bArray = true;
                                else
                                    bArray = false;
                            }
                            if (!bArray)
                                sPath += lItems[i] + ".";
                            else
                            {
                                sPath += lItems[i] + "[" + iVal.ToString() + "].";
                                i++;
                            }
                        }

                        return sPath.TrimEnd('.');
                    }
                    catch { }

                    return "";
                }
                */

        class TopicComparer : IComparer<string>
        {
            public int Compare(string x, string y)
            {
                if (x == y)
                    return 0;

                List<string> lx = x.Split('/').ToList();
                List<string> ly = y.Split('/').ToList();

                if (lx.Count > ly.Count)
                    return 1; //y is smaller
                if (lx.Count < ly.Count)
                    return -1; //x is smaller

                int i = 0;
                foreach (string s in lx)
                {
                    if (s == ly[i])
                    {
                        i++;
                        continue;
                    }

                    if (s.StartsWith("[") && s.EndsWith("]") && ly[i].StartsWith("[") && ly[i].EndsWith("]"))
                    {
                        try
                        {
                            int ix = int.Parse(s.TrimStart('[').TrimEnd(']'));
                            int iy = int.Parse(ly[i].TrimStart('[').TrimEnd(']'));
                            if (ix == iy)
                                return 0;
                            if (ix < iy)
                                return -1;
                            else
                                return 1;
                        }
                        catch { }
                    }

                    int iRes = string.Compare(s, ly[i]);

                    return iRes;
                }

                return string.Compare(x, y);
            }
        }

        /// <summary>
        /// Get a list of KeyID that contains a searchkey (freeText)
        /// </summary>
        /// <param name="searchkey"></param>
        /// <param name="KeyID"></param>
        /// <returns></returns>
        public static List<string> Search(string searchkey, string KeyID = "#id")
        {
            if (string.IsNullOrEmpty(searchkey))
                return new List<string>();

            if (string.IsNullOrEmpty(KeyID))
                KeyID = "#id";

            if (KeyID.Contains(','))
                KeyID = KeyID.Split(',')[0];

            List<string> lNames = new List<string>();

            lNames = FindLatestAsync(System.Net.WebUtility.UrlDecode(searchkey), "#" + KeyID.TrimStart('?').TrimStart('#')).Result;
            lNames.Sort();
            return lNames.Union(lNames).ToList();
        }

        public static JArray Query(string paths, string select, string exclude)
        {
            paths = System.Net.WebUtility.UrlDecode(paths);
            select = System.Net.WebUtility.UrlDecode(select);
            exclude = System.Net.WebUtility.UrlDecode(exclude);
            List<string> lExclude = new List<string>();

            if (!string.IsNullOrEmpty(exclude))
            {
                lExclude = exclude.Split(";").ToList();
            }

            if (string.IsNullOrEmpty(select))
                select = "#id"; //,#Name,_inventoryDate

            //int i = 0;
            DateTime dStart = DateTime.Now;
            //JObject lRes = new JObject();
            JArray aRes = new JArray();
            List<string> lLatestHash = GetAllChainsAsync().Result;
            foreach (string sHash in lLatestHash)
            {
                bool foundData = false;
                try
                {
                    var jObj = GetFull(sHash);

                    JObject oRes = new JObject();
                    foreach (string sAttrib in select.Split(';'))
                    {
                        //var jVal = jObj[sAttrib];
                        var jVal = jObj.SelectToken(sAttrib);

                        if (jVal != null)
                        {
                            oRes.Add(sAttrib.Trim(), jVal);
                        }
                    }
                    if (!string.IsNullOrEmpty(paths)) //only return defined objects, if empty all object will return
                    {
                        //Generate list of excluded paths
                        List<string> sExclPath = new List<string>();
                        foreach (string sExclude in lExclude)
                        {
                            foreach (var oRem in jObj.SelectTokens(sExclude, false).ToList())
                            {
                                sExclPath.Add(oRem.Path);
                            }
                        }

                        foreach (string path in paths.Split(';'))
                        {
                            try
                            {
                                var oToks = jObj.SelectTokens(path.Trim(), false);

                                if (oToks.Count() == 0)
                                {
                                    if (!foundData)
                                    {
                                        oRes = new JObject(); //remove selected attributes as we do not have any vresults from jsonpath
                                        continue;
                                    }
                                }

                                foreach (JToken oTok in oToks)
                                {
                                    try
                                    {
                                        if (oTok.Type == JTokenType.Object)
                                        {
                                            oRes.Merge(oTok);
                                            //oRes.Add(jObj[select.Split(',')[0]].ToString(), oTok);
                                            continue;
                                        }
                                        if (oTok.Type == JTokenType.Array)
                                        {
                                            oRes.Add(new JProperty(path, oTok));
                                        }
                                        if (oTok.Type == JTokenType.Property)
                                            oRes.Add(oTok.Parent);

                                        if (oTok.Type == JTokenType.String ||
                                            oTok.Type == JTokenType.Integer ||
                                            oTok.Type == JTokenType.Date ||
                                            oTok.Type == JTokenType.Boolean ||
                                            oTok.Type == JTokenType.Float ||
                                            oTok.Type == JTokenType.Guid ||
                                            oTok.Type == JTokenType.TimeSpan)
                                        {
                                            //check if path is excluded
                                            if (!sExclPath.Contains(oTok.Path))
                                                oRes.Add(oTok.Path, oTok);
                                        }

                                        if (oTok.Type == JTokenType.Date)
                                            oRes.Add(oTok.Parent);

                                        foundData = true;
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine("Error Query_5: " + ex.Message.ToString());
                                    }

                                }

                                /*if (oToks.Count() == 0)
                                    oRes = new JObject(); */
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("Error Query_5: " + ex.Message.ToString());
                            }
                        }
                    }

                    if (oRes.HasValues)
                    {
                        //Remove excluded Properties
                        foreach (string sExclude in lExclude)
                        {
                            foreach (var oRem in oRes.SelectTokens(sExclude, false).ToList())
                            {
                                oRem.Parent.Remove();
                            }
                        }

                        aRes.Add(oRes);
                        //lRes.Add(i.ToString(), oRes);
                        //i++;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error Query_5: " + ex.Message.ToString());
                }
            }

            GC.Collect();
            return aRes;

        }

        public static JArray QueryAll(string paths, string select, string exclude)
        {
            paths = System.Net.WebUtility.UrlDecode(paths);
            select = System.Net.WebUtility.UrlDecode(select);
            exclude = System.Net.WebUtility.UrlDecode(exclude);
            List<string> lExclude = new List<string>();

            if (!string.IsNullOrEmpty(exclude))
            {
                lExclude = exclude.Split(";").ToList();
            }

            if (string.IsNullOrEmpty(select))
                select = "#id"; //,#Name,_inventoryDate

            //JObject lRes = new JObject();
            JArray aRes = new JArray();
            List<string> lHashes = new List<string>();
            try
            {
                if (UseRedis)
                {
                    foreach (var oObj in srv.Keys(4, "*"))
                    {
                        bool foundData = false;

                        JObject jObj = GetRaw(ReadHash(oObj, "assets"), paths);

                        if (paths.Contains("*") || paths.Contains(".."))
                        {
                            try
                            {
                                jObj = GetFull(jObj["#id"].Value<string>(), jObj["_index"].Value<int>());
                            }
                            catch { }
                        }

                        //JObject jObj = GetRaw(cache4.StringGet(oObj), paths);
                        JObject oRes = new JObject();

                        foreach (string sAttrib in select.Split(';'))
                        {
                            //var jVal = jObj[sAttrib];
                            var jVal = jObj.SelectToken(sAttrib);

                            if (jVal != null)
                            {
                                oRes.Add(sAttrib.Trim(), jVal);
                            }
                        }

                        if (!string.IsNullOrEmpty(paths)) //only return defined objects, if empty all object will return
                        {
                            //Generate list of excluded paths
                            List<string> sExclPath = new List<string>();
                            foreach (string sExclude in lExclude)
                            {
                                foreach (var oRem in jObj.SelectTokens(sExclude, false).ToList())
                                {
                                    sExclPath.Add(oRem.Path);
                                }
                            }

                            foreach (string path in paths.Split(';'))
                            {
                                try
                                {
                                    var oToks = jObj.SelectTokens(path.Trim(), false);

                                    if (oToks.Count() == 0)
                                    {
                                        if (!foundData)
                                        {
                                            oRes = new JObject(); //remove selected attributes as we do not have any vresults from jsonpath
                                            break;
                                        }
                                    }

                                    foreach (JToken oTok in oToks)
                                    {
                                        try
                                        {
                                            if (oTok.Type == JTokenType.Object)
                                            {
                                                oRes.Merge(oTok);
                                                //oRes.Add(jObj[select.Split(',')[0]].ToString(), oTok);
                                                continue;
                                            }
                                            if (oTok.Type == JTokenType.Array)
                                            {
                                                oRes.Add(new JProperty(path, oTok));
                                            }
                                            if (oTok.Type == JTokenType.Property)
                                                oRes.Add(oTok.Parent);

                                            if (oTok.Type == JTokenType.String ||
                                                oTok.Type == JTokenType.Integer ||
                                                oTok.Type == JTokenType.Date ||
                                                oTok.Type == JTokenType.Boolean ||
                                                oTok.Type == JTokenType.Float ||
                                                oTok.Type == JTokenType.Guid ||
                                                oTok.Type == JTokenType.TimeSpan)
                                            {
                                                //check if path is excluded
                                                if (!sExclPath.Contains(oTok.Path))
                                                    oRes.Add(oTok.Path, oTok);
                                            }

                                            if (oTok.Type == JTokenType.Date)
                                                oRes.Add(oTok.Parent);

                                            foundData = true;
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.WriteLine("Error QueryAll_1: " + ex.Message.ToString());
                                        }

                                    }

                                    /*if (oToks.Count() == 0)
                                        oRes = new JObject(); */
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine("Error Query_5: " + ex.Message.ToString());
                                }
                            }
                        }

                        if (oRes.HasValues)
                        {
                            string sHa = CalculateHash(oRes.ToString(Formatting.None));
                            if (!lHashes.Contains(sHa))
                            {
                                aRes.Add(oRes);
                                lHashes.Add(sHa);
                            }

                            //Remove excluded Properties
                            foreach (string sExclude in lExclude)
                            {
                                foreach (var oRem in oRes.SelectTokens(sExclude, false).ToList())
                                {
                                    oRem.Parent.Remove();
                                }
                            }
                        }
                    }
                    GC.Collect();
                    return aRes;
                }

                if (UseFileStore)
                {
                    foreach (var oFile in new DirectoryInfo("wwwroot/Assets").GetFiles("*.json"))
                    {
                        bool foundData = false;
                        JObject jObj = GetRaw(File.ReadAllText(oFile.FullName), paths);

                        if (paths.Contains("*") || paths.Contains(".."))
                        {
                            try
                            {
                                jObj = GetFull(jObj["#id"].Value<string>(), jObj["_index"].Value<int>());
                            }
                            catch { }
                        }

                        JObject oRes = new JObject();

                        foreach (string sAttrib in select.Split(';'))
                        {
                            //var jVal = jObj[sAttrib];
                            var jVal = jObj.SelectToken(sAttrib);

                            if (jVal != null)
                            {
                                oRes.Add(sAttrib.Trim(), jVal);
                            }
                        }

                        if (!string.IsNullOrEmpty(paths)) //only return defined objects, if empty all object will return
                        {
                            //Generate list of excluded paths
                            List<string> sExclPath = new List<string>();
                            foreach (string sExclude in lExclude)
                            {
                                foreach (var oRem in jObj.SelectTokens(sExclude, false).ToList())
                                {
                                    sExclPath.Add(oRem.Path);
                                }
                            }

                            foreach (string path in paths.Split(';'))
                            {
                                try
                                {
                                    var oToks = jObj.SelectTokens(path.Trim(), false);

                                    if (oToks.Count() == 0)
                                    {
                                        if (!foundData)
                                        {
                                            oRes = new JObject(); //remove selected attributes as we do not have any vresults from jsonpath
                                            continue;
                                        }
                                    }

                                    foreach (JToken oTok in oToks)
                                    {
                                        try
                                        {
                                            if (oTok.Type == JTokenType.Object)
                                            {
                                                oRes.Merge(oTok);
                                                //oRes.Add(jObj[select.Split(',')[0]].ToString(), oTok);
                                                continue;
                                            }
                                            if (oTok.Type == JTokenType.Array)
                                            {
                                                oRes.Add(new JProperty(path, oTok));
                                            }
                                            if (oTok.Type == JTokenType.Property)
                                                oRes.Add(oTok.Parent);

                                            if (oTok.Type == JTokenType.String)
                                            {
                                                //check if path is excluded
                                                if (!sExclPath.Contains(oTok.Path))
                                                    oRes.Add(oTok.Path, oTok.ToString());
                                            }

                                            if (oTok.Type == JTokenType.Date)
                                                oRes.Add(oTok.Parent);

                                            foundData = true;
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.WriteLine("Error Query_5: " + ex.Message.ToString());
                                        }

                                    }

                                    /*if (oToks.Count() == 0)
                                        oRes = new JObject(); */
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine("Error Query_5: " + ex.Message.ToString());
                                }
                            }
                        }

                        if (oRes.HasValues)
                        {
                            string sHa = CalculateHash(oRes.ToString(Formatting.None));
                            if (!lHashes.Contains(sHa))
                            {
                                aRes.Add(oRes);
                                lHashes.Add(sHa);
                            }

                            //Remove excluded Properties
                            foreach (string sExclude in lExclude)
                            {
                                foreach (var oRem in oRes.SelectTokens(sExclude, false).ToList())
                                {
                                    oRem.Parent.Remove();
                                    //oRes.Remove(oRem.Path);
                                }
                            }
                        }
                    }
                    GC.Collect();
                    return aRes;
                }
            }
            catch { }

            return new JArray();
        }

        public enum ChangeType { New, Update };

        public class Change
        {
            public ChangeType changeType; 
            public DateTime lastChange;
            public int index;
            public string id;
        }

        public static JArray GetChanges(TimeSpan age, int changeType = -1)
        {
            age.ToString();
            List<Change> lRes = new List<Change>();
            foreach (var sID in GetAllChainsAsync().Result)
            {
                Change oRes = new Change();
                oRes.id = sID;
                var jObj = JObject.Parse(ReadHash(sID, "chain"));
                oRes.lastChange = new DateTime(jObj["Chain"].Last["timestamp"].Value<long>());
                if(DateTime.Now.Subtract(oRes.lastChange) > age)
                {
                    continue;
                }
                oRes.index = jObj["Chain"].Last["index"].Value<int>();
                if (oRes.index > 1)
                    oRes.changeType = ChangeType.Update;
                else
                    oRes.changeType = ChangeType.New;

                if(changeType >= 0)
                {
                    if(((int)oRes.changeType) != changeType)
                        continue;
                }

                lRes.Add(oRes);
            }

            return JArray.Parse(JsonConvert.SerializeObject(lRes.OrderBy(t=>t.id).ToList(), Formatting.None));
        }

        /// <summary>
        /// Get all BlockChains
        /// </summary>
        /// <returns>List of BlockChain (Device) ID's</returns>
        public static async Task<List<string>> GetAllChainsAsync()
        {
            return await Task.Run(() =>
            {
                List<string> lResult = new List<string>();
                try
                {
                    if (UseRedis)
                    {
                        foreach (var oObj in srv.Keys(3, "*"))
                        {
                            lResult.Add(oObj.ToString());
                        }

                        return lResult;
                    }

                    if (UseFileStore)
                    {
                        foreach (var oFile in new DirectoryInfo("wwwroot/Chain").GetFiles("*.json"))
                        {
                            lResult.Add(System.IO.Path.GetFileNameWithoutExtension(oFile.Name));
                        }
                        return lResult;
                    }
                }
                catch { }

                return lResult;
            });

        }

        /// <summary>
        /// Get List of latest Blocks of each Chain
        /// </summary>
        /// <returns></returns>
        public static async Task<List<string>> GetLatestBlocksAsync()
        {
            return await Task.Run(() =>
            {
                List<string> lResult = new List<string>();
                try
                {
                    var cache5 = RedisConnectorHelper.Connection.GetDatabase(5);
                    string sBlocks = cache5.StringGet("latestBlocks");
                    if (string.IsNullOrEmpty(sBlocks))
                    {
                        try
                        {
                            if (UseRedis)
                            {
                                //var cache3 = RedisConnectorHelper.Connection.GetDatabase(3);

                                foreach (var sID in GetAllChainsAsync().Result)
                                {
                                    try
                                    {
                                        var jObj = JObject.Parse(cache3.StringGet(sID));
                                        lResult.Add(jObj["Chain"].Last["data"].ToString());
                                    }
                                    catch { }
                                }
                            }
                        }
                        catch { }

                        cache5.StringSet("latestBlocks", String.Join(";", lResult), new TimeSpan(0, 5, 0)); // Store for 5min
                    }
                    else
                        return sBlocks.Split(';').ToList();

                }
                catch { }
                return lResult;
            });
        }

        public static async Task<List<string>> FindHashOnContentAsync(string searchstring)
        {
            return await Task.Run(() =>
            {
                List<string> lResult = new List<string>();
                try
                {
                    if (UseRedis)
                    {
                        foreach (var oObj in srv.Keys(2, "*"))
                        {
                            string sVal = cache2.StringGet(oObj);
                            if (sVal.Contains(searchstring))
                            {
                                lResult.Add(oObj);
                            }
                        }
                    }
                }
                catch { }

                return lResult;
            });
        }

        public static List<string> FindLatestRawWithHash(List<string> HashList, string KeyID = "#id")
        {
            List<string> lResult = new List<string>();
            try
            {
                if (UseRedis)
                {
                    List<string> latestBlocks = GetLatestBlocksAsync().Result;
                    foreach (string sRawID in latestBlocks)
                    {
                        try
                        {
                            var sRaw = cache4.StringGet(sRawID).ToString();
                            foreach (string Hash in HashList)
                            {
                                if (sRaw.Contains(Hash))
                                    lResult.Add(JObject.Parse(sRaw)[KeyID].ToString());
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }

            return lResult;
        }

        public static async Task<List<string>> FindLatestAsync(string searchstring, string KeyID = "#id")
        {
            List<string> lResult = new List<string>();
            try
            {
                if (UseRedis)
                {
                    var tFind = await FindHashOnContentAsync(searchstring);

                    tFind.AsParallel().ForAll(t =>
                    {
                        foreach (string sName in FindLatestRawWithHash(new List<string>() { t }, KeyID))
                        {
                            lResult.Add(sName);
                        }
                    });
                }
            }
            catch { }

            return lResult;
        }

        public static bool Export(string URL, string RemoveObjects)
        {
            int iCount = 0;
            bool bResult = true;

            try
            {
                oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                if (UseRedis)
                {
                    foreach (var sID in GetAllChainsAsync().Result) //Keep it single threaded, to prevent Redis Timeouts
                    {
                        try
                        {

                            var jObj = JObject.Parse(ReadHash(sID, "chain"));
                            //var jObj = JObject.Parse(cache3.StringGet(sID));

                            foreach (var sBlock in jObj.SelectTokens("Chain[*].data"))
                            {
                                try
                                {
                                    string sBlockID = sBlock.Value<string>();
                                    if (!string.IsNullOrEmpty(sBlockID))
                                    {
                                        var jBlock = GetRaw(ReadHash(sBlockID, "assets"));
                                        //var jBlock = GetRaw(cache4.StringGet(sBlockID));
                                        jBlock.Remove("#id"); //old Version of jainDB 
                                        //jBlock.Remove("_date");
                                        jBlock.Remove("_index");

                                        //Remove Objects from Chain
                                        foreach (string sRemObj in RemoveObjects.Split(';'))
                                        {
                                            try
                                            {
                                                foreach (var oTok in jBlock.Descendants().Where(t => t.Path == sRemObj).ToList())
                                                {
                                                    oTok.Remove();
                                                    break;
                                                }
                                            }
                                            catch { }
                                        }

                                        //jBlock.Add("#id", sID);

                                        string sResult = UploadToREST(URL + "/upload/" + sID, jBlock.ToString(Formatting.None));
                                        //System.Threading.Thread.Sleep(50);
                                        if (!string.IsNullOrEmpty(sResult.Trim('"')))
                                        {
                                            Console.WriteLine("Exported: " + sResult);
                                            iCount++;
                                        }
                                        else
                                        {
                                            jBlock.ToString();
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine("Error: " + ex.Message);
                                    bResult = false;
                                }
                            }
                            Thread.Sleep(100);
                        }
                        catch { bResult = false; }
                    }
                }

                if (UseFileStore)
                {
                    foreach (var sID in GetAllChainsAsync().Result)
                    {
                        try
                        {
                            var jObj = JObject.Parse(ReadHash(sID, "Chain"));
                            foreach (var sBlock in jObj.SelectTokens("Chain[*].data"))
                            {
                                try
                                {
                                    string sBlockID = sBlock.Value<string>();
                                    if (!string.IsNullOrEmpty(sBlockID))
                                    {
                                        var jBlock = GetRaw(ReadHash(sBlockID, "Assets"));
                                        jBlock.Remove("#id"); //old Version of jainDB 
                                        //jBlock.Remove("_date");
                                        jBlock.Remove("_index");
                                        //jBlock.Add("#id", sID);

                                        //Remove Objects from Chain
                                        foreach (string sRemObj in RemoveObjects.Split(';'))
                                        {
                                            try
                                            {
                                                foreach (var oTok in jBlock.Descendants().Where(t => t.Path == sRemObj).ToList())
                                                {
                                                    oTok.Remove();
                                                    break;
                                                }
                                            }
                                            catch { }
                                        }

                                        string sResult = UploadToREST(URL + "/upload/" + sID, jBlock.ToString(Formatting.None));
                                        //System.Threading.Thread.Sleep(50);
                                        if (!string.IsNullOrEmpty(sResult.Trim('"')))
                                        {
                                            Console.WriteLine("Exported: " + sResult);
                                            iCount++;
                                        }
                                        else
                                        {
                                            jBlock.ToString();
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine("Error: " + ex.Message);
                                    bResult = false;
                                }
                            }
                            System.Threading.Thread.Sleep(100);
                        }
                        catch { bResult = false; }
                    }
                }
            }
            catch { bResult = false; }
            Console.WriteLine("Done... " + iCount.ToString() + " Blocks exported");
            return bResult;
        }

        public static string UploadToREST(string URL, string content)
        {
            try
            {
                //oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                HttpContent oCont = new StringContent(content);

                var response = oClient.PostAsync(URL, oCont);
                response.Wait(15000);
                if (response.IsCompleted)
                {
                    return response.Result.Content.ReadAsStringAsync().Result.ToString();
                }


            }
            catch (Exception ex)
            {
                ex.Message.ToString();
            }

            return "";

        }
        /*
        public static string GetFull(Blockchain oChain, int Index = -1)
        {
            try
            {
                block lBlock = null;
                if (Index == -1)
                {
                    lBlock = oChain.GetLastBlock();
                }
                else
                {
                    lBlock = oChain.GetBlock(Index);
                }

                int index = lBlock.index;
                DateTime dInvDate = new DateTime(lBlock.timestamp);
                string InvHash = lBlock.data;
                string sData = ReadHash(InvHash, "Assets");
                if (!string.IsNullOrEmpty(sData))
                {
                    JObject oInv = JObject.Parse(sData);
                    oInv.Add(new JProperty("_index", index));
                    oInv.Add(new JProperty("_inventoryDate", dInvDate));
                    oInv.Add(new JProperty("_hash", InvHash));
                    JSort(oInv);

                    //Load hashed values
                    foreach (JProperty oTok in oInv.Descendants().Where(t => t.Type == JTokenType.Property && ((JProperty)t).Name.StartsWith("##hash")).ToList())
                    {
                        string sH = oTok.Value.ToString();
                        string sRoot = oTok.Path.Split('.')[0].Split('[')[0];
                        string sObj = ReadHash(sH, sRoot);
                        if (!string.IsNullOrEmpty(sObj))
                        {
                            var jStatic = JObject.Parse(sObj);
                            oTok.Parent.Merge(jStatic);
                            oTok.Remove();
                        }
                    }

                    return oInv.ToString();
                }
            }
            catch { }

            return "";
        }
        public static string ByteArrayToString(byte[] input)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < input.Length; i++)
            {
                sb.Append(input[i].ToString("X2"));
            }
            return sb.ToString();
        }

        public static byte[] StringToByteArray(String hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }
        public static string LookupID(string query)
        {
            try
            {
                if (UseRedis)
                {
                    var cache1 = RedisConnectorHelper.Connection.GetDatabase(1);
                    return cache1.StringGet(query.TrimStart('?').Split('&')[0].Replace("=", "/"));
                }
            }
            catch { }

            return "";
        }
        
        public static async Task<List<string>> FindLatestPathAsync(string path)
        {
            List<string> lResult = new List<string>();
            try
            {
                if (UseRedis)
                {
                    var tFind = await FindHashOnContentAsync(path);

                    tFind.AsParallel().ForAll(t =>
                    {
                        foreach (string sName in FindLatestRawWithHash(new List<string>() { t }, "#Name"))
                        {
                            lResult.Add(sName);
                        }
                    });
                }
            }
            catch { }

            return lResult;
        }

        public static string sTopic2Json(string Topic = "*")
        {
            var cache = RedisConnectorHelper.Connection.GetDatabase();
            var server = RedisConnectorHelper.Connection.GetServer("localhost", 6379);

            List<string> lRes = new List<string>();
            var keys = server.Keys(pattern: Topic);
            //var sResult = cache.StringGet("BIOS/*");
            foreach (var key in keys)
            {
                key.ToString();
                var sVal = cache.StringGet(key);
                lRes.Add(key.ToString() + ";" + sVal);
            }

            return Topic2Json(lRes.ToArray());
            try
            {
                string[] aFile = File.ReadAllLines("wwwroot\\test.json");
                return Topic2Json(aFile);
            }
            catch { }

            return "";
        }

        public static string Topic2Json(string Topic)
        {
            return Topic2Json(Topic.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));
        }
        public static string Topic2Json(string[] Topic)
        {
            try
            {
                List<string> lSource = Topic.OrderBy(t => t, new TopicComparer()).ToList();

                JObject ORes = new JObject();
                foreach (string sLine in lSource)
                {
                    string sTopic = sLine.Split(';')[0];
                    string sValue = sLine.Split(';')[1];
                    bool bMerge = true;

                    string sPath = "";

                    //Convert Topic to JPath
                    sPath = Topic2JPath(sTopic);


                    JObject O = new JObject();
                    string sFullPath = sPath;
                    foreach (string sItem in sPath.Split('.').Reverse())
                    {
                        bool bArray = false;
                        if (sItem.EndsWith("]"))
                            bArray = true;

                        if (O.Count == 0)
                        {
                            if (!bArray)
                            {
                                O.Add(sItem, sValue);
                            }
                            else
                            {
                                JArray A = new JArray();
                                A.Add(sValue);
                                O.Add(sItem.Split('[')[0], A);
                                //JObject OA = new JObject();
                                //OA.Add(A);
                                //O = OA;
                            }
                            sFullPath = sFullPath.Substring(0, (sFullPath.Length - sItem.Length)).TrimEnd('.');
                        }
                        else
                        {
                            if (!bArray)
                            {
                                if (O.SelectToken(sFullPath) == null)
                                {
                                    JObject N = new JObject();
                                    N.Add(sItem, O);
                                    O = N;
                                    sFullPath = sFullPath.Substring(0, (sFullPath.Length - sItem.Length)).TrimEnd('.');
                                }
                            }
                            else
                            {
                                if (ORes.SelectToken(sFullPath) == null)
                                {
                                    JArray A = new JArray();
                                    A.Add(O);

                                    JObject N = new JObject();
                                    N.Add(sItem.Split('[')[0], A);

                                    O = N;
                                    sFullPath = sFullPath.Substring(0, (sFullPath.Length - sItem.Length)).TrimEnd('.');
                                }
                                else
                                {
                                    var oT = ORes.SelectToken(sFullPath);
                                    ((JObject)oT).Add(O.First);
                                    bMerge = false;
                                }
                            }
                        }

                    }

                    if (bMerge)
                    {
                        ORes.Merge(O);
                    }
                    else
                    {
                        bMerge = true;
                    }
                }

                string sJ = ORes.ToString();
                return sJ;
            }
            catch
            { }

            return "";
        }

        public static List<string> Json2Topic(JObject JSON, string ID = "")
        {
            if (!string.IsNullOrEmpty(ID))
            {
                if (!ID.EndsWith("/"))
                    ID += "/";
            }
            List<string> sResult = new List<string>();

            foreach (var x in JSON)
            {
                try
                {
                    string name = x.Key;
                    if (x.Value.Count() > 0)
                    {
                        if (x.Value.Type != JTokenType.Array)
                        {
                            if (x.Value.Type == JTokenType.Object)
                                sResult.AddRange(Json2Topic(x.Value as JObject, ID));
                            else
                            {
                                foreach (var oVal in x.Value)
                                {
                                    if (!string.IsNullOrEmpty(oVal.ToString()))
                                    {
                                        sResult.Add(ID + JSON.Path + "/" + name + ";" + oVal.ToString());
                                    }
                                }
                            }
                        }
                        else
                        {
                            int i = 0;
                            foreach (var oVal in x.Value)
                            {
                                if (oVal.Type == JTokenType.Object)
                                {
                                    sResult.AddRange(Json2Topic(oVal as JObject, ID));
                                }
                                else
                                {
                                    if (!string.IsNullOrEmpty(oVal.ToString()))
                                    {
                                        sResult.Add(ID + JSON.Path + "/" + name + "/[" + i + "];" + oVal.ToString());
                                        i++;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(JSON.Path))
                        {
                            if (!string.IsNullOrEmpty(x.Value.ToString()))
                            {
                                sResult.Add(ID + JSON.Path.Replace("[", "/[") + "/" + name + ";" + x.Value.ToString());
                            }
                        }
                    }
                }
                catch
                { }
            }

            var cache = RedisConnectorHelper.Connection.GetDatabase();
            foreach (string s in sResult)
            {
                cache.StringSet(s.Split(';')[0], s.Split(';')[1]);
            }

            return sResult;
        }

        public static List<string> GetLatestRawAsync()
        {
            List<string> lResult = new List<string>();
            try
            {
                if (UseRedis)
                {
                    var cache4 = RedisConnectorHelper.Connection.GetDatabase(4);
                    foreach (string sRawID in GetLatestBlocksAsync().Result)
                    {
                        Task.Run(() =>
                        {
                            cache4.StringGet(sRawID);
                        });
                    }
                }
            }
            catch { }

            return lResult;
        }
        */

        /*public static bool WriteHash(string Hash, string Data, string Collection)
{
try
{
    if (!Directory.Exists("wwwroot\\" + Collection))
        Directory.CreateDirectory("wwwroot\\" + Collection);

    if (!File.Exists("wwwroot\\" + Collection + "\\" + Hash + ".json")) //We do not have to create the same hash file twice...
    {
        lock (locker) //only one write operation
        {
            File.WriteAllText("wwwroot\\" + Collection + "\\" + Hash + ".json", Data);
        }
    }

    return true;
}
catch { }

return false;
}*/

        /*public static void WriteJ(JObject JSON)
        {
            //IList<string> keys = JSON.Properties().Select(p => p.Name).ToList();
            //keys.ToString();

            foreach (var x in JSON)
            {
                try
                {
                    string name = x.Key;
                    if (x.Value.Count() > 0)
                    {
                        if (x.Value.Type != JTokenType.Array)
                        {
                            if(x.Value.Type == JTokenType.Object)
                                WriteJ(x.Value as JObject);
                            else
                            {
                                foreach(var oVal in x.Value)
                                {
                                    if (!string.IsNullOrEmpty(oVal.ToString()))
                                    {
                                        File.AppendAllText("wwwroot\\test.json", JSON.Path + "/" + name + ";" + oVal.ToString() + "" + Environment.NewLine);
                                    }
                                }
                            }
                        }
                        else
                        {
                            int i = 0;
                            foreach (var oVal in x.Value)
                            {
                                if (oVal.Type == JTokenType.Object)
                                {
                                    WriteJ(oVal as JObject);
                                }
                                else
                                {
                                    if (!string.IsNullOrEmpty(oVal.ToString()))
                                    {
                                        File.AppendAllText("wwwroot\\test.json", JSON.Path + "/" + name + "/[" + i + "];" + oVal.ToString() + Environment.NewLine);
                                        i++;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(JSON.Path))
                        {
                            if (!string.IsNullOrEmpty(x.Value.ToString()))
                            {
                                File.AppendAllText("wwwroot\\test.json", JSON.Path.Replace("[", "/[") + "/" + name + ";" + x.Value.ToString() + Environment.NewLine);
                            }
                        }
                    }
                }
                catch
                { }
            }
        }*/

        public static void JSort(JObject jObj, bool deep = false)
        {
            var props = jObj.Properties().ToList();
            foreach (var prop in props)
            {
                prop.Remove();
            }

            foreach (var prop in props.OrderBy(p => p.Name))
            {
                jObj.Add(prop);

                if (deep) //Deep Sort
                {
                    var child = prop.Descendants().Where(t => t.Type == (JTokenType.Object)).ToList();

                    foreach (JObject cChild in child)
                    {
                        JSort(cChild, false);
                    }
                }

                if (prop.Value is JObject)
                    JSort((JObject)prop.Value);
            }
        }
    }
}
