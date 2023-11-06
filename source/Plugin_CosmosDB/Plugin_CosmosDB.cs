using JainDBProvider;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Plugin_CosmosDB
{
    public class Plugin_CosmosDB : IStore
    {
        public static CosmosClient CosmosDB;
        public static List<string> CosmosTables = new List<string>();
        public static Database database;
        public string authorizationKey;
        public string databaseId;
        public string endpointUrl;
        private static readonly object locker = new object();
        private bool bReadOnly = false;
        private bool CacheAsset = true;
        private bool CacheFull = true;
        private bool CacheKeys = true;
        private bool ContinueAfterWrite = true;
        private JObject JConfig = new JObject();
        private int SlidingExpiration = -1;
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
            List<string> lResult = new List<string>();

            Container container = (database.CreateContainerIfNotExistsAsync("chain", "/customerid").Result).Container;

            using (FeedIterator<JObject> feedIterator = container.GetItemQueryIterator<JObject>("select c.assetid, c.customerid from c",null, null))
            {
                while (feedIterator.HasMoreResults)
                {
                    var feedResponse = await feedIterator.ReadNextAsync();
                    foreach (var item in feedResponse)
                    {
                        lResult.Add(item["assetid"]?.Value<string>() + ";" + item["customerid"]?.Value<string>());
                    }
                    Console.WriteLine("RU charge query:" + feedResponse.RequestCharge);
                }
                
            }
            

            await Task.CompletedTask;
            return lResult;
        }

        public async IAsyncEnumerable<JObject> GetRawAssetsAsync(string paths, [EnumeratorCancellation] CancellationToken ct)
        {
            //var oAssets = CosmosDB.CreateDocumentQuery(UriFactory.CreateDocumentCollectionUri(databaseId, "assets"), "SELECT * FROM c");

            //await Task.CompletedTask;

            //foreach (var oAsset in oAssets)
            //{
            //    JObject jObj = oAsset;

            //    yield return jObj;
            //}
            await Task.CompletedTask;
            yield return null;
        }

        public void Init()
        {
            if (Settings == null)
                Settings = new Dictionary<string, string>();

            try
            {
                if (!File.Exists(Assembly.GetExecutingAssembly().Location.Replace(".dll", ".json")))
                {
                    File.WriteAllText(Assembly.GetExecutingAssembly().Location.Replace(".dll", ".json"), Properties.Resources.Plugin_CosmosDB);
                }

                if (File.Exists(Assembly.GetExecutingAssembly().Location.Replace(".dll", ".json")))
                {
                    JConfig = JObject.Parse(File.ReadAllText(Assembly.GetExecutingAssembly().Location.Replace(".dll", ".json")));
                    bReadOnly = JConfig["ReadOnly"]?.Value<bool>() ?? false;
                    SlidingExpiration = JConfig["SlidingExpiration"]?.Value<int>() ?? 0;
                    ContinueAfterWrite = JConfig["ContinueAfterWrite"]?.Value<bool>() ?? true; 
                    CacheAsset = JConfig["CacheAssets"]?.Value<bool>() ?? true;
                    CacheFull = JConfig["CacheFull"]?.Value<bool>() ?? true;
                    CacheKeys = JConfig["CacheKeys"]?.Value<bool>() ?? false;
                    databaseId = JConfig["databaseId"]?.Value<string>() ?? "jaindb";
                    endpointUrl = JConfig["endpointUrl"]?.Value<string>() ?? "";
                    authorizationKey = JConfig["authorizationKey"]?.Value<string>() ?? "";

                    CosmosDB = new CosmosClient(endpointUrl, authorizationKey);
                    database = CosmosDB.CreateDatabaseIfNotExistsAsync(databaseId).Result;
                }
                else
                {
                    JConfig = new JObject();
                }

            }
            catch { }
        }

        public async Task<string> LookupIDAsync(string name, string value, CancellationToken ct = default(CancellationToken))
        {
            await Task.CompletedTask;
            return "";
        }

        public async Task<string> ReadHashAsync(string Hash, string Collection, CancellationToken ct = default(CancellationToken))
        {
            string sResult = "";

            try
            {
                string sColl = Collection.ToLower();
                string PartitionKey = "";

                if(Hash.Contains(';'))
                {
                    PartitionKey = Hash.Split(';')[1];
                    Hash = Hash.Split(';')[0];
                }

                switch (sColl)
                {
                    case "_assets":
                        sColl = "assets";
                        break;
                    case "_chain":
                        sColl = "chain";
                        break;
                    case "_full":
                        sColl = "full";
                        break;
                    default:
                        return "";
                }

                Container container = (await database.CreateContainerIfNotExistsAsync(sColl, "/customerid")).Container;
                var oRes = await container.ReadItemAsync<JObject>(Hash, new PartitionKey(PartitionKey), cancellationToken: ct);
                Console.WriteLine("RU charge read:" + oRes.RequestCharge);

                JObject jRes = oRes.Resource;
                string sID = jRes["id"].Value<string>();

                jRes.Remove("id");
                jRes.Remove("_rid");
                jRes.Remove("_ts");
                jRes.Remove("_etag");
                jRes.Remove("_self");
                jRes.Remove("_attachments");

                if (sColl == "chain")
                {
                    jRes.Remove("customerid");
                    jRes.Remove("index");
                    jRes.Remove("modifydate");
                    jRes.Remove("keys");
                    jRes.Remove("assetid");
                }

                jRes.Add("#id", sID);
                sResult = jRes.ToString(Newtonsoft.Json.Formatting.None);

                return sResult;
            }
            catch { }

            return sResult;
        }

        public async Task<int> totalDeviceCountAsync(string sPath = "", CancellationToken ct = default(CancellationToken))
        {
            int iCount = -1;
            //try
            //{
            //    await Task.CompletedTask;
            //    var oAssets = CosmosDB.CreateDocumentQuery(UriFactory.CreateDocumentCollectionUri(databaseId, "chain"), "SELECT c.id FROM c");
            //    Console.WriteLine("RU charge:" + oAssets.AsDocumentQuery().ExecuteNextAsync().Result.ResponseHeaders["x-ms-request-charge"]);
            //    iCount = oAssets.ToList().Count();
            //}
            //catch { }

            try
            {
                Container container = (database.CreateContainerIfNotExistsAsync("chain", "/customerid").Result).Container;
                using FeedIterator<int> feedCount = container.GetItemQueryIterator<int>(new QueryDefinition("SELECT VALUE COUNT(1) FROM c"));


                while (feedCount.HasMoreResults)
                {
                    FeedResponse<int> response = await feedCount.ReadNextAsync();

                    // Iterate query results
                    foreach (int item in response)
                    {
                        iCount = item;
                    }
                }
            }
            catch { }

            await Task.CompletedTask;
            return iCount;
        }

        public async Task<bool> WriteHashAsync(string Hash, string Data, string Collection, CancellationToken ct = default(CancellationToken))
        {
            if (bReadOnly)
                return false;

            if (string.IsNullOrEmpty(Data) || Data == "null")
                return true;

            Collection = Collection.ToLower();
            string sColl = Collection;
            switch (Collection)
            {
                case "_full":
                    sColl = "assets";
                    break;
                case "_chain":
                    sColl = "chain";
                    break;
                default:
                    if (ContinueAfterWrite)
                        return false;
                    else
                        return true;

            }

            string PartitionKey = "";

            if (Hash.Contains(';'))
            {
                PartitionKey = Hash.Split(';')[1];
                Hash = Hash.Split(';')[0];
            }

            try
            {
                Container container = (await database.CreateContainerIfNotExistsAsync(sColl, "/customerid")).Container;

                //string sJ = "{ \"Id\" : \"" + Hash + "\"," + Data.TrimStart('{');
                var jObj = JObject.Parse(Data);

                string CustomerID = jObj["customerid"]?.Value<string>() ?? PartitionKey;

                if (string.IsNullOrEmpty(CustomerID))
                    return true;

                try
                {
                    if (sColl == "chain")
                    {
                        if (jObj.GetValue("id") == null)
                        {
                            jObj.Add("id", Hash);
                        }
                        jObj.Remove("#id");

                        try
                        {
                            //var oItem = await container.ReadItemAsync<JObject>(Hash, new PartitionKey(CustomerID), new ItemRequestOptions() { }, cancellationToken: ct);
                            //Console.WriteLine("RU charge read:" + oItem.RequestCharge);

                            lock (locker) //only one write operation
                            {
                                //Console.WriteLine("RU charge chain:" + container.CreateItemAsync(jObj, new PartitionKey(CustomerID)).Result.RequestCharge);
                                //Console.WriteLine("RU charge chain:" + container.ReplaceItemAsync(jObj, Hash, new PartitionKey(CustomerID)).Result.RequestCharge);
                                Console.WriteLine("RU charge chain:" + container.UpsertItemAsync(jObj, new PartitionKey(CustomerID)).Result.RequestCharge);
                            }
                        }
                        catch
                        {
                            lock (locker) //only one write operation
                            {
                                Console.WriteLine("RU charge chain:" + container.CreateItemAsync(jObj, new PartitionKey(CustomerID)).Result.RequestCharge);
                            }
                        }
                    }
                    else
                    {
                        
                        if (jObj["_objectid"] == null)
                            jObj.AddFirst(new JProperty("_objectid", Hash));


                        if (jObj.GetValue("id") == null)
                        {
                            jObj.Add("id", jObj["_hash"].Value<string>());
                        }
                        jObj.Remove("#id");

                        if (jObj.GetValue("customerid") == null)
                        {
                            jObj.Add("customerid", CustomerID);
                        }

                        //rename all # attributes
                        //foreach (var oKey in jObj.Descendants().Where(t => t.Type == JTokenType.Property && (((JProperty)t).Name.StartsWith("#")) && !((JProperty)t).Name.StartsWith("##")).ToList())
                        //{
                        //    if (ct.IsCancellationRequested)
                        //        throw new TaskCanceledException();

                        //    try
                        //    {
                        //        JProperty jNew = new JProperty(((JProperty)oKey).Name.TrimStart('#'), ((JProperty)oKey).Value);
                        //        oKey.Parent.Add(jNew);
                        //        oKey.Remove();
                        //    }
                        //    catch (Exception ex)
                        //    {
                        //        Debug.WriteLine("Error UploadFull_3: " + ex.Message.ToString());
                        //    }
                        //}

                        JObject jAsset = (JObject)jObj.DeepClone();

                        //remove all @ attributes
                        foreach (var oKey in jAsset.Descendants().Where(t => t.Type == JTokenType.Property && ((JProperty)t).Name.StartsWith("@")).ToList())
                        {
                            if (ct.IsCancellationRequested)
                                throw new TaskCanceledException();

                            try
                            {
                                oKey.Remove();
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("Error UploadFull_3: " + ex.Message.ToString());
                            }
                        }

                        if (CacheAsset)
                        {
                            try
                            {
                                //var oItem = await container.ReadItemAsync<JObject>(jObj["_hash"].Value<string>(), new PartitionKey(CustomerID), new ItemRequestOptions() { }, cancellationToken: ct);
                                //Console.WriteLine("RU charge read:" + oItem.RequestCharge);

                                lock (locker) //only one write operation
                                {
                                    Console.WriteLine("RU charge asset:" + container.CreateItemAsync(jObj, new PartitionKey(CustomerID)).Result.RequestCharge);
                                    //Console.WriteLine("RU charge asset:" + container.ReplaceItemAsync(jAsset, jObj["_hash"].Value<string>(), new PartitionKey(CustomerID)).Result.RequestCharge);
                                    //Console.WriteLine("RU charge asset:" + container.UpsertItemAsync(jAsset, new PartitionKey(CustomerID)).Result.RequestCharge);
                                }
                            }
                            catch
                            {
                                //lock (locker) //only one write operation
                                //{
                                //    Console.WriteLine("RU charge asset:" + container.CreateItemAsync(jAsset, new PartitionKey(CustomerID)).Result.RequestCharge);
                                //}
                            }
                        }

                        jObj["id"] =  Hash;
                        if (CacheFull)
                        {
                            Container full = (await database.CreateContainerIfNotExistsAsync("full", "/customerid")).Container;
                            try
                            {
                                //var oItem = await full.ReadItemAsync<JObject>(Hash, new PartitionKey(CustomerID), new ItemRequestOptions() { }, cancellationToken: ct);
                                //Console.WriteLine("RU charge read:" + oItem.RequestCharge);

                                lock (locker) //only one write operation
                                {
                                    //Console.WriteLine("RU charge full:" + full.CreateItemAsync(jObj, new PartitionKey(CustomerID)).Result.RequestCharge);
                                    //Console.WriteLine("RU charge full:" + full.ReplaceItemAsync(jObj, Hash, new PartitionKey(CustomerID)).Result.RequestCharge);
                                    Console.WriteLine("RU charge full:" + full.UpsertItemAsync(jObj, new PartitionKey(CustomerID)).Result.RequestCharge);
                                }
                            }
                            catch
                            {
                                lock (locker) //only one write operation
                                {
                                    Console.WriteLine("RU charge full:" + full.CreateItemAsync(jObj, new PartitionKey(CustomerID)).Result.RequestCharge);
                                }
                            }
                        }

                        //var cUri = UriFactory.CreateDocumentCollectionUri(databaseId, sColl);
                        //Console.WriteLine("RU charge:" + CosmosDB.CreateDocumentAsync(cUri, jObj).Result.ResponseHeaders["x-ms-request-charge"]);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error 382:" + ex.Message);
                    return false;
                }
            }
            catch { }

            await Task.CompletedTask;
            if (ContinueAfterWrite)
                return false;
            else
                return true;
        }

        public async Task<bool> WriteLookupIDAsync(string name, string value, string id, CancellationToken ct = default(CancellationToken))
        {
            await Task.CompletedTask;
            return false;
        }
    }


}

