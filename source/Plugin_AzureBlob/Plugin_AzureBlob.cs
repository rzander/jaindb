using JainDBProvider;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using System.Threading.Tasks;
using jaindb;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;

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

        CloudStorageAccount storageAccount;
        CloudBlobClient blobClient;

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

                storageAccount = new CloudStorageAccount(new Microsoft.Azure.Storage.Auth.StorageCredentials(StorageAccount, AccessKey), true);
                blobClient = storageAccount.CreateCloudBlobClient();
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

            var jObj = JObject.Parse(Data);


            // Get a reference to a container named "my-new-container."
            CloudBlobContainer container = blobClient.GetContainerReference(sColl);

            // If "mycontainer" doesn't exist, create it.
            container.CreateIfNotExistsAsync().Wait();

            if (sColl == "assets")
            {
                CloudBlockBlob blockBlob = container.GetBlockBlobReference(jObj["_hash"].Value<string>());
                if(!blockBlob.Exists())
                    blockBlob.UploadText(Data);
            }
            else
            {
                CloudBlockBlob blockBlob = container.GetBlockBlobReference(Hash);
                if (!blockBlob.Exists())
                    blockBlob.UploadText(Data);
                else
                {
                    string sOLD = blockBlob.DownloadText();
                    if(Data.Length > sOLD.Length) //only update if the new chain is larger
                        blockBlob.UploadText(Data);
                }
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
                switch (Collection)
                {
                    case "_assets":
                        sColl = "assets";
                        break;
                    case "_chain":
                        sColl = "chain";
                        break;
                    default:
                        return "";
                }

                CloudBlobContainer container = blobClient.GetContainerReference(sColl);
                CloudBlockBlob blockBlob = container.GetBlockBlobReference(Hash);
                sResult = blockBlob.DownloadText();
            }
            catch { }
            return sResult;
        }

        public int totalDeviceCount(string sPath = "")
        {
            int iCount = -1;
            try
            {
                CloudBlobContainer container = blobClient.GetContainerReference("_chain");
                iCount = container.ListBlobs().Count();
            }
            catch { }

            return iCount;
        }

        public async IAsyncEnumerable<JObject> GetRawAssetsAsync(string paths)
        {
            //paths = "*"; //Azure Blob store full assets only
            CloudBlobContainer container = blobClient.GetContainerReference("assets");

            foreach (var bAsset in container.ListBlobs())
            {
                if (bAsset.GetType() == typeof(CloudBlockBlob))
                {
                    CloudBlockBlob blob = (CloudBlockBlob)bAsset;

                    JObject jObj = new JObject();
                    jObj = JObject.Parse(blob.DownloadText());

                    if (jObj["_hash"] == null)
                        jObj.Add(new JProperty("_hash", blob.Name));

                    yield return jObj;
                }
                else
                {
                    continue;
                }
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

            CloudBlobContainer container = blobClient.GetContainerReference("chain");

            foreach (var oAsset in container.ListBlobs())
            {
                if (oAsset.GetType() == typeof(CloudBlockBlob))
                {
                    CloudBlockBlob blob = (CloudBlockBlob)oAsset;
                    lResult.Add(blob.Name);
                }
                else
                {
                    continue;
                }
            }

            return lResult;
        }
    }


}

