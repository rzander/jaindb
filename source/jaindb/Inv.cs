// ************************************************************************************
//          jaindb (c) Copyright 2017 by Roger Zander
// ************************************************************************************

using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
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
                return ConnectionMultiplexer.Connect(RedisServer + ":" + RedisPort.ToString());
            });
        }

        private static Lazy<ConnectionMultiplexer> lazyConnection;

        public static ConnectionMultiplexer Connection
        {
            get
            {
                return lazyConnection.Value;
            }
        }
    }

    public static class Inv
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

        public static string LookupID(string name, string value)
        {
            try
            {
                if (UseRedis)
                {
                    return cache1.StringGet(name.ToLower().TrimStart('#', '@') + "/" + value.ToLower());
                }

                if (UseFileStore)
                {
                    return File.ReadAllText("wwwroot\\" + "_Key" + "\\" + name.TrimStart('#', '@') + "\\" + value + ".json");
                }
            }
            catch { }

            return "";
        }

        public static void WriteHash(JToken oRoot, ref JObject oStatic, string Collection)
        {
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

                    WriteHash(sHash, oRoot.ToString(Newtonsoft.Json.Formatting.None), Collection);
                }
            }
        }

        public static bool WriteHash(string Hash, string Data, string Collection)
        {
            try
            {
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
                    if (!Directory.Exists("wwwroot\\" + Collection))
                        Directory.CreateDirectory("wwwroot\\" + Collection);

                    if (!File.Exists("wwwroot\\" + Collection + "\\" + Hash + ".json")) //We do not have to create the same hash file twice...
                    {
                        lock (locker) //only one write operation
                        {
                            File.WriteAllText("wwwroot\\" + Collection + "\\" + Hash + ".json", Data);
                        }
                    }

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
                                            if (oSubSub.ToString() != sID)
                                            {
                                                string sDir = "wwwroot\\" + "_Key" + "\\" + oSub.Name.ToLower().TrimStart('#');
                                                if (!Directory.Exists(sDir))
                                                    Directory.CreateDirectory(sDir);

                                                File.WriteAllText(sDir + "\\" + oSubSub.ToString() + ".json", sID);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (!string.IsNullOrEmpty((string)oSub.Value))
                                        {
                                            if (oSub.Value.ToString() != sID)
                                            {
                                                string sDir = "wwwroot\\" + "_Key" + "\\" + oSub.Name.ToLower().TrimStart('#');
                                                if (!Directory.Exists(sDir))
                                                    Directory.CreateDirectory(sDir);

                                                File.WriteAllText(sDir + "\\" + (string)oSub.Value + ".json", sID);
                                            }
                                        }
                                    }
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

        public static string ReadHash(string Hash, string Collection)
        {
            try
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
                            return cache2.StringGet(Hash);
                    }
                }

                if (UseFileStore)
                {
                    return File.ReadAllText("wwwroot\\" + Collection + "\\" + Hash + ".json");
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

                    return jRes.ToString(Newtonsoft.Json.Formatting.None);
                }

                return File.ReadAllText("wwwroot\\" + Collection + "\\" + Hash + ".json");
            }
            catch { }

            return "";
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
                JSort(oObj, true); //Enforce full sort

                JObject oStatic = oObj.ToObject<JObject>();
                JObject jTemp = oObj.ToObject<JObject>();

                //Load BlockChain
                Blockchain oChain = GetChain(DeviceID);

                //Remove dynamic data
                foreach (var oTok in oObj.Descendants().Where(t => t.Type == JTokenType.Property && ((JProperty)t).Name.StartsWith("#")).ToList())
                {
                    oTok.Remove();
                }
                foreach (var oTok in oObj.Descendants().Where(t => t.Type == JTokenType.Property && ((JProperty)t).Name.StartsWith("@")).ToList())
                {
                    oTok.Remove();
                }


                //Remove static data
                foreach (var oTok in oStatic.Descendants().Where(t => t.Type == (JTokenType.Property) && !((JProperty)t).Name.StartsWith("#") && ((JProperty)t).Path.Contains(".")).ToList())
                {
                    oTok.Remove();
                }
                foreach (var oTok in oStatic.Properties().Where(t => t.Type == (JTokenType.Property) && ((JProperty)t).Name.StartsWith("@") && ((JProperty)t).Path.StartsWith("@")).ToList())
                {
                    oTok.Remove();
                }

                //Remove NULL values
                foreach (var oTok in oStatic.Descendants().Where(t => t.Parent.Type == (JTokenType.Property) && t.Type == JTokenType.Null).ToList())
                {
                    oTok.Parent.Remove();
                }
                JSort(oObj);
                JSort(oStatic);

                foreach (var oRoot in oObj.Children())
                {
                    try
                    {
                        if (oRoot.First.Type == JTokenType.Array)
                        {
                            foreach (var oItem in oRoot.First.Children())
                            {
                                if (oItem.Type == JTokenType.Object)
                                    WriteHash(oItem, ref oStatic, ((Newtonsoft.Json.Linq.JProperty)oRoot).Name);
                            }
                        }
                        else
                        {
                            WriteHash(oRoot.First, ref oStatic, ((Newtonsoft.Json.Linq.JProperty)oRoot).Name);
                        }
                    }
                    catch { }

                }

                JSort(oStatic);
                string sResult = CalculateHash(oStatic.ToString(Newtonsoft.Json.Formatting.None));

                var oBlock = oChain.GetLastBlock();
                if (oBlock.data != sResult)
                {
                    var oNew = oChain.MineNewBlock(oBlock, BlockType);
                    oChain.UseBlock(sResult, oNew);

                    if (oChain.ValidateChain())
                    {
                        //Console.WriteLine(JsonConvert.SerializeObject(tChain));
                        Console.WriteLine("Blockchain is valid... " + DeviceID);
                        WriteHash(DeviceID, JsonConvert.SerializeObject(oChain), "Chain");

                        //Add an #id if missing
                        if (oStatic["#id"] == null)
                        {
                            oStatic.Add(new JProperty("#id", DeviceID));
                            jTemp.Add(new JProperty("#id", DeviceID));
                        }

                        oStatic.Add(new JProperty("_index", oNew.index));

                        if (oStatic["_date"] == null)
                            oStatic.Add(new JProperty("_date", new DateTime(oNew.timestamp).ToUniversalTime()));

                        jTemp.Add(new JProperty("_index", oNew.index));

                        if (jTemp["_date"] == null)
                            jTemp.Add(new JProperty("_date", new DateTime(oNew.timestamp).ToUniversalTime()));

                        jTemp.Add(new JProperty("_hash", oNew.data));
                        JSort(jTemp);

                        WriteHash(DeviceID, jTemp.ToString(Formatting.None), "_Full");
                    }
                    else
                    {
                        Console.WriteLine("Blockchain is NOT valid... " + DeviceID);
                    }
                }


                JSort(oStatic);
                WriteHash(sResult, oStatic.ToString(Newtonsoft.Json.Formatting.None), "Assets");


                return sResult;
            }
            catch { }

            return "";
        }

        public static JObject GetFull(string DeviceID, int Index = -1)
        {
            try
            {
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
                    JObject oInv = JObject.Parse(sData);
                    try
                    {
                        if (oInv["_index"] == null)
                            oInv.Add(new JProperty("_index", oRaw["_index"]));
                        if (oInv["_inventoryDate"] == null)
                            oInv.Add(new JProperty("_inventoryDate", oRaw["_inventoryDate"]));
                        if (oInv["_hash"] == null)
                            oInv.Add(new JProperty("_hash", oRaw["_hash"]));
                    }
                    catch { }

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

                    JSort(oInv);

                    if (Index == -1)
                    {
                        WriteHash(DeviceID, oInv.ToString(), "_full");
                    }


                    return oInv;
                }

            }
            catch { }

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
                if (!string.IsNullOrEmpty(path))
                {
                    foreach (string spath in path.Split(','))
                    {
                        foreach (JProperty oTok in oInv.Descendants().Where(t => t.Path.StartsWith(spath.Split('.')[0]) && t.Type == JTokenType.Property && ((JProperty)t).Name.StartsWith("##hash")).ToList())
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

                return oInv;
            }
            catch { }

            return jResult;
        }

        public static JObject GetDiff(string DeviceId, int IndexLeft, int mode = -1, int IndexRight = -1)
        {
            try
            {
                var right = GetFull(DeviceId, IndexRight);
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

                return JObject.Parse(oDiff.ToString());
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
        public static List<string> search(string searchkey, string KeyID = "#id")
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

        public static JArray query(string paths, string select)
        {
            paths = System.Net.WebUtility.UrlDecode(paths);
            select = System.Net.WebUtility.UrlDecode(select);

            if (string.IsNullOrEmpty(select))
                select = "#id"; //,#Name,_inventoryDate

            //int i = 0;
            DateTime dStart = DateTime.Now;
            //JObject lRes = new JObject();
            JArray aRes = new JArray();
            List<string> lLatestHash = GetAllChainsAsync().Result;
            foreach (string sHash in lLatestHash)
            {
                try
                {
                    var jObj = GetFull(sHash);

                    JObject oRes = new JObject();
                    foreach (string sAttrib in select.Split(','))
                    {
                        oRes.Add(sAttrib.Trim(), jObj[sAttrib]);
                    }
                    foreach (string path in paths.Split(','))
                    {
                        try
                        {
                            var oToks = jObj.SelectTokens(path.Trim(), false);
                            foreach (JToken oTok in oToks)
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
                                    oRes.Add(oTok.Parent);

                                if (oTok.Type == JTokenType.Date)
                                    oRes.Add(oTok.Parent);

                            }
                            if (oToks.Count() == 0)
                                oRes = null;
                        }
                        catch { }
                    }
                    if (oRes != null)
                    {
                        aRes.Add(oRes);
                        //lRes.Add(i.ToString(), oRes);
                        //i++;
                    }
                }
                catch { }
            }

            return aRes;

        }

        public static JArray queryAll(string paths, string select)
        {
            paths = System.Net.WebUtility.UrlDecode(paths);
            select = System.Net.WebUtility.UrlDecode(select);

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
                        JObject jObj = GetRaw(cache4.StringGet(oObj), paths);
                        JObject oRes = new JObject();

                        foreach (string sAttrib in select.Split(','))
                        {
                            oRes.Add(sAttrib.Trim(), jObj[sAttrib]);
                        }
                        foreach (string path in paths.Split(','))
                        {
                            try
                            {
                                var oToks = jObj.SelectTokens(path.Trim(), false);
                                foreach (JToken oTok in oToks)
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
                                        oRes.Add(oTok.Parent);

                                    if (oTok.Type == JTokenType.Date)
                                        oRes.Add(oTok.Parent);

                                }
                                if (oToks.Count() == 0)
                                    oRes = null;
                            }
                            catch { }
                        }

                        if (oRes != null)
                        {
                            string sHa = CalculateHash(oRes.ToString(Formatting.None));
                            if (!lHashes.Contains(sHa))
                            {
                                aRes.Add(oRes);
                                lHashes.Add(sHa);
                            }
                        }
                    }

                    return aRes;
                }

                if (UseFileStore)
                {
                    foreach (var oFile in new DirectoryInfo("wwwroot/Assets").GetFiles("*.json"))
                    {
                        JObject jObj = GetRaw(File.ReadAllText(oFile.FullName), paths);
                        JObject oRes = new JObject();

                        foreach (string sAttrib in select.Split(','))
                        {
                            oRes.Add(sAttrib.Trim(), jObj[sAttrib]);
                        }

                        foreach (string path in paths.Split(','))
                        {
                            try
                            {
                                var oToks = jObj.SelectTokens(path.Trim(), false);
                                foreach (JToken oTok in oToks)
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
                                        oRes.Add(oTok.Parent);

                                    if (oTok.Type == JTokenType.Date)
                                        oRes.Add(oTok.Parent);

                                }
                                if (oToks.Count() == 0)
                                    oRes = null;
                            }
                            catch { }
                        }

                        if (oRes != null)
                        {
                            string sHa = CalculateHash(oRes.ToString(Formatting.None));
                            if (!lHashes.Contains(sHa))
                            {
                                aRes.Add(oRes);
                                lHashes.Add(sHa);
                            }
                        }
                    }

                    return aRes;
                }
            }
            catch { }

            return new JArray();
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
                            lResult.Add(oFile.Name.Split('.')[0]);
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
                            var jObj = JObject.Parse(cache3.StringGet(sID));

                            foreach (var sBlock in jObj.SelectTokens("Chain[*].data"))
                            {
                                try
                                {
                                    string sBlockID = sBlock.Value<string>();
                                    if (!string.IsNullOrEmpty(sBlockID))
                                    {
                                        var jBlock = GetRaw(cache4.StringGet(sBlockID));
                                        jBlock.Remove("#Id"); //old Version of jainDB 
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
                                        jBlock.Remove("#Id"); //old Version of jainDB 
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
