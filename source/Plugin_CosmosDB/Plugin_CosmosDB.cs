using JainDBProvider;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Newtonsoft.Json.Linq;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Plugin_CosmosDB
{
    public class Plugin_CosmosDB : IStore
    {
        private bool bReadOnly = false;
        private int SlidingExpiration = -1;
        private bool ContinueAfterWrite = true;
        private bool CacheFull = true;
        private bool CacheKeys = true;

        public string databaseId;
        public string endpointUrl;
        public string authorizationKey;
        public static DocumentClient CosmosDB;
        public static Database database;
        public static List<string> CosmosTables = new List<string>();
        private static readonly object locker = new object();

        private JObject JConfig = new JObject();

        public Dictionary<string, string> Settings { get; set; }

        public string Name
        {
            get
            {
                return "300_CosmosDB";
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
                    File.WriteAllText(Assembly.GetExecutingAssembly().Location.Replace(".dll", ".json"), Properties.Resources.Plugin_CosmosDB);
                }

                if (File.Exists(Assembly.GetExecutingAssembly().Location.Replace(".dll", ".json")))
                {
                    JConfig = JObject.Parse(File.ReadAllText(Assembly.GetExecutingAssembly().Location.Replace(".dll", ".json")));
                    bReadOnly = JConfig["ReadOnly"].Value<bool>();
                    SlidingExpiration = JConfig["SlidingExpiration"].Value<int>();
                    ContinueAfterWrite = JConfig["ContinueAfterWrite"].Value<bool>();
                    CacheFull = JConfig["CacheFull"].Value<bool>();
                    CacheKeys = JConfig["CacheKeys"].Value<bool>();
                    databaseId = JConfig["databaseId"].Value<string>();
                    endpointUrl = JConfig["endpointUrl"].Value<string>();
                    authorizationKey = JConfig["authorizationKey"].Value<string>();

                    CosmosDB = new DocumentClient(new Uri(endpointUrl), authorizationKey);
                    CosmosDB.OpenAsync();
                }
                else
                {
                    JConfig = new JObject();
                }

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

            try
            {
                if (database == null)
                {
                    database = CosmosDB.CreateDatabaseQuery().Where(db => db.Id == databaseId).AsEnumerable().FirstOrDefault();
                    if (database == null)
                        database = CosmosDB.CreateDatabaseAsync(new Database { Id = databaseId }).Result;
                }

                if (!CosmosTables.Contains(sColl))
                {
                    var collquery = CosmosDB.CreateDocumentCollectionQuery(database.SelfLink, new FeedOptions() { MaxItemCount = 1 });
                    var oCollExists = collquery.Where(t => t.Id == sColl).AsEnumerable().Any();
                    if (!oCollExists)
                    {
                        lock (locker) //only one write operation
                        {
                            CosmosDB.CreateDocumentCollectionAsync(database.SelfLink, new DocumentCollection { Id = sColl }).Wait();
                            CosmosTables.Add(sColl);
                        }
                    }
                }

                //string sJ = "{ \"Id\" : \"" + Hash + "\"," + Data.TrimStart('{');
                var jObj = JObject.Parse(Data);

                try
                {
                    if (sColl == "chain")
                    {
                        if (jObj.GetValue("id") == null)
                        {
                            jObj.Add("id", Hash);
                        }
                        jObj.Remove("#id");
                        //lock (locker) //only one write operation
                        //{
                        //    //var dUri = UriFactory.CreateDocumentUri(databaseId, sColl, Hash);
                        //    var cUri = UriFactory.CreateDocumentCollectionUri(databaseId, sColl);
                        //    CosmosDB.UpsertDocumentAsync(cUri, jObj).Wait();
                        //}

                        var cUri = UriFactory.CreateDocumentCollectionUri(databaseId, sColl);
                        var docquery = CosmosDB.CreateDocumentQuery(cUri, new FeedOptions() { MaxItemCount = 1 });
                        var oDocExists = docquery.Where(t => t.Id == Hash).AsEnumerable().Any();
                        if (!oDocExists)
                        {
                            lock (locker) //only one write operation
                            {
                                CosmosDB.CreateDocumentAsync(cUri, jObj).Wait();
                            }
                        }
                        else
                        {
                            lock (locker) //only one write operation
                            {
                                var dUri = UriFactory.CreateDocumentUri(databaseId, sColl, Hash);
                                CosmosDB.ReplaceDocumentAsync(dUri, jObj).Wait();
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

                        var cUri = UriFactory.CreateDocumentCollectionUri(databaseId, sColl);
                        Console.WriteLine("RU charge:" + CosmosDB.CreateDocumentAsync(cUri, jObj).Result.ResponseHeaders["x-ms-request-charge"]);
                    }


                }
                catch (Exception ex)
                {
                    ex.Message.ToString();
                    return false;
                }
            }
            catch { }
            if (ContinueAfterWrite)
                return false;
            else
                return true;
        }

        public string ReadHash(string Hash, string Collection)
        {
            string sResult = "";

            try
            {
                Collection = Collection.ToLower();
                if (database == null)
                {
                    database = CosmosDB.CreateDatabaseQuery().Where(db => db.Id == databaseId).AsEnumerable().FirstOrDefault();
                    if (database == null)
                        database = CosmosDB.CreateDatabaseAsync(new Database { Id = databaseId }).Result;
                }

                string sColl = Collection;
                switch (Collection)
                {
                    case "_assets":
                        sColl = "assets";
                        break;
                    case "_chain":
                        sColl = "chain";
                        break;
                }

                var sRes = CosmosDB.ReadDocumentAsync(UriFactory.CreateDocumentUri(databaseId, sColl, Hash)).Result.Resource;
                JObject jRes = JObject.Parse(sRes.ToString());
                string sID = jRes["id"].Value<string>();

                jRes.Remove("id");
                jRes.Remove("_rid");
                jRes.Remove("_ts");
                jRes.Remove("_etag");
                jRes.Remove("_self");
                jRes.Remove("_attachments");
                jRes.Add("#id", sID);
                sResult = jRes.ToString(Newtonsoft.Json.Formatting.None);

                return sResult;
            }
            catch { }

            return sResult;
        }

        public int totalDeviceCount(string sPath = "")
        {
            int iCount = -1;
            try
            {
                var oAssets = CosmosDB.CreateDocumentQuery(UriFactory.CreateDocumentCollectionUri(databaseId, "chain"), "SELECT c.id FROM c");
                Console.WriteLine("RU charge:" + oAssets.AsDocumentQuery().ExecuteNextAsync().Result.ResponseHeaders["x-ms-request-charge"]);
                iCount = oAssets.ToList().Count();
            }
            catch { }

            return iCount;
        }

        public IEnumerable<JObject> GetRawAssets(string paths)
        {
            var oAssets = CosmosDB.CreateDocumentQuery(UriFactory.CreateDocumentCollectionUri(databaseId, "assets"), "SELECT * FROM c");
            foreach (var oAsset in oAssets)
            {
                JObject jObj = oAsset;
                yield return jObj;
            }
        }

        public string LookupID(string name, string value)
        {
            return "";
        }

        public bool WriteLookupID(string name, string value, string id)
        {
            return false;
        }

        public List<string> GetAllIDs()
        {
            List<string> lResult = new List<string>();

            if (database == null)
            {
                database = CosmosDB.CreateDatabaseQuery().Where(db => db.Id == databaseId).AsEnumerable().FirstOrDefault();
                if (database == null)
                    database = CosmosDB.CreateDatabaseAsync(new Database { Id = databaseId }).Result;
            }

            var cUri = UriFactory.CreateDocumentCollectionUri(databaseId, "chain");

            var docquery = CosmosDB.CreateDocumentQuery<Document>(cUri).Select(t => t.Id);
            foreach (var oDoc in docquery.AsEnumerable())
            {
                lResult.Add(oDoc);
            }

            return lResult;
        }

    }


}

