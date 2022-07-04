using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using jaindb;
using JainDBProvider;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Plugin_AzureBlob
{
    public class Plugin_AzureBlob : IStore
    {
        private string AccessKey = "";
        private bool bAssets = true;
        private bool bBlocks = true;
        private bool bChain = true;
        private bool bFull = false;
        private BlobClient blobClient;
        private string blobcontainer = "jaindb";
        private bool bReadOnly = false;
        private BlobContainerClient container;
        private bool ContinueAfterWrite = true;
        private JObject JConfig = new JObject();
        private int maxAgeDays = 90;
        private string StorageAccount = "";

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
            List<string> lResult = new List<string>();

            DateTime dStart = DateTime.Now;

            await foreach (BlobItem bChain in container.GetBlobsAsync(BlobTraits.None, BlobStates.None, "_chain", ct))
            {
                try
                {
                    if (maxAgeDays > 0)
                    {
                        if ((DateTimeOffset.Now - ((DateTimeOffset)bChain.Properties.LastModified)).TotalDays <= maxAgeDays)
                            lResult.Add(Path.GetFileNameWithoutExtension(bChain.Name.Split('/')[1]));
                    }
                    else
                    {
                        lResult.Add(Path.GetFileNameWithoutExtension(bChain.Name.Split('/')[1]));
                    }
                }
                catch { }
            }

            DateTime dEnd = DateTime.Now;
            Console.WriteLine("Loading " + lResult.Count + " ID's duration: " + (dEnd - dStart).TotalMilliseconds.ToString());

            return lResult;
        }

        public async IAsyncEnumerable<JObject> GetRawAssetsAsync(string paths, [EnumeratorCancellation] CancellationToken ct = default(CancellationToken))
        {
            await foreach (var bAsset in container.GetBlobsAsync(BlobTraits.None, Azure.Storage.Blobs.Models.BlobStates.None, "_assets", ct))
            {
                if (ct.IsCancellationRequested)
                    throw new TaskCanceledException();

                blobClient = container.GetBlobClient(bAsset.Name);
                JObject jObj = new JObject();

                if (paths.Contains("*") || paths.Contains("..")) //?
                {
                    try
                    {
                        var dca = await blobClient.DownloadContentAsync();

                        JObject jObjRaw = new JObject(dca.Value.Content.ToString());
                        jObj = await jDB.GetFullAsync(jObjRaw["#id"].Value<string>(), jObjRaw["_index"].Value<int>(), "", false, ct);
                    }
                    catch { }
                }
                else
                {
                    var oAsset = await jDB.ReadHashAsync(bAsset.Name.Split('/')[1], "_assets", ct);
                    if (!string.IsNullOrEmpty(paths))
                        jObj = await jDB.GetRawAsync(oAsset, paths, ct); //load only the path
                    else
                        jObj = JObject.Parse(oAsset); //if not paths, we only return the raw data
                }

                if (jObj["_hash"] == null)
                    jObj.Add(new JProperty("_hash", bAsset.Name.Split('/')[1]));

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
                    File.WriteAllText(Assembly.GetExecutingAssembly().Location.Replace(".dll", ".json"), Properties.Resources.Plugin_AzureBlob);
                }

                if (File.Exists(Assembly.GetExecutingAssembly().Location.Replace(".dll", ".json")))
                {
                    JConfig = JObject.Parse(File.ReadAllText(Assembly.GetExecutingAssembly().Location.Replace(".dll", ".json")));
                    bReadOnly = JConfig["ReadOnly"].Value<bool>();
                    ContinueAfterWrite = JConfig["ContinueAfterWrite"].Value<bool>();
                    StorageAccount = JConfig["StorageAccount"].Value<string>();
                    AccessKey = JConfig["AccessKey"].Value<string>();

                    if (JConfig["Assets"] != null)
                        bAssets = JConfig["Assets"].Value<bool>();

                    if (JConfig["Blocks"] != null)
                        bBlocks = JConfig["Blocks"].Value<bool>();

                    if (JConfig["Chain"] != null)
                        bChain = JConfig["Chain"].Value<bool>();

                    if (JConfig["Full"] != null)
                        bFull = JConfig["Full"].Value<bool>();

                    if (JConfig["BlobContainer"] != null)
                        blobcontainer = JConfig["BlobContainer"].Value<string>();

                    if (JConfig["InvMaxAgeDays"] != null)
                        maxAgeDays = JConfig["InvMaxAgeDays"].Value<int>();
                }
                else
                {
                    JConfig = new JObject();
                }

                container = new BlobContainerClient($"DefaultEndpointsProtocol=https;AccountName={StorageAccount};AccountKey={AccessKey};EndpointSuffix=core.windows.net", blobcontainer);
            }
            catch (Exception ex)
            {
                Console.WriteLine("AzureBlob Error: " + ex.Message);
            }
        }

        public async Task<string> LookupIDAsync(string name, string value, CancellationToken ct = default(CancellationToken))
        {
            await Task.CompletedTask;
            return null;
        }

        public async Task<string> ReadHashAsync(string Hash, string Collection, CancellationToken ct = default(CancellationToken))
        {
            string sResult = "";
            if (string.IsNullOrEmpty(Hash))
                return null;
            try
            {
                Collection = Collection.ToLower();

                Collection = RemoveInvalidChars(Collection);
                Hash = RemoveInvalidChars(Hash);

                string sColl = Collection;

                blobClient = container.GetBlobClient(sColl + "/" + Hash + ".json");
                if (blobClient != null)
                {
                    var oRes = await blobClient.DownloadContentAsync();
                    if (oRes != null)
                    {
                        if (oRes.Value.Content.GetType() == typeof(BinaryData))
                        {
                            sResult = Encoding.ASCII.GetString(oRes.Value.Content);
                        }
                        else
                        {
                            sResult = oRes.Value.Content.ToString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading hash {Hash} from {Collection} .");
            }

            return sResult;
        }

        public string RemoveInvalidChars(string filename)
        {
            if (!string.IsNullOrEmpty(filename))
                return string.Concat(filename.Split(Path.GetInvalidFileNameChars()));
            else
                return null;
        }

        public async Task<int> totalDeviceCountAsync(string sPath = "", CancellationToken ct = default(CancellationToken))
        {
            return await Task.Run(() => {
                int iCount = -1;
                try
                {
                    var blobs = container.GetBlobs(Azure.Storage.Blobs.Models.BlobTraits.None, Azure.Storage.Blobs.Models.BlobStates.None, "_chain", ct);
                    iCount = blobs.Count();
                }
                catch { }

                return iCount;
            });
        }

        public async Task<bool> WriteHashAsync(string Hash, string Data, string Collection, CancellationToken ct = default(CancellationToken))
        {
            try
            {
                if (bReadOnly)
                    return false;

                if (ct.IsCancellationRequested)
                    return false;

                if (string.IsNullOrEmpty(Data) || Data == "null")
                    return true;

                Collection = Collection.ToLower();

                Collection = RemoveInvalidChars(Collection);
                Hash = RemoveInvalidChars(Hash);

                string sColl = Collection;

                if (sColl.StartsWith('_'))
                {
                    if (sColl == "_full" && bFull)
                    {
                        //always upload...
                        blobClient = container.GetBlobClient(sColl + "/" + Hash + ".json");
                        await blobClient.UploadAsync(new BinaryData(Data), true, ct);
                    }

                    if (sColl == "_chain" && bChain)
                    {
                        //always upload...
                        blobClient = container.GetBlobClient(sColl + "/" + Hash + ".json");
                        await blobClient.UploadAsync(new BinaryData(Data), true, ct);
                    }

                    if (sColl == "_assets" && bAssets)
                    {
                        //only upload if not exists...
                        blobClient = container.GetBlobClient(sColl + "/" + Hash + ".json");
                        await blobClient.UploadAsync(new BinaryData(Data), false, ct);
                    }
                }
                else
                {
                    if (bBlocks)
                    {
                        //only upload if not exists...
                        blobClient = container.GetBlobClient(sColl + "/" + Hash + ".json");
                        await blobClient.UploadAsync(new BinaryData(Data), false, ct);
                    }
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
            await Task.CompletedTask;
            if (bReadOnly)
                return false;

            try
            {
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}