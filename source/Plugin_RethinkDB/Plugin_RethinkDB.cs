using JainDBProvider;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Plugin_RethinkDB
{
    public class Plugin_RethinkDB : IStore
    {
        private bool bReadOnly = false;
        private bool ContinueAfterWrite = true;
        private bool CacheFull = true;
        private bool CacheKeys = true;

        private JObject JConfig = new JObject();

        private static readonly object locker = new object();
        public static RethinkDb.Driver.RethinkDB R = RethinkDb.Driver.RethinkDB.R;
        public static RethinkDb.Driver.Net.Connection conn;
        public static List<string> RethinkTables = new List<string>();
        private string RethinkDBServer = "localhost";
        private int Port = 28015;
        private string Database = "jaindb";

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
                    File.WriteAllText(Assembly.GetExecutingAssembly().Location.Replace(".dll", ".json"), Properties.Resources.Plugin_RethinkDB);
                }

                if (File.Exists(Assembly.GetExecutingAssembly().Location.Replace(".dll", ".json")))
                {
                    JConfig = JObject.Parse(File.ReadAllText(Assembly.GetExecutingAssembly().Location.Replace(".dll", ".json")));
                    bReadOnly = JConfig["ReadOnly"].Value<bool>();
                    ContinueAfterWrite = JConfig["ContinueAfterWrite"].Value<bool>();
                    CacheFull = JConfig["CacheFull"].Value<bool>();
                    CacheKeys = JConfig["CacheKeys"].Value<bool>();
                    RethinkDBServer = JConfig["RethinkDBServer"].Value<string>();
                    Port = JConfig["Port"].Value<int>();
                    Database = JConfig["Database"].Value<string>();
                }
                else
                {
                    JConfig = new JObject();
                }
                try
                {
                    conn = R.Connection()
                        .Hostname(RethinkDBServer)
                        .Port(Port)
                        .Timeout(60)
                        .Db(Database)
                        .Connect();

                    //Create DB if missing
                    if (!((string[])R.DbList().Run<string[]>(conn)).Contains(Database))
                    {
                        R.DbCreate(Database).Run(conn);
                    }

                    //Get Tables
                    RethinkTables = ((string[])R.TableList().Run<string[]>(conn)).ToList();

                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            }
            catch { }
        }

        public bool WriteHash(string Hash, string Data, string Collection)
        {
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

            try
            {
                if (!RethinkTables.Contains(Collection))
                {
                    try
                    {
                        lock (locker) //only one write operation
                        {
                            R.TableCreate(Collection).OptArg("primary_key", "#id").Run(conn);
                            RethinkTables.Add(Collection);
                        }
                    }
                    catch { }
                }
                JObject jObj = JObject.Parse(Data);

                if (jObj["#id"] == null)
                    jObj.Add("#id", Hash);

                switch (Collection)
                {
                    case "_chain":
                        var iR = R.Table(Collection).Insert(jObj).RunAtom<JObject>(conn); // Update
                        break;
                    case "_full":
                        if (CacheFull)
                        {
                            R.Table(Collection).Insert(jObj).RunAtomAsync<JObject>(conn); // Update
                        }
                        string sID = jObj["#id"].ToString();

                        if (CacheKeys)
                        {
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
                                                    /*string sDir = Path.Combine(FilePath, "_key", oSub.Name.ToLower().TrimStart('#'));

                                                    //Remove invalid Characters in Path
                                                    foreach (var sChar in Path.GetInvalidPathChars())
                                                    {
                                                        sDir = sDir.Replace(sChar.ToString(), "");
                                                    }

                                                    if (!Directory.Exists(sDir))
                                                        Directory.CreateDirectory(sDir);

                                                    File.WriteAllText(Path.Combine(sDir, oSubSub.ToString() + ".json"), sID);
                                                    */
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

                        break;
                    case "_assets":
                        R.Table(Collection).Insert(jObj).RunAtomAsync<JObject>(conn); // Update
                        break;
                    default:
                        R.Table(Collection).Insert(jObj).RunAsync<JObject>(conn); // Insert Async
                        break;

                }

                if (ContinueAfterWrite)
                    return false;
                else
                    return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public string ReadHash(string Hash, string Collection)
        {
            string sResult = "";

            Collection = Collection.ToLower();

            try
            {
                JObject oRes = R.Table(Collection).Get(Hash).Run<JObject>(conn);
                if (oRes != null)
                    sResult = oRes.ToString();

                return sResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            return sResult;
        }

        public int totalDeviceCount(string sPath = "")
        {
            return -1;
        }

        public IEnumerable<JObject> GetRawAssets(string paths)
        {
            foreach (var oAsset in R.Table("_assets").GetAll().Run<JObject>(conn))
            {
                JObject jObj = oAsset;

                if (paths.Contains("*") || paths.Contains(".."))
                {
                    jObj = jaindb.jDB.GetFull(jObj["#id"].Value<string>(), jObj["_index"].Value<int>());
                }
                yield return jObj;
            }
        }

        public string LookupID(string name, string value)
        {
            string sResult = null;
            try
            {
                JObject jRes = R.Table("_key").Get(name.ToLower().TrimStart('#', '@') + "/" + value.ToLower()).Run<JObject>(conn);
                sResult = jRes["value"].Value<string>();
            }
            catch { }

            return sResult;
        }

        public bool WriteLookupID(string name, string value, string id)
        {
            if (!RethinkTables.Contains("_key"))
            {
                try
                {
                    lock (locker) //only one write operation
                    {
                        R.TableCreate("_key").OptArg("primary_key", "name").Run(conn);
                        RethinkTables.Add("_key");
                    }
                }
                catch { }
            }

            JObject jObj = new JObject();
            jObj.Add("value", id);
            jObj.Add("name", name.ToLower().TrimStart('#', '@') + "/" + value.ToLower());
            R.Table("_key").Insert(jObj).RunAtomAsync<JObject>(conn);

            return false;
        }

        public List<string> GetAllIDs()
        {
            List<string> lResult = new List<string>();

            try
            {
                foreach (var oObj in R.Table("_chain").GetAll().Run<JObject>(conn))
                {
                    lResult.Add(oObj.ToString());
                }
            }
            catch { }

            return lResult;
        }
    }


}

