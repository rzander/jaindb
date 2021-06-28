using JainDBProvider;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using System.Threading.Tasks;
using jaindb;
using Azure.Storage.Blobs;

namespace Plugin_AzureBlob
{
    public class Plugin_AzureBlob : IStore
    {
        private bool bReadOnly = false;
        private bool ContinueAfterWrite = true;
        private string StorageAccount = "";
        private string AccessKey = "";

        private JObject JConfig = new JObject();

        public Dictionary<string, string> Settings { get; set; }

        BlobClient blobClient;
        BlobContainerClient container;

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
                    File.WriteAllText(Assembly.GetExecutingAssembly().Location.Replace(".dll", ".json"), Properties.Resources.Plugin_AzureBlob);
                }

                if (File.Exists(Assembly.GetExecutingAssembly().Location.Replace(".dll", ".json")))
                {
                    JConfig = JObject.Parse(File.ReadAllText(Assembly.GetExecutingAssembly().Location.Replace(".dll", ".json")));
                    bReadOnly = JConfig["ReadOnly"].Value<bool>();
                    ContinueAfterWrite = JConfig["ContinueAfterWrite"].Value<bool>();
                    StorageAccount = JConfig["StorageAccount"].Value<string>();
                    AccessKey = JConfig["AccessKey"].Value<string>();
                }
                else
                {
                    JConfig = new JObject();
                }

                container = new BlobContainerClient($"DefaultEndpointsProtocol=https;AccountName={ StorageAccount };AccountKey={ AccessKey };EndpointSuffix=core.windows.net", "jaindb");
                //blobClient = container.GetBlobClient("blobName");
                //storageAccount = new CloudStorageAccount(new Microsoft.Azure.Storage.Auth.StorageCredentials(StorageAccount, AccessKey), true);
                //blobClient = storageAccount.CreateCloudBlobClient();
            }
            catch(Exception ex)
            {
                Console.WriteLine("AzureBlob Error: " + ex.Message);
            }
        }

        public bool WriteHash(string Hash, string Data, string Collection)
        {
            if (bReadOnly)
                return false;

            if (string.IsNullOrEmpty(Data) || Data == "null")
                return true;

            Collection = Collection.ToLower();
            string sColl = Collection;

            if (sColl.StartsWith('_'))
            {
                //always upload...
                blobClient = container.GetBlobClient(sColl + "/" + Hash);
                blobClient.UploadAsync(new BinaryData(Data));
            }
            else
            {
                //only upload if not exists...
                blobClient = container.GetBlobClient(sColl + "/" + Hash);
                if (!blobClient.Exists())
                    blobClient.UploadAsync(new BinaryData(Data));
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
                Collection = Collection.ToLower();
                string sColl = Collection;

                blobClient = container.GetBlobClient(sColl + "/" + Hash);
                sResult = blobClient.DownloadContent().Value.Content.ToString();
            }
            catch { }
            return sResult;
        }

        public int totalDeviceCount(string sPath = "")
        {
            int iCount = -1;
            try
            {
                iCount = container.GetBlobs(Azure.Storage.Blobs.Models.BlobTraits.None, Azure.Storage.Blobs.Models.BlobStates.None, "_chain").ToList().Count;
            }
            catch { }

            return iCount;
        }

        public async IAsyncEnumerable<JObject> GetRawAssetsAsync(string paths)
        {
            foreach (var bAsset in container.GetBlobs(Azure.Storage.Blobs.Models.BlobTraits.None, Azure.Storage.Blobs.Models.BlobStates.None, "_assets"))
            {
                blobClient = container.GetBlobClient(bAsset.Name);
                JObject jObj = new JObject();

                if (paths.Contains("*") || paths.Contains(".."))
                {
                    try
                    {
                        string jRes = blobClient.DownloadContent().Value.Content.ToString();
                        jObj = new JObject(jRes);
                        jObj = await jDB.GetFullAsync(jObj["#id"].Value<string>(), jObj["_index"].Value<int>());
                    }
                    catch { }
                }
                else
                {
                    var oAsset = await jDB.ReadHashAsync(bAsset.Name.Split('/')[1], "_assets");
                    if (!string.IsNullOrEmpty(paths))
                        jObj = await jDB.GetRawAsync(oAsset, paths); //load only the path
                    else
                        jObj = JObject.Parse(oAsset); //if not paths, we only return the raw data
                }

                if (jObj["_hash"] == null)
                    jObj.Add(new JProperty("_hash", bAsset.Name.Split('/')[1]));

                yield return jObj;
            }
        }

        public string LookupID(string name, string value)
        {
            string sResult = null;
            return sResult;
        }

        public bool WriteLookupID(string name, string value, string id)
        {
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

        public List<string> GetAllIDs()
        {
            List<string> lResult = new List<string>();


            foreach (var bChain in container.GetBlobs(Azure.Storage.Blobs.Models.BlobTraits.None, Azure.Storage.Blobs.Models.BlobStates.None, "_chain"))
            {
                lResult.Add(bChain.Name.Split('/')[1]);
            }

            return lResult;
        }
    }


}

