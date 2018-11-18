using JainDBProvider;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.SqlServer;
using Microsoft.Extensions.Caching.Distributed;
using System.Data.SqlClient;

namespace Plugin_SQLCache
{
    public class Plugin_SQLCache : IStore
    {
        private static readonly object locker = new object();
        private bool bReadOnly = false;
        private bool ContinueAfterWrite = true;
        private bool CacheFull = true;
        private bool CacheKeys = true;
        private string SQLConnectionString = @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=JCache;Integrated Security=true";
        private string SQLTable = "JCache";
        private int SlidingExpiration = 2678400;

        private string FilePath = "";
        private JObject JConfig = new JObject();
        private SqlServerCache oSrv;

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
                    File.WriteAllText(Assembly.GetExecutingAssembly().Location.Replace(".dll", ".json"), Properties.Resources.Plugin_SQLCache);
                }

                if (File.Exists(Assembly.GetExecutingAssembly().Location.Replace(".dll", ".json")))
                {
                    JConfig = JObject.Parse(File.ReadAllText(Assembly.GetExecutingAssembly().Location.Replace(".dll", ".json")));
                    bReadOnly = JConfig["ReadOnly"].Value<bool>();
                    ContinueAfterWrite = JConfig["ContinueAfterWrite"].Value<bool>();
                    CacheFull = JConfig["CacheFull"].Value<bool>();
                    CacheKeys = JConfig["CacheKeys"].Value<bool>();
                    SQLConnectionString = JConfig["SQLConnectionString"].Value<string>();
                    SQLTable = JConfig["SQLTable"].Value<string>();
                    SlidingExpiration = JConfig["SlidingExpiration"].Value<int>();
                }
                else
                {
                    JConfig = new JObject();
                }

                try
                {
                    SqlServerCacheOptions oOption = new SqlServerCacheOptions()
                    {
                        ConnectionString = SQLConnectionString,
                        SchemaName = "dbo",
                        TableName = "JCache",
                        DefaultSlidingExpiration = new TimeSpan(0, 0, 0, SlidingExpiration)
                    };

                    oSrv = new SqlServerCache(oOption);
                    oSrv.SetString("key1", "value1", new DistributedCacheEntryOptions() { SlidingExpiration = new TimeSpan(1000) });
                }
                catch
                {
                    using (SqlConnection connection = new SqlConnection(SQLConnectionString))
                    {
                        SqlCommand command = new SqlCommand(Properties.Resources.CreateTable, connection);
                        command.Connection.Open();
                        command.ExecuteNonQuery();
                    }

                    SqlServerCacheOptions oOption = new SqlServerCacheOptions()
                    {
                        ConnectionString = SQLConnectionString,
                        SchemaName = "dbo",
                        TableName = "JCache",
                        DefaultSlidingExpiration = new TimeSpan(0, 0, 0, SlidingExpiration)
                    };

                    oSrv = new SqlServerCache(oOption);
                    Console.WriteLine("SQL Table 'JCache' created...");
                }

                oSrv.SetString("key1", "value1", new DistributedCacheEntryOptions() { SlidingExpiration = new TimeSpan(1000) });
                Console.WriteLine(oSrv.GetString("key1"));
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
                }
            }

            //Cache Data
            if (SlidingExpiration >= 0)
            {
                oSrv.SetString(Collection + "\\" + Hash, Data, new DistributedCacheEntryOptions() { SlidingExpiration = new TimeSpan(0,0, SlidingExpiration) });
            }
            else
            {
                oSrv.SetString(Collection + "\\" + Hash, Data, new DistributedCacheEntryOptions() { SlidingExpiration = null });
            }

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
                string Coll2 = Collection;
                //Remove invalid Characters in Path anf File
                foreach (var sChar in Path.GetInvalidPathChars())
                {
                    Coll2 = Coll2.Replace(sChar.ToString(), "");
                    Hash = Hash.Replace(sChar.ToString(), "");
                }
                
                sResult = oSrv.GetString(Collection + "\\" + Hash);
                if (!string.IsNullOrEmpty(sResult))
                    oSrv.RefreshAsync(Collection + "\\" + Hash);

#if DEBUG
                    //Check if hashes are valid...
                if (Collection != "_full" && Collection != "_chain" && Collection != "_assets")
                {
                    var jData = JObject.Parse(sResult);
                    /*if (jData["#id"] != null)
                        jData.Remove("#id");*/
                    if (jData["_date"] != null)
                        jData.Remove("_date");
                    if (jData["_index"] != null)
                        jData.Remove("_index");

                    string s1 = jaindb.jDB.CalculateHash(jData.ToString(Newtonsoft.Json.Formatting.None));
                    if (Hash != s1)
                    {
                        s1.ToString();
                        return "";
                    }
                }
#endif

            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error ReadHash_1: " + ex.Message.ToString());
            }
            return sResult;
        }

        public int totalDeviceCount(string sPath = "")
        {
            int iCount = -1;
            try
            {
                if (string.IsNullOrEmpty(sPath))
                    sPath = Path.Combine(FilePath, "_chain");

                if (Directory.Exists(sPath))
                    iCount = Directory.GetFiles(sPath).Count(); //count Blockchain Files
            }
            catch { }

            return iCount;
        }

        public IEnumerable<JObject> GetRawAssets(string paths)
        {
            foreach (var oFile in new DirectoryInfo(Path.Combine(FilePath, "_assets")).GetFiles("*.json"))
            {
                JObject jObj = jaindb.jDB.GetRaw(File.ReadAllText(oFile.FullName), paths);

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

        public string LookupID(string name, string value)
        {
            string sResult = "";
            try
            {
                sResult = File.ReadAllText(Path.Combine(FilePath, "_key", name.TrimStart('#', '@'), value + ".json"));
            }
            catch { }

            return sResult;
        }

        public bool WriteLookupID(string name, string value, string id)
        {
            try
            {
                string sDir = Path.Combine(FilePath, "_key", name.ToLower().TrimStart('#'));

                //Remove invalid Characters in Path
                foreach (var sChar in Path.GetInvalidPathChars())
                {
                    sDir = sDir.Replace(sChar.ToString(), "");
                }

                if (!Directory.Exists(sDir))
                    Directory.CreateDirectory(sDir);

                File.WriteAllText(Path.Combine(sDir, value + ".json"), id);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public List<string> GetAllIDs()
        {
            List<string> lResult = new List<string>();

            try
            {
                foreach (var oFile in new DirectoryInfo(Path.Combine(FilePath, "_chain")).GetFiles("*.json"))
                {
                    lResult.Add(System.IO.Path.GetFileNameWithoutExtension(oFile.Name));
                }
            }
            catch { }

            return lResult;
        }
    }


}

