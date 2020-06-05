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
        private bool CacheFull = true;
        private bool CacheKeys = true;
        private bool ContinueAfterWrite = true;
        private string FilePath = "";
        private JObject JConfig = new JObject();
        private SqlServerCache oSrv;
        private int SlidingExpiration = 2678400;
        private string SQLConnectionString = @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=JCache;Integrated Security=true";
        private string SQLTable = "JCache";
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

        public IAsyncEnumerable<JObject> GetRawAssetsAsync(string paths)
        {
            return null; //We cannot list objects
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
                    try
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
                    catch(Exception ex)
                    {
                        Console.WriteLine("Error: " + ex.Message);
                    }
                }

                oSrv.SetString("key1", "value1", new DistributedCacheEntryOptions() { SlidingExpiration = new TimeSpan(1000) });
                Console.WriteLine(oSrv.GetString("key1"));
            }
            catch { }
        }

        public string LookupID(string name, string value)
        {
            string sResult = "";
            try
            {
                sResult = oSrv.GetString("_key\\" + name.ToLower() + "\\" + value.ToLower());
            }
            catch { }

            return sResult;
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

                    string s1 = jaindb.jDB.CalculateHashAsync(jData.ToString(Newtonsoft.Json.Formatting.None)).Result;
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
            return iCount;
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

        public bool WriteLookupID(string name, string value, string id)
        {
            try
            {
                oSrv.SetString("_key\\" + name.ToLower() + "\\" + value.ToLower(), id, new DistributedCacheEntryOptions() { SlidingExpiration = new TimeSpan(0, 0, SlidingExpiration) });

                return true;
            }
            catch
            {
                return false;
            }
        }
    }


}

