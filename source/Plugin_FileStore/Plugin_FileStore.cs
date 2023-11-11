using jaindb;
using JainDBProvider;
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

        public async Task<List<string>> GetAllIDsAsync(CancellationToken ct = default(CancellationToken))
        {
            return await Task.Run(() =>
            {
                List<string> lResult = new List<string>();

                try
                {
                    if (Directory.Exists(Path.Combine(FilePath, "_chain")))
                    {
                        foreach (var oFile in new DirectoryInfo(Path.Combine(FilePath, "_chain")).GetFiles("*.json"))
                        {
                            if (ct.IsCancellationRequested)
                                throw new TaskCanceledException();

                            lResult.Add(System.IO.Path.GetFileNameWithoutExtension(oFile.Name));
                        }
                    }
                }
                catch { }

                return lResult;
            });
        }

        public async IAsyncEnumerable<JObject> GetRawAssetsAsync(string paths, [EnumeratorCancellation] CancellationToken ct = default(CancellationToken))
        {
            foreach (var oFile in new DirectoryInfo(Path.Combine(FilePath, "_assets")).GetFiles("*.json"))
            {
                JObject jObj = new JObject();

                if (paths.Contains('*') || paths.Contains(".."))
                {
                    try
                    {
                        //tbv
                        jObj =  new JObject(await File.ReadAllTextAsync(oFile.FullName, ct));
                        jObj = await jDB.GetFullAsync(jObj["#id"].Value<string>(), jObj["_index"].Value<int>(), "", false, ct);
                    }
                    catch { }
                }
                else
                {
                    var oAsset = await jDB.ReadHashAsync(oFile.FullName.Replace(oFile.Extension, ""), "_assets", ct);
                    if (!string.IsNullOrEmpty(paths))
                        jObj = await jDB.GetRawAsync(oAsset, paths, ct); //load only the path
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

        public async Task<string> LookupIDAsync(string name, string value, CancellationToken ct = default(CancellationToken))
        {
            string sResult = "";
            try
            {
                sResult = await File.ReadAllTextAsync(Path.Combine(FilePath, "_key", name.TrimStart('#', '@'), value + ".json"), ct);
            }
            catch { }

            return sResult;
        }
                
        public async Task<string> ReadHashAsync(string Hash, string Collection, CancellationToken ct = default(CancellationToken))
        {
            string sResult = "";
            if (bWriteOnly)
                return sResult;

            //string PartitionKey = "";

            if (Hash.Contains(';'))
            {
                //PartitionKey = Hash.Split(';')[1];
                Hash = Hash.Split(';')[0];
            }

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

                sResult = await File.ReadAllTextAsync(Path.Combine(FilePath, Coll2, Hash + ".json"), ct);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error ReadHash_1: " + ex.Message.ToString());
            }

            return sResult;
        }

        public static string RemoveInvalidChars(string filename)
        {
            return string.Concat(filename.Split(Path.GetInvalidFileNameChars()));
        }

        public async Task<int> totalDeviceCountAsync(string sPath = "", CancellationToken ct = default(CancellationToken))
        {
            return await Task.Run(() =>
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
            });
        }

        public async Task<bool> WriteHashAsync(string Hash, string Data, string Collection, CancellationToken ct = default(CancellationToken))
        {
            try
            {
                Collection = Collection.ToLower();

                if (bReadOnly)
                    if (ContinueAfterWrite)
                        return false;
                    else
                        return true;

                string PartitionKey = "";

                if (Hash.Contains(';'))
                {
                    PartitionKey = Hash.Split(';')[1];
                    Hash = Hash.Split(';')[0];
                }

                Collection = RemoveInvalidChars(Collection);
                Hash = RemoveInvalidChars(Hash);



                string sCol = Path.Combine(FilePath, Collection);
                if (!Directory.Exists(sCol))
                {
                    Directory.CreateDirectory(sCol);
                }

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
                        await jDB.JSortAsync(jObj, false, ct);

                        string sID = jObj["#id"].ToString();

                        if (CacheKeys)
                        {
                            if (!Directory.Exists(Path.Combine(FilePath, "_key")))
                                Directory.CreateDirectory(Path.Combine(FilePath, "_key"));

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
                                                    _ = WriteLookupIDAsync(oSub.Name.ToLower(), (string)oSub.Value, sID, ct);
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
                                                    _ = WriteLookupIDAsync(oSub.Name.ToLower(), (string)oSub.Value, sID, ct);
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
            catch { }

            return false;
        }

        public async Task<bool> WriteLookupIDAsync(string name, string value, string id, CancellationToken ct = default(CancellationToken))
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

                await Task.CompletedTask;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

