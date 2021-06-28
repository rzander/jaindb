using jaindb;
using JainDBProvider;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Plugin_FileStore
{
    public class Plugin_FileStore : IStore
    {
        private static readonly object locker = new object();
        private bool bReadOnly = false;
        private bool bWriteOnly = false;
        private bool CacheFull = true;
        private bool CacheKeys = true;
        private bool ContinueAfterWrite = true;
        private string FilePath = "";
        private string foldername = "jaindb";
        private JObject JConfig = new JObject();

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

        public async IAsyncEnumerable<JObject> GetRawAssetsAsync(string paths)
        {
            foreach (var oFile in new DirectoryInfo(Path.Combine(FilePath, "_assets")).GetFiles("*.json"))
            {
                JObject jObj = new JObject();

                if (paths.Contains("*") || paths.Contains(".."))
                {
                    try
                    {
                        //tbv
                        jObj = new JObject(File.ReadAllText(oFile.FullName));
                        jObj = await jDB.GetFullAsync(jObj["#id"].Value<string>(), jObj["_index"].Value<int>());
                    }
                    catch { }
                }
                else
                {
                    var oAsset = await jDB.ReadHashAsync(oFile.FullName.Replace(oFile.Extension, ""), "_assets");
                    if (!string.IsNullOrEmpty(paths))
                        jObj = await jDB.GetRawAsync(oAsset, paths); //load only the path
                    else
                        jObj = JObject.Parse(oAsset); //if not paths, we only return the raw data
                }

                if (jObj["_hash"] == null)
                    jObj.Add(new JProperty("_hash", oFile.FullName));

                yield return jObj;
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
                    File.WriteAllText(Assembly.GetExecutingAssembly().Location.Replace(".dll", ".json"), Properties.Resources.Plugin_FileStore);
                }

                if (File.Exists(Assembly.GetExecutingAssembly().Location.Replace(".dll", ".json")))
                {
                    JConfig = JObject.Parse(File.ReadAllText(Assembly.GetExecutingAssembly().Location.Replace(".dll", ".json")));
                    bReadOnly = JConfig["ReadOnly"].Value<bool>();
                    ContinueAfterWrite = JConfig["ContinueAfterWrite"].Value<bool>();
                    CacheFull = JConfig["CacheFull"].Value<bool>();
                    CacheKeys = JConfig["CacheKeys"].Value<bool>();
                    foldername = JConfig["foldername"].Value<string>();
                }
                else
                {
                    JConfig = new JObject();
                }
            }
            catch { }

            FilePath = Path.Combine(Settings["wwwPath"] ?? "", foldername);
            if (!Directory.Exists(FilePath))
                Directory.CreateDirectory(FilePath);
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

        public string ReadHash(string Hash, string Collection)
        {
            string sResult = "";
            if (bWriteOnly)
                return sResult;

            try
            {
                string Coll2 = Collection;
                //Remove invalid Characters in Path anf File
                foreach (var sChar in Path.GetInvalidPathChars())
                {
                    Coll2 = Coll2.Replace(sChar.ToString(), "");
                    Hash = Hash.Replace(sChar.ToString(), "");
                }

                if (!File.Exists(Path.Combine(FilePath, Coll2, Hash + ".json")))
                    return "";

                sResult = File.ReadAllText(Path.Combine(FilePath, Coll2, Hash + ".json"));


                //Check if hashes are valid...
                //if (Collection != "_full" && Collection != "_chain" && Collection != "_assets")
                //{
                //    var jData = JObject.Parse(sResult);
                //    /*if (jData["#id"] != null)
                //        jData.Remove("#id");*/
                //    if (jData["_date"] != null)
                //        jData.Remove("_date");
                //    if (jData["_index"] != null)
                //        jData.Remove("_index");

                //    string s1 = jaindb.jDB.CalculateHash(jData.ToString(Newtonsoft.Json.Formatting.None));
                //    if (Hash != s1)
                //    {
                //        s1.ToString();
                //        return "";
                //    }
                //}

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

        public bool WriteHash(string Hash, string Data, string Collection)
        {
            Collection = Collection.ToLower();

            if (bReadOnly)
                if (ContinueAfterWrite)
                    return false;
                else
                    return true;

            //Remove invalid Characters in Path and Hash
            foreach (var sChar in Path.GetInvalidPathChars())
            {
                Collection = Collection.Replace(sChar.ToString(), "");
                Hash = Hash.Replace(sChar.ToString(), "");
            }

            string sCol = Path.Combine(FilePath, Collection);
            if (!Directory.Exists(sCol))
                Directory.CreateDirectory(sCol);

            switch (Collection)
            {
                case "_full":

                    if (!CacheFull) //exit if ChacheFull is not set
                    {
                        if (ContinueAfterWrite)
                            return false;
                        else
                            return true;
                    }

                    var jObj = JObject.Parse(Data);
                    jaindb.jDB.JSort(jObj);

                    string sID = jObj["#id"].ToString();

                    if (!Directory.Exists(Path.Combine(FilePath, "_key")))
                        Directory.CreateDirectory(Path.Combine(FilePath, "_key"));

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
                                                /*string sDir = Path.Combine(FilePath, "_key", oSub.Name.ToLower().TrimStart('#'));

                                                //Remove invalid Characters in Path
                                                foreach (var sChar in Path.GetInvalidPathChars())
                                                {
                                                    sDir = sDir.Replace(sChar.ToString(), "");
                                                }

                                                if (!Directory.Exists(sDir))
                                                    Directory.CreateDirectory(sDir);

                                                File.WriteAllText(Path.Combine(sDir, (string)oSub.Value + ".json"), sID);
                                                */
                                            }
                                            catch { }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    lock (locker) //only one write operation
                    {
                        File.WriteAllText(Path.Combine(FilePath, Collection, Hash + ".json"), Data);
                    }
                    break;

                case "_chain":
                    lock (locker) //only one write operation
                    {
                        File.WriteAllText(Path.Combine(FilePath, Collection, Hash + ".json"), Data);
                    }
                    break;

                default:
                    if (!File.Exists(Path.Combine(FilePath, Collection, Hash + ".json"))) //We do not have to create the same hash file twice...
                    {
                        lock (locker) //only one write operation
                        {
                            File.WriteAllText(Path.Combine(FilePath, Collection, Hash + ".json"), Data);
                        }
                    }
                    break;
            }


            if (ContinueAfterWrite)
                return false;
            else
                return true;
        }
        public bool WriteLookupID(string name, string value, string id)
        {
            if (bReadOnly)
                return false;

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
    }
}

