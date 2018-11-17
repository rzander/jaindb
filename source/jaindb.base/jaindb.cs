// ************************************************************************************
//          jaindb (c) Copyright 2018 by Roger Zander
// ************************************************************************************

using JainDBProvider;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using static jaindb.BlockChain;

namespace jaindb
{
    public static class jDB
    {
        public enum hashType { MD5, SHA2_256 } //Implemented Hash types
        private static readonly object locker = new object();
        private static HttpClient oClient = new HttpClient();

        public static bool UseCosmosDB;
        public static bool UseRedis;
        public static bool UseFileStore;
        public static string FilePath = "wwwroot";
        public static string wwwPath = "wwwroot";
        public static bool UseRethinkDB;

        public static hashType HashType = hashType.MD5;

        public static string BlockType = "INV";
        public static int PoWComplexitity = 0; //Proof of Work complexity; 0 = no PoW; 8 = 8 trailing bits of the block hash must be '0'
        public static bool ReadOnly = false;






        internal static Dictionary<string, IStore> _Plugins = new Dictionary<string, IStore>();

        public static void loadPlugins(string PluginPath = "")
        {
            if (string.IsNullOrEmpty(PluginPath))
                PluginPath = AppDomain.CurrentDomain.BaseDirectory;

            ICollection<IStore> plugins = GenericPluginLoader<IStore>.LoadPlugins(PluginPath);
            foreach (var item in plugins)
            {
                _Plugins.Add(item.Name, item);
                Console.WriteLine(item.Name);
                item.Settings = new Dictionary<string, string>();
                item.Settings.Add("FilePath", FilePath);
                item.Settings.Add("wwwPath", wwwPath);
                item.Init();
            }
        }

        public static string CalculateHash(string input)
        {
            switch (HashType)
            {
                case hashType.MD5:
                    return Hash.CalculateMD5HashString(input);
                case hashType.SHA2_256:
                    return Hash.CalculateSHA2_256HashString(input);
                default:
                    return Hash.CalculateMD5HashString(input); ;
            }
        }


        /// <summary>
        /// Lookup Key ID's to search for Objects based on their Key
        /// </summary>
        /// <param name="name">Key name to search. E.g. "name"</param>
        /// <param name="value">Value to search. E.g. "computer01"</param>
        /// <returns>Hash ID of the Object</returns>
        public static string LookupID(string name, string value)
        {
            string sResult = "";
            try
            {
                foreach (var item in _Plugins.OrderBy(t => t.Key))
                {
                    try
                    {
                        sResult = item.Value.LookupID(name, value);

                        if (!string.IsNullOrEmpty(sResult))
                        {
                            //Write Hash to the first Plugin if the current plugin is not the first one
                            if (item.Key != _Plugins.OrderBy(t => t.Key).FirstOrDefault().Key)
                            {
                                _Plugins.OrderBy(t => t.Key).FirstOrDefault().Value.WriteLookupID(name, value, sResult);
                            }
                            return sResult;
                        }
                    }
                    catch { }
                }


                return sResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error LookupID_1: " + ex.Message.ToString());
            }

            return sResult;
        }

        public static void WriteHash(ref JToken oRoot, ref JObject oStatic, string Collection)
        {
            Collection = Collection.ToLower(); //all lowercase

            if (ReadOnly)
                return;
            try
            {
                if (!oRoot.HasValues)
                    return;

                //JSort(oStatic);
                string sHash = CalculateHash(oRoot.ToString(Newtonsoft.Json.Formatting.None));

                string sPath = oRoot.Path;

                var oClass = oStatic.SelectToken(sPath);// as JObject;

                if (oClass != null)
                {
                    if (oClass.Type == JTokenType.Object)
                    {
                        if (oClass["##hash"] == null)
                        {
                            ((JObject)oClass).Add("##hash", sHash);
                            if (!UseCosmosDB)
                            {
                                if (UseFileStore)
                                    WriteHash(sHash, oRoot.ToString(Formatting.None), Collection); //not async, it's faster
                                if (UseRedis)
                                    WriteHash(sHash, oRoot.ToString(Formatting.None), Collection); //not async, it's faster
                                if (UseRethinkDB)
                                    WriteHash(sHash, oRoot.ToString(Formatting.None), Collection); //not async, it's faster
                            }
                            else
                            {
                                //No need to save hashes when using CosmosDB
                                //WriteHashAsync(sHash, oRoot.ToString(Formatting.None), Collection).Wait();
                            }
                            oRoot = oClass;
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                ex.Message.ToString();
            }
        }

        public static bool WriteHash(string Hash, string Data, string Collection)
        {
            Collection = Collection.ToLower();

            if (ReadOnly)
                return false;

            try
            {
                foreach (var item in _Plugins.OrderBy(t=>t.Key))
                {
                    try
                    {
                        if (item.Value.WriteHash(Hash, Data, Collection))
                            return true; //exit if return value is true
                    }
                    catch { }
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                if (!Directory.Exists(Path.Combine(FilePath, Collection)))
                    Directory.CreateDirectory(Path.Combine(FilePath, Collection));

                if (!File.Exists(Path.Combine(FilePath, Collection, Hash + ".json"))) //We do not have to create the same hash file twice...
                {
                    lock (locker) //only one write operation
                    {
                        File.WriteAllText(Path.Combine(FilePath, Collection, Hash + ".json"), Data);
                    }
                }

                return true;
            }
        }

        public static async Task<bool> WriteHashAsync(string Hash, string Data, string Collection)
        {
            if (ReadOnly)
                return false;

            //return WriteHash(Hash, Data, Collection);
            //write async
            return await Task.Run(() =>
            {
                return WriteHash(Hash, Data, Collection);
            });
        }

        public static string ReadHash(string Hash, string Collection)
        {
            string sResult = "";
            Collection = Collection.ToLower();

            try
            {
                foreach (var item in _Plugins.OrderBy(t => t.Key))
                {
                    try
                    {
                        sResult = item.Value.ReadHash(Hash, Collection);

                        if (!string.IsNullOrEmpty(sResult))
                        {
                            //Write Hash to the first Plugin if the current plugin is not the first one
                            if(item.Key != _Plugins.OrderBy(t => t.Key).FirstOrDefault().Key)
                            {
                                _Plugins.OrderBy(t => t.Key).FirstOrDefault().Value.WriteHash(Hash, sResult, Collection);
                            }
                            return sResult;
                        }
                    }
                    catch { }
                }

                return sResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error ReadHash_1: " + ex.Message.ToString());
            }

            return sResult;
        }

        public static Blockchain GetChain(string DeviceID)
        {
            Blockchain oChain;
            string sData = "";
            if(!UseCosmosDB)
                sData = ReadHash(DeviceID, "_chain");
            else
                sData = ReadHash(DeviceID, "chain");

            if (string.IsNullOrEmpty(sData))
            {
                oChain = new Blockchain("", "root", 0);
            }
            else
            {
                JsonSerializerSettings oSettings = new JsonSerializerSettings();
                var oC = JsonConvert.DeserializeObject(sData, typeof(Blockchain), oSettings);
                oChain = oC as Blockchain;
            }

            return oChain;
        }

        public static JObject Deduplicate(JObject FullObject)
        {
            JObject oObj = FullObject;
            JObject oStatic = oObj.ToObject<JObject>();

            //Loop through all ChildObjects
            foreach (var oChild in oObj.Descendants().Where(t => t.Type == JTokenType.Object).Reverse())
            {
                try
                {
                    JToken tRef = oObj.SelectToken(oChild.Path, false);

                    //check if tRfe is valid..
                    if (tRef == null)
                        continue;


                    string sName = "misc";
                    if (oChild.Parent.Type == JTokenType.Property)
                        sName = ((Newtonsoft.Json.Linq.JProperty)oChild.Parent).Name;
                    else
                        sName = ((Newtonsoft.Json.Linq.JProperty)oChild.Parent.Parent).Name; //it's an array

                    if (sName.StartsWith('@'))
                        continue;

                    foreach (JProperty jProp in oStatic.SelectToken(oChild.Path).Children().Where(t => t.Type == JTokenType.Property).ToList())
                    {
                        try
                        {
                            if (!jProp.Name.StartsWith('#'))
                            {
                                if (jProp.Descendants().Where(t => t.Type == JTokenType.Property && ((JProperty)t).Name.StartsWith("#")).Count() == 0)
                                {
                                    jProp.Remove();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("Error UploadFull_2: " + ex.Message.ToString());
                        }
                    }


                    //remove all # and @ attributes
                    foreach (var oKey in tRef.Parent.Descendants().Where(t => t.Type == JTokenType.Property && (((JProperty)t).Name.StartsWith("#") || ((JProperty)t).Name.StartsWith("@"))).ToList())
                    {
                        try
                        {
                            oKey.Remove();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("Error UploadFull_3: " + ex.Message.ToString());
                        }
                    }

                    WriteHash(ref tRef, ref oStatic, sName);
                    oObj.SelectToken(oChild.Path).Replace(tRef);

                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error UploadFull_4: " + ex.Message.ToString());
                }
            }

            //remove all # and @ objects
            foreach (var oKey in oStatic.Descendants().Where(t => t.Type == JTokenType.Property && ((JProperty)t).Name.StartsWith("@")).ToList())
            {
                try
                {
                    oKey.Remove();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error UploadFull_5: " + ex.Message.ToString());
                }
            }

            return oStatic;
        }

        public static string UploadFull(string JSON, string DeviceID, string blockType = "")
        {
            if (ReadOnly)
                return "";

            if (string.IsNullOrEmpty(blockType))
                blockType = BlockType;

            try
            {
                JObject oObj = JObject.Parse(JSON);

                //Remove NULL values
                foreach (var oTok in oObj.Descendants().Where(t => t.Parent.Type == (JTokenType.Property) && t.Type == JTokenType.Null).ToList())
                {
                    try
                    {
                        oTok.Parent.Remove();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Error UploadFull_1: " + ex.Message.ToString());
                    }
                }

                //Remove empty values
                foreach (var oTok in oObj.Descendants().Where(t => t.Type == (JTokenType.Object) && !t.HasValues).ToList())
                {
                    try
                    {
                        if (oTok.Parent.Type == JTokenType.Property)
                        {
                            oTok.Parent.Remove();
                            continue;
                        }

                        if (oTok.Parent.Type == JTokenType.Array)
                        {
                            if (oTok.Parent.Count == 1) //Parent is array with one empty child
                            {
                                if (oTok.Parent.Parent.Type == JTokenType.Property)
                                    oTok.Parent.Parent.Remove(); //remove parent
                            }
                            else
                                oTok.Remove(); //remove empty array item
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Error UploadFull_1: " + ex.Message.ToString());
                    }
                }


                JSort(oObj, true); //Enforce full sort

                //JObject oStatic = oObj.ToObject<JObject>();
                JObject jTemp = oObj.ToObject<JObject>();

                //Load BlockChain
                Blockchain oChain = GetChain(DeviceID);

                JSort(oObj);
                //JSort(oStatic);

                var jObj = oObj;

                //if (!UseCosmosDB)
                //{
                //    //Loop through all ChildObjects
                //    foreach (var oChild in jObj.Descendants().Where(t => t.Type == JTokenType.Object).Reverse())
                //    {
                //        try
                //        {
                //            JToken tRef = oObj.SelectToken(oChild.Path, false);

                //            //check if tRfe is valid..
                //            if (tRef == null)
                //                continue;


                //            string sName = "misc";
                //            if (oChild.Parent.Type == JTokenType.Property)
                //                sName = ((Newtonsoft.Json.Linq.JProperty)oChild.Parent).Name;
                //            else
                //                sName = ((Newtonsoft.Json.Linq.JProperty)oChild.Parent.Parent).Name; //it's an array

                //            if (sName.StartsWith('@'))
                //                continue;

                //            foreach (JProperty jProp in oStatic.SelectToken(oChild.Path).Children().Where(t => t.Type == JTokenType.Property).ToList())
                //            {
                //                try
                //                {
                //                    if (!jProp.Name.StartsWith('#'))
                //                    {
                //                        if (jProp.Descendants().Where(t => t.Type == JTokenType.Property && ((JProperty)t).Name.StartsWith("#")).Count() == 0)
                //                        {
                //                            jProp.Remove();
                //                        }
                //                    }
                //                }
                //                catch (Exception ex)
                //                {
                //                    Debug.WriteLine("Error UploadFull_2: " + ex.Message.ToString());
                //                }
                //            }


                //            //remove all # and @ attributes
                //            foreach (var oKey in tRef.Parent.Descendants().Where(t => t.Type == JTokenType.Property && (((JProperty)t).Name.StartsWith("#") || ((JProperty)t).Name.StartsWith("@"))).ToList())
                //            {
                //                try
                //                {
                //                    oKey.Remove();
                //                }
                //                catch (Exception ex)
                //                {
                //                    Debug.WriteLine("Error UploadFull_3: " + ex.Message.ToString());
                //                }
                //            }

                //            WriteHash(ref tRef, ref oStatic, sName);
                //            oObj.SelectToken(oChild.Path).Replace(tRef);

                //        }
                //        catch (Exception ex)
                //        {
                //            Debug.WriteLine("Error UploadFull_4: " + ex.Message.ToString());
                //        }
                //    }

                //    //remove all # and @ objects
                //    foreach (var oKey in oStatic.Descendants().Where(t => t.Type == JTokenType.Property && ((JProperty)t).Name.StartsWith("@")).ToList())
                //    {
                //        try
                //        {
                //            oKey.Remove();
                //        }
                //        catch (Exception ex)
                //        {
                //            Debug.WriteLine("Error UploadFull_5: " + ex.Message.ToString());
                //        }
                //    }
                //}

                JObject oStatic = Deduplicate(oObj);
                JSort(oStatic);
                //JSort(oStatic, true);

                string sResult = CalculateHash(oStatic.ToString(Newtonsoft.Json.Formatting.None));

                var oBlock = oChain.GetLastBlock();

                if (oBlock.data != sResult)
                {
                    var oNew = oChain.MineNewBlock(oBlock, BlockType);
                    oChain.UseBlock(sResult, oNew);

                    if (oChain.ValidateChain())
                    {
                        //Console.WriteLine(JsonConvert.SerializeObject(tChain));
                        if (oNew.index == 1)
                        {
                            Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " - new " + DeviceID);
                        }
                        else
                        {
                            Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " - update " + DeviceID);
                        }

                        if (!UseCosmosDB)
                            WriteHash(DeviceID, JsonConvert.SerializeObject(oChain), "_chain");
                        else
                            WriteHash(DeviceID, JsonConvert.SerializeObject(oChain), "chain");


                        //Add missing attributes
                        if (oStatic["_date"] == null)
                            oStatic.AddFirst(new JProperty("_date", new DateTime(oNew.timestamp).ToUniversalTime()));
                        if (oStatic["_index"] == null)
                            oStatic.AddFirst(new JProperty("_index", oNew.index));
                        if (oStatic["_type"] == null)
                            oStatic.AddFirst(new JProperty("_type", blockType));
                        if (oStatic["#id"] == null)
                            oStatic.AddFirst(new JProperty("#id", DeviceID));

                        if (jTemp["_index"] == null)
                            jTemp.AddFirst(new JProperty("_index", oNew.index));
                        if (jTemp["_hash"] == null)
                            jTemp.AddFirst(new JProperty("_hash", oNew.data));
                        if (jTemp["_date"] == null)
                            jTemp.AddFirst(new JProperty("_date", new DateTime(oNew.timestamp).ToUniversalTime()));
                        if (jTemp["_type"] == null)
                            jTemp.AddFirst(new JProperty("_type", blockType));
                        if (jTemp["#id"] == null)
                            jTemp.AddFirst(new JProperty("#id", DeviceID));

                        //JSort(jTemp);
                        if (!UseCosmosDB)
                        {
                            if (blockType == BlockType)
                                WriteHash(DeviceID, jTemp.ToString(Formatting.None), "_full");
                            else
                                WriteHash(DeviceID + "_" + blockType, jTemp.ToString(Formatting.None), "_full");
                        }
                        else
                        {
                            //No need to save cached document when using CosmosDB
                            //WriteHashAsync(DeviceID, jTemp.ToString(Formatting.None), "_Full").Wait();
                        }
                    }
                    else
                    {
                        Console.WriteLine("Blockchain is NOT valid... " + DeviceID);
                    }
                }
                else
                {
                    //Do not touch Blockchain, but store the Full JSON for reporting
                    if (jTemp["_index"] == null)
                        jTemp.AddFirst(new JProperty("_index", oBlock.index));
                    if (jTemp["_hash"] == null)
                        jTemp.AddFirst(new JProperty("_hash", oBlock.data));

                    if (!UseCosmosDB) //do update _date on CosmosDB
                    {
                        if (jTemp["_date"] == null)
                            jTemp.AddFirst(new JProperty("_date", DateTime.Now.ToUniversalTime()));
                    }

                    if (jTemp["_type"] == null)
                        jTemp.AddFirst(new JProperty("_type", blockType));
                    if (jTemp["#id"] == null)
                        jTemp.AddFirst(new JProperty("#id", DeviceID));

                    //JSort(jTemp);

                    if (!UseCosmosDB)
                    {
                        //Only store Full data for default BlockType
                        if (blockType == BlockType)
                            WriteHashAsync(DeviceID, jTemp.ToString(Formatting.None), "_full").ConfigureAwait(false);
                    }
                }

                //JSort(oStatic);
                if (!UseCosmosDB)
                {
                    if (blockType == BlockType)
                        WriteHash(sResult, oStatic.ToString(Newtonsoft.Json.Formatting.None), "_assets");
                    else
                        WriteHash(sResult + "_" + blockType, oStatic.ToString(Newtonsoft.Json.Formatting.None), "_assets");
                }
                else
                {
                    //On CosmosDB, store full document as Asset
                    if (jTemp["_objectid"] == null)
                        jTemp.AddFirst(new JProperty("_objectid", DeviceID));
                    WriteHash(sResult, jTemp.ToString(Formatting.None), "assets");
                }


                return sResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error UploadFull_6: " + ex.Message.ToString());
            }

            return "";
        }

        public static JObject GetFull(string DeviceID, int Index = -1, string blockType = "")
        {
            try
            {
                JObject oInv = new JObject();

                if (string.IsNullOrEmpty(blockType))
                    blockType = BlockType;

                if (Index == -1 && !UseCosmosDB)
                {
                    string sFull = "";
                    if (blockType == BlockType)
                        sFull = ReadHash(DeviceID, "_full");
                    else
                        sFull = ReadHash(DeviceID + "_" + blockType, "_full");

                    if (!string.IsNullOrEmpty(sFull))
                    {
                        return JObject.Parse(sFull);
                    }
                }

                JObject oRaw = GetRawId(DeviceID, Index, blockType);

                string sData = "";
                if (blockType == BlockType)
                    sData = ReadHash(oRaw["_hash"].ToString(), "_assets");
                else
                    sData = ReadHash(oRaw["_hash"].ToString() + "_" + blockType, "_assets");

                if (!UseCosmosDB)
                {
                    if (!string.IsNullOrEmpty(sData))
                    {
                        oInv = JObject.Parse(sData);
                        try
                        {
                            if (oInv["_index"] == null)
                                oInv.Add(new JProperty("_index", oRaw["_index"]));
                            if (oInv["_date"] == null)
                                oInv.Add(new JProperty("_date", oRaw["_date"]));
                            if (oInv["_hash"] == null)
                                oInv.Add(new JProperty("_hash", oRaw["_hash"]));

                            //Set index and date from blockchain as the index and hash can be from a previous block
                            oInv["_index"] = oRaw["_index"];
                            oInv["_date"] = oRaw["_inventoryDate"];
                        }
                        catch { }

                        List<string> lHashes = new List<string>();

                        //Load hashed values
                        foreach (JProperty oTok in oInv.Descendants().Where(t => t.Type == JTokenType.Property && ((JProperty)t).Name.StartsWith("##hash")).ToList())
                        {
                            lHashes.Add(oTok.Path);
                        }

                        //Remove merge ##hash with hasehd value
                        foreach (string sHash in lHashes)
                        {
                            try
                            {
                                JProperty oTok = oInv.SelectToken(sHash).Parent as JProperty;
                                string sH = oTok.Value.ToString();

                                List<string> aPathItems = oTok.Path.Split('.').ToList();
                                aPathItems.Reverse();
                                string sRoot = "";
                                if (aPathItems.Count > 1)
                                    sRoot = aPathItems[1].Split('[')[0];

                                string sObj = ReadHash(sH, sRoot);
                                if (!string.IsNullOrEmpty(sObj))
                                {
                                    var jStatic = JObject.Parse(sObj);
                                    oTok.Parent.Merge(jStatic);
                                    bool bLoop = true;
                                    int i = 0;
                                    //Remove NULL values as a result from merge
                                    while (bLoop)
                                    {
                                        bLoop = false;
                                        foreach (var jObj in (oTok.Parent.Descendants().Where(t => (t.Type == (JTokenType.Object) || t.Type == (JTokenType.Array)) && t.HasValues == false).Reverse().ToList()))
                                        {
                                            try
                                            {
                                                if ((jObj.Type == JTokenType.Object || jObj.Type == JTokenType.Array) && jObj.Parent.Type == JTokenType.Property)
                                                {
                                                    jObj.Parent.Remove();
                                                    bLoop = true;
                                                    continue;
                                                }

                                                jObj.Remove();
                                                bLoop = true;
                                            }
                                            catch (Exception ex)
                                            {
                                                Debug.WriteLine("Error GetFull_1: " + ex.Message.ToString());
                                                if (i <= 100)
                                                    bLoop = true;
                                                i++;
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    sObj.ToString();
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("Error GetFull_2: " + ex.Message.ToString());
                            }
                        }

                        //Remove ##hash
                        foreach (var oTok in oInv.Descendants().Where(t => t.Type == JTokenType.Property && ((JProperty)t).Name.StartsWith("##hash")).ToList())
                        {
                            try
                            {
                                if (oInv.SelectToken(oTok.Path).Parent.Parent.Children().Count() == 1)
                                    oInv.SelectToken(oTok.Path).Parent.Parent.Remove();
                                else
                                    oInv.SelectToken(oTok.Path).Parent.Remove();
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("Error GetFull_3: " + ex.Message.ToString());
                            }
                        }

                        try
                        {
                            //Remove NULL values
                            foreach (var oTok in (oInv.Descendants().Where(t => t.Type == (JTokenType.Object) && t.HasValues == false).Reverse().ToList()))
                            {
                                try
                                {
                                    if (oTok.Parent.Count == 1)
                                    {
                                        oTok.Parent.Remove();
                                    }

                                    if (oTok.HasValues)
                                        oTok.Remove();
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine("Error GetFull_4: " + ex.Message.ToString());
                                }
                            }
                        }
                        catch { }
                        JSort(oInv, true);

                        if (Index == -1)
                        {
                            //Cache Full
                            WriteHash(DeviceID, oInv.ToString(), "_full");
                        }
                        return oInv;
                    }
                }
                else
                {
                    return JObject.Parse(sData);
                }

            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error GetFull_5: " + ex.Message.ToString());
            }

            return new JObject();
        }

        public static JObject GetRawId(string DeviceID, int Index = -1, string blockType = "")
        {
            JObject jResult = new JObject();

            if (string.IsNullOrEmpty(blockType))
                blockType = BlockType;

            try
            {
                Blockchain oChain;

                block lBlock = null;

                if (Index == -1)
                {
                    oChain = GetChain(DeviceID);
                    lBlock = oChain.GetLastBlock(blockType);

                }
                else
                {
                    oChain = GetChain(DeviceID);
                    lBlock = oChain.GetBlock(Index, blockType);
                }


                int index = lBlock.index;
                DateTime dInvDate = new DateTime(lBlock.timestamp).ToUniversalTime();
                string sRawId = lBlock.data;

                jResult.Add(new JProperty("_index", index));
                jResult.Add(new JProperty("_inventoryDate", dInvDate));
                jResult.Add(new JProperty("_hash", sRawId));
            }
            catch { }

            return jResult;
        }

        public static JObject GetHistory(string DeviceID, string blockType = "")
        {
            JObject jResult = new JObject();

            if (string.IsNullOrEmpty(blockType))
                blockType = BlockType;

            try
            {

                string sChain = ReadHash(DeviceID, "_chain");
                var oChain = JsonConvert.DeserializeObject<Blockchain>(sChain);
                foreach (block oBlock in oChain.Chain.Where(t => t.blocktype == blockType))
                {
                    try
                    {
                        JArray jArr = new JArray();
                        JObject jTemp = new JObject();
                        jTemp.Add("index", oBlock.index);
                        jTemp.Add("timestamp", new DateTime(oBlock.timestamp).ToUniversalTime());
                        jTemp.Add("data", oBlock.data);
                        jArr.Add(jTemp);
                        jResult.Add(oBlock.index.ToString(), jArr);
                    }
                    catch { }
                }

            }
            catch { }

            JSort(jResult);
            return jResult;
        }

        public static JArray GetJHistory(string DeviceID, string blockType = "")
        {
            JArray jResult = new JArray();

            if (string.IsNullOrEmpty(blockType))
                blockType = BlockType;

            try
            {
                string sChain = ReadHash(DeviceID, "_chain");
                var oChain = JsonConvert.DeserializeObject<Blockchain>(sChain);
                foreach (block oBlock in oChain.Chain.Where(t => t.blocktype == blockType))
                {
                    try
                    {
                        JObject jTemp = new JObject();
                        jTemp.Add("index", oBlock.index);
                        jTemp.Add("timestamp", new DateTime(oBlock.timestamp).ToUniversalTime());
                        jTemp.Add("type", "");
                        jResult.Add(jTemp);
                    }
                    catch { }
                }
            }
            catch { }

            return jResult;
        }

        public static JObject GetRaw(string RawID, string path = "")
        {
            JObject jResult = new JObject();
            try
            {
                JObject oInv = JObject.Parse(RawID);

                if (!path.Contains("*") && !path.Contains("..")) //Skip if using wildcards; we have to get the full content to filter
                {
                    if (!string.IsNullOrEmpty(path))
                    {
                        foreach (string spath in path.Split(';'))
                        {
                            string sLookupPath = spath.Replace(".##hash", "");
                            var aPath = spath.Split('.');
                            if (aPath.Length > 1) //at least 2 segments...
                            {
                                sLookupPath = sLookupPath.Substring(0, sLookupPath.LastIndexOf('.'));
                            }
                            //foreach (JProperty oTok in oInv.Descendants().Where(t => t.Path.StartsWith(spath.Split('.')[0]) && t.Type == JTokenType.Property && ((JProperty)t).Name.StartsWith("##hash")).ToList())
                            foreach (JProperty oTok in oInv.Descendants().Where(t => t.Path.StartsWith(sLookupPath) && t.Type == JTokenType.Property && ((JProperty)t).Name.StartsWith("##hash")).ToList())
                            {

                                string sH = oTok.Value.ToString();
                                string sRoot = oTok.Path.Split('.').Reverse().ToList()[1]; //second last as last is ##hash
                                                                                           //string sRoot = oTok.Path.Split('.')[0].Split('[')[0]; //AppMgmtDigest.Application.DisplayInfo.Info.##hash
                                string sObj = ReadHash(sH, sRoot);
                                if (!string.IsNullOrEmpty(sObj))
                                {
                                    var jStatic = JObject.Parse(sObj);
                                    oTok.Parent.Merge(jStatic);
                                    oTok.Remove();
                                }
                            }
                        }
                    }
                    else
                    {
                        foreach (JProperty oTok in oInv.Descendants().Where(t => t.Type == JTokenType.Property && ((JProperty)t).Name.StartsWith("##hash")).ToList())
                        {
                            string sH = oTok.Value.ToString();
                            string sRoot = oTok.Path.Split('.')[0].Split('[')[0];
                            string sObj = ReadHash(sH, sRoot);
                            if (!string.IsNullOrEmpty(sObj))
                            {
                                var jStatic = JObject.Parse(sObj);
                                oTok.Parent.Merge(jStatic);
                                oTok.Remove();
                            }
                        }
                    }

                    JSort(oInv);
                }

                return oInv;
            }
            catch (Exception ex)
            {
                ex.Message.ToString();
            }

            return jResult;
        }

        public static JObject GetDiff(string DeviceId, int IndexLeft, int mode = -1, int IndexRight = -1, string blockType = "")
        {
            if (string.IsNullOrEmpty(blockType))
                blockType = BlockType;

            try
            {
                var right = GetFull(DeviceId, IndexRight);

                if (IndexLeft == 0)
                {
                    IndexLeft = ((int)right["_index"]) - 1;
                }
                var left = GetFull(DeviceId, IndexLeft);

                foreach (var oTok in right.Descendants().Where(t => t.Type == JTokenType.Property && ((JProperty)t).Name.StartsWith("@")).ToList())
                {
                    oTok.Remove();
                }
                foreach (var oTok in left.Descendants().Where(t => t.Type == JTokenType.Property && ((JProperty)t).Name.StartsWith("@")).ToList())
                {
                    oTok.Remove();
                }

                //Remove NULL values
                foreach (var oTok in left.Descendants().Where(t => t.Parent.Type == (JTokenType.Property) && t.Type == JTokenType.Null).ToList())
                {
                    oTok.Parent.Remove();
                }
                foreach (var oTok in right.Descendants().Where(t => t.Parent.Type == (JTokenType.Property) && t.Type == JTokenType.Null).ToList())
                {
                    oTok.Parent.Remove();
                }

                JSort(right);
                JSort(left);

                if (mode <= 1)
                {
                    var optipons = new JsonDiffPatchDotNet.Options();
                    if (mode == 0)
                    {
                        optipons.ArrayDiff = JsonDiffPatchDotNet.ArrayDiffMode.Simple;
                        optipons.TextDiff = JsonDiffPatchDotNet.TextDiffMode.Simple;
                    }
                    if (mode == 1)
                    {
                        optipons.ArrayDiff = JsonDiffPatchDotNet.ArrayDiffMode.Efficient;
                        optipons.TextDiff = JsonDiffPatchDotNet.TextDiffMode.Efficient;
                    }

                    var jpf = new JsonDiffPatchDotNet.JsonDiffPatch(optipons);

                    var oDiff = jpf.Diff(left, right);

                    if (oDiff == null)
                        return new JObject();

                    GC.Collect();
                    return JObject.Parse(oDiff.ToString());
                }
            }
            catch { }

            return new JObject();
        }

        /*      /// <summary>
                /// Convert Topic /Key/Key/[0]/val to JSON Path format Key.Key[0].val
                /// </summary>
                /// <param name="Topic"></param>
                /// <returns></returns>
                public static string Topic2JPath(string Topic)
                {
                    try
                    {
                        string sPath = "";
                        List<string> lItems = Topic.Split('/').ToList();
                        for (int i = 0; i < lItems.Count(); i++)
                        {
                            bool bArray = false;
                            int iVal = -1;
                            if (i + 1 < lItems.Count())
                            {
                                if (lItems[i + 1].Contains("[") && int.TryParse(lItems[i + 1].TrimStart('[').TrimEnd(']'), out iVal))
                                    bArray = true;
                                else
                                    bArray = false;
                            }
                            if (!bArray)
                                sPath += lItems[i] + ".";
                            else
                            {
                                sPath += lItems[i] + "[" + iVal.ToString() + "].";
                                i++;
                            }
                        }

                        return sPath.TrimEnd('.');
                    }
                    catch { }

                    return "";
                }
                */

        class TopicComparer : IComparer<string>
        {
            public int Compare(string x, string y)
            {
                if (x == y)
                    return 0;

                List<string> lx = x.Split('/').ToList();
                List<string> ly = y.Split('/').ToList();

                if (lx.Count > ly.Count)
                    return 1; //y is smaller
                if (lx.Count < ly.Count)
                    return -1; //x is smaller

                int i = 0;
                foreach (string s in lx)
                {
                    if (s == ly[i])
                    {
                        i++;
                        continue;
                    }

                    if (s.StartsWith("[") && s.EndsWith("]") && ly[i].StartsWith("[") && ly[i].EndsWith("]"))
                    {
                        try
                        {
                            int ix = int.Parse(s.TrimStart('[').TrimEnd(']'));
                            int iy = int.Parse(ly[i].TrimStart('[').TrimEnd(']'));
                            if (ix == iy)
                                return 0;
                            if (ix < iy)
                                return -1;
                            else
                                return 1;
                        }
                        catch { }
                    }

                    int iRes = string.Compare(s, ly[i]);

                    return iRes;
                }

                return string.Compare(x, y);
            }
        }

        /// <summary>
        /// Get a list of KeyID that contains a searchkey (freeText)
        /// </summary>
        /// <param name="searchkey"></param>
        /// <param name="KeyID"></param>
        /// <returns></returns>
        public static List<string> Search(string searchkey, string KeyID = "#id")
        {
            //if (string.IsNullOrEmpty(searchkey))
            //    return new List<string>();

            //if (string.IsNullOrEmpty(KeyID))
            //    KeyID = "#id";

            //if (KeyID.Contains(','))
            //    KeyID = KeyID.Split(',')[0];

            List<string> lNames = new List<string>();

            //lNames = FindLatestAsync(System.Net.WebUtility.UrlDecode(searchkey), "#" + KeyID.TrimStart('?').TrimStart('#')).Result;
            //lNames.Sort();
            return lNames.Union(lNames).ToList();
        }

        public static async Task<JArray> QueryAsync(string paths, string select, string exclude, string where)
        {
            paths = System.Net.WebUtility.UrlDecode(paths);
            select = System.Net.WebUtility.UrlDecode(select);
            exclude = System.Net.WebUtility.UrlDecode(exclude);
            where = System.Net.WebUtility.UrlDecode(where);

            List<string> lExclude = new List<string>();
            List<string> lWhere = new List<string>();

            if (!string.IsNullOrEmpty(exclude))
            {
                lExclude = exclude.Split(";").ToList();
            }

            if (!string.IsNullOrEmpty(where))
            {
                lWhere = where.Split(";").ToList();
            }

            if (string.IsNullOrEmpty(select))
            {
                if (!UseCosmosDB)
                    select = "#id"; //,#Name,_inventoryDate
                else
                    select = "id";
            }

            JArray aRes = new JArray();
            List<string> lLatestHash = await GetAllChainsAsync();
            foreach (string sHash in lLatestHash)
            {
                bool foundData = false;
                try
                {
                    var jObj = GetFull(sHash);

                    //Where filter..
                    if (lWhere.Count > 0)
                    {
                        bool bWhere = false;
                        foreach (string sWhere in lWhere)
                        {
                            try
                            {
                                string sPath = sWhere;
                                string sVal = "";
                                string sOp = "";
                                if (sWhere.Contains("=="))
                                {
                                    sVal = sWhere.Split("==")[1];
                                    sOp = "eq";
                                    sPath = sWhere.Split("==")[0];
                                }
                                if (sWhere.Contains("!="))
                                {
                                    sVal = sWhere.Split("!=")[1];
                                    sOp = "ne";
                                    sPath = sWhere.Split("!=")[0];
                                }

                                var jRes = jObj.SelectToken(sPath);
                                if (jRes == null)
                                {
                                    bWhere = true;
                                    continue;
                                }
                                else
                                {
                                    switch (sOp)
                                    {
                                        case "eq":
                                            if (sVal != jRes.ToString())
                                            {
                                                bWhere = true;
                                                continue;
                                            }
                                            break;
                                        case "ne":
                                            if (sVal == jRes.ToString())
                                            {
                                                bWhere = true;
                                                continue;
                                            }
                                            break;
                                        default:
                                            bWhere = true;
                                            continue;
                                    }
                                }
                            }
                            catch { }
                        }

                        if (bWhere)
                        {
                            continue;
                        }
                    }

                    JObject oRes = new JObject();
                    //if(UseCosmosDB)
                    //{
                    //    string sID = jObj["#id"].Value<string>();
                    //    jObj.Add("id", sID);
                    //}
                    foreach (string sAttrib in select.Split(';'))
                    {
                        try
                        {
                            //var jVal = jObj[sAttrib];
                            var jVal = jObj.SelectToken(sAttrib);

                            if (jVal != null)
                            {
                                oRes.Add(sAttrib.Trim(), jVal);
                            }
                        }
                        catch { }
                    }
                    if (!string.IsNullOrEmpty(paths)) //only return defined objects, if empty all object will return
                    {
                        //Generate list of excluded paths
                        List<string> sExclPath = new List<string>();
                        foreach (string sExclude in lExclude)
                        {
                            foreach (var oRem in jObj.SelectTokens(sExclude, false).ToList())
                            {
                                sExclPath.Add(oRem.Path);
                            }
                        }

                        foreach (string path in paths.Split(';'))
                        {
                            try
                            {
                                var oToks = jObj.SelectTokens(path.Trim(), false);

                                if (oToks.Count() == 0)
                                {
                                    if (!foundData)
                                    {
                                        oRes = new JObject(); //remove selected attributes as we do not have any vresults from jsonpath
                                        continue;
                                    }
                                }

                                foreach (JToken oTok in oToks)
                                {
                                    try
                                    {
                                        if (oTok.Type == JTokenType.Object)
                                        {
                                            oRes.Merge(oTok);
                                            //oRes.Add(jObj[select.Split(',')[0]].ToString(), oTok);
                                            continue;
                                        }
                                        if (oTok.Type == JTokenType.Array)
                                        {
                                            oRes.Add(new JProperty(path, oTok));
                                        }
                                        if (oTok.Type == JTokenType.Property)
                                            oRes.Add(oTok.Parent);

                                        if (oTok.Type == JTokenType.String ||
                                            oTok.Type == JTokenType.Integer ||
                                            oTok.Type == JTokenType.Date ||
                                            oTok.Type == JTokenType.Boolean ||
                                            oTok.Type == JTokenType.Float ||
                                            oTok.Type == JTokenType.Guid ||
                                            oTok.Type == JTokenType.TimeSpan)
                                        {
                                            //check if path is excluded
                                            if (!sExclPath.Contains(oTok.Path))
                                                oRes.Add(oTok.Path, oTok);
                                        }

                                        if (oTok.Type == JTokenType.Date)
                                            oRes.Add(oTok.Parent);

                                        foundData = true;
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine("Error Query_5: " + ex.Message.ToString());
                                    }

                                }

                                /*if (oToks.Count() == 0)
                                    oRes = new JObject(); */
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("Error Query_5: " + ex.Message.ToString());
                            }
                        }
                    }

                    if (oRes.HasValues)
                    {
                        //Remove excluded Properties
                        foreach (string sExclude in lExclude)
                        {
                            foreach (var oRem in oRes.SelectTokens(sExclude, false).ToList())
                            {
                                oRem.Parent.Remove();
                            }
                        }

                        aRes.Add(oRes);
                        //lRes.Add(i.ToString(), oRes);
                        //i++;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error Query_5: " + ex.Message.ToString());
                }
            }

            return aRes;

        }

        public static JArray QueryAll(string paths, string select, string exclude, string where)
        {
            paths = System.Net.WebUtility.UrlDecode(paths);
            select = System.Net.WebUtility.UrlDecode(select);
            exclude = System.Net.WebUtility.UrlDecode(exclude);
            where = System.Net.WebUtility.UrlDecode(where);

            List<string> lExclude = new List<string>();
            List<string> lWhere = new List<string>();

            if (!string.IsNullOrEmpty(exclude))
            {
                lExclude = exclude.Split(";").ToList();
            }

            if (!string.IsNullOrEmpty(where))
            {
                lWhere = where.Split(";").ToList();
            }

            if (string.IsNullOrEmpty(select))
            {
                if (!UseCosmosDB)
                    select = "#id"; //,#Name,_inventoryDate
                else
                    select = "id";
            }

            //JObject lRes = new JObject();
            JArray aRes = new JArray();
            List<string> lHashes = new List<string>();
            try
            {
                foreach (var item in _Plugins.OrderBy(t => t.Key))
                {
                    try
                    {
                        bool bHasValues = false;
                        foreach (var jObj in item.Value.GetRawAssets(""))
                        {
                            bHasValues = true;
                            bool foundData = false;
                            //Where filter..
                            if (lWhere.Count > 0)
                            {
                                bool bWhere = false;
                                foreach (string sWhere in lWhere)
                                {
                                    try
                                    {
                                        string sPath = sWhere;
                                        string sVal = "";
                                        string sOp = "";
                                        if (sWhere.Contains("=="))
                                        {
                                            sVal = sWhere.Split("==")[1];
                                            sOp = "eq";
                                            sPath = sWhere.Split("==")[0];
                                        }
                                        if (sWhere.Contains("!="))
                                        {
                                            sVal = sWhere.Split("!=")[1];
                                            sOp = "ne";
                                            sPath = sWhere.Split("!=")[0];
                                        }

                                        var jRes = jObj.SelectToken(sPath);
                                        if (jRes == null)
                                        {
                                            bWhere = true;
                                            continue;
                                        }
                                        else
                                        {
                                            switch (sOp)
                                            {
                                                case "eq":
                                                    if (sVal != jRes.ToString())
                                                    {
                                                        bWhere = true;
                                                        continue;
                                                    }
                                                    break;
                                                case "ne":
                                                    if (sVal == jRes.ToString())
                                                    {
                                                        bWhere = true;
                                                        continue;
                                                    }
                                                    break;
                                                default:
                                                    bWhere = true;
                                                    continue;
                                            }
                                        }
                                    }
                                    catch { }
                                }

                                if (bWhere)
                                {
                                    continue;
                                }
                            }

                            JObject oRes = new JObject();

                            foreach (string sAttrib in select.Split(';'))
                            {
                                try
                                {
                                    //var jVal = jObj[sAttrib];
                                    var jVal = jObj.SelectToken(sAttrib);

                                    if (jVal != null)
                                    {
                                        oRes.Add(sAttrib.Trim(), jVal);
                                    }
                                }
                                catch { }
                            }

                            if (!string.IsNullOrEmpty(paths)) //only return defined objects, if empty all object will return
                            {
                                //Generate list of excluded paths
                                List<string> sExclPath = new List<string>();
                                foreach (string sExclude in lExclude)
                                {
                                    foreach (var oRem in jObj.SelectTokens(sExclude, false).ToList())
                                    {
                                        sExclPath.Add(oRem.Path);
                                    }
                                }

                                foreach (string path in paths.Split(';'))
                                {
                                    try
                                    {
                                        var oToks = jObj.SelectTokens(path.Trim(), false);

                                        if (oToks.Count() == 0)
                                        {
                                            if (!foundData)
                                            {
                                                oRes = new JObject(); //remove selected attributes as we do not have any vresults from jsonpath
                                                continue;
                                            }
                                        }

                                        foreach (JToken oTok in oToks)
                                        {
                                            try
                                            {
                                                if (oTok.Type == JTokenType.Object)
                                                {
                                                    oRes.Merge(oTok);
                                                    //oRes.Add(jObj[select.Split(',')[0]].ToString(), oTok);
                                                    continue;
                                                }
                                                if (oTok.Type == JTokenType.Array)
                                                {
                                                    oRes.Add(new JProperty(path, oTok));
                                                }
                                                if (oTok.Type == JTokenType.Property)
                                                    oRes.Add(oTok.Parent);

                                                if (oTok.Type == JTokenType.String)
                                                {
                                                    //check if path is excluded
                                                    if (!sExclPath.Contains(oTok.Path))
                                                        oRes.Add(oTok.Path, oTok.ToString());
                                                }

                                                if (oTok.Type == JTokenType.Date)
                                                    oRes.Add(oTok.Parent);

                                                foundData = true;
                                            }
                                            catch (Exception ex)
                                            {
                                                Debug.WriteLine("Error Query_5: " + ex.Message.ToString());
                                            }

                                        }

                                        /*if (oToks.Count() == 0)
                                            oRes = new JObject(); */
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine("Error Query_5: " + ex.Message.ToString());
                                    }
                                }
                            }

                            if (oRes.HasValues)
                            {
                                string sHa = CalculateHash(oRes.ToString(Formatting.None));
                                if (!lHashes.Contains(sHa))
                                {
                                    aRes.Add(oRes);
                                    lHashes.Add(sHa);
                                }

                                //Remove excluded Properties
                                foreach (string sExclude in lExclude)
                                {
                                    foreach (var oRem in oRes.SelectTokens(sExclude, false).ToList())
                                    {
                                        oRem.Parent.Remove();
                                        //oRes.Remove(oRem.Path);
                                    }
                                }
                            }
                        }

                        if (bHasValues)
                            return aRes;
                    }
                    catch { }
                }

                return aRes;
            }
            catch { }

            return new JArray();
        }

        public enum ChangeType { New, Update };

        public class Change
        {
            public ChangeType changeType;
            public DateTime lastChange;
            public int index;
            public string id;
        }

        public static JArray GetChanges(TimeSpan age, int changeType = -1)
        {
            age.ToString();
            List<Change> lRes = new List<Change>();

            foreach (var sID in GetAllChainsAsync().Result)
            {
                Change oRes = new Change();
                oRes.id = sID;
                var jObj = JObject.Parse(ReadHash(sID, "_chain"));
                oRes.lastChange = new DateTime(jObj["Chain"].Last["timestamp"].Value<long>());
                if (DateTime.Now.ToUniversalTime().Subtract(oRes.lastChange) > age)
                {
                    continue;
                }
                oRes.index = jObj["Chain"].Last["index"].Value<int>();
                if (oRes.index > 1)
                    oRes.changeType = ChangeType.Update;
                else
                    oRes.changeType = ChangeType.New;

                if (changeType >= 0)
                {
                    if (((int)oRes.changeType) != changeType)
                        continue;
                }

                lRes.Add(oRes);
            }
            return JArray.Parse(JsonConvert.SerializeObject(lRes.OrderBy(t => t.id).ToList(), Formatting.None));
        }

        /// <summary>
        /// Get all BlockChains
        /// </summary>
        /// <returns>List of BlockChain (Device) ID's</returns>
        public static async Task<List<string>> GetAllChainsAsync()
        {
            return await Task.Run(() =>
            {
                List<string> lResult = new List<string>();

                foreach (var item in _Plugins.OrderBy(t => t.Key))
                {
                    try
                    {
                        lResult = item.Value.GetAllIDs();

                        if (lResult.Count() > 0)
                        {
                            return lResult;
                        }
                    }
                    catch { }
                }
                return lResult;
            });

        }

        /// <summary>
        /// Get List of latest Blocks of each Chain
        /// </summary>
        /// <returns></returns>
        public static async Task<List<string>> GetLatestBlocksAsync()
        {
            return await Task.Run(() =>
            {
                List<string> lResult = new List<string>();
                //try
                //{
                //    var cache5 = RedisConnectorHelper.Connection.GetDatabase(5);
                //    string sBlocks = cache5.StringGet("latestBlocks");

                //    if (string.IsNullOrEmpty(sBlocks))
                //    {
                //        try
                //        {
                //            if (UseRedis)
                //            {
                //                //var cache3 = RedisConnectorHelper.Connection.GetDatabase(3);

                //                foreach (var sID in GetAllChainsAsync().Result)
                //                {
                //                    try
                //                    {
                //                        var jObj = JObject.Parse(cache3.StringGet(sID));
                //                        lResult.Add(jObj["Chain"].Last["data"].ToString());
                //                    }
                //                    catch { }
                //                }
                //            }

                //            if (UseRethinkDB)
                //            {
                //                foreach (var sID in GetAllChainsAsync().Result)
                //                {
                //                    try
                //                    {
                //                        var oRes = R.Table("_chain").Get(sID).GetField("Chain").Nth(-1).GetField("data").RunCursor<string>(conn).BufferedItems;
                //                        lResult.AddRange(oRes);
                //                    }
                //                    catch { }
                //                }

                //            }
                //        }
                //        catch { }

                //        cache5.StringSet("latestBlocks", String.Join(";", lResult), new TimeSpan(0, 5, 0)); // Store for 5min
                //    }
                //    else
                //        return sBlocks.Split(';').ToList();

                //}
                //catch { }
                return lResult;
            });
        }

        public static async Task<List<string>> FindHashOnContentAsync(string searchstring)
        {
            return await Task.Run(() =>
            {
                List<string> lResult = new List<string>();
                try
                {
                    //if (UseRedis)
                    //{
                    //    foreach (var oObj in srv.Keys(2, "*"))
                    //    {
                    //        string sVal = cache2.StringGet(oObj);
                    //        if (sVal.Contains(searchstring))
                    //        {
                    //            lResult.Add(oObj);
                    //        }
                    //    }
                    //}
                }
                catch { }

                return lResult;
            });
        }

        public static List<string> FindLatestRawWithHash(List<string> HashList, string KeyID = "#id")
        {
            List<string> lResult = new List<string>();
            try
            {
                List<string> latestBlocks = GetLatestBlocksAsync().Result;
                if (UseRedis)
                {
                    //foreach (string sRawID in latestBlocks)
                    //{
                    //    try
                    //    {
                    //        var sRaw = cache4.StringGet(sRawID).ToString();
                    //        foreach (string Hash in HashList)
                    //        {
                    //            if (sRaw.Contains(Hash))
                    //                lResult.Add(JObject.Parse(sRaw)[KeyID].ToString());
                    //        }
                    //    }
                    //    catch { }
                    //}
                }
            }
            catch { }

            return lResult;
        }

        public static async Task<List<string>> FindLatestAsync(string searchstring, string KeyID = "#id")
        {
            List<string> lResult = new List<string>();
            try
            {
                if (UseRedis)
                {
                    var tFind = await FindHashOnContentAsync(searchstring);

                    tFind.AsParallel().ForAll(t =>
                    {
                        foreach (string sName in FindLatestRawWithHash(new List<string>() { t }, KeyID))
                        {
                            lResult.Add(sName);
                        }
                    });
                }

                if (UseRethinkDB)
                {
                    var tFind = await FindHashOnContentAsync(searchstring);

                    tFind.AsParallel().ForAll(t =>
                    {
                        foreach (string sName in FindLatestRawWithHash(new List<string>() { t }, KeyID))
                        {
                            lResult.Add(sName);
                        }
                    });
                }
            }
            catch { }

            return lResult;
        }

        public static bool Export(string URL, string RemoveObjects)
        {
            int iCount = 0;
            bool bResult = true;

            try
            {
                oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                if (UseRedis)
                {
                    foreach (var sID in GetAllChainsAsync().Result) //Keep it single threaded, to prevent Redis Timeouts
                    {
                        try
                        {

                            var jObj = JObject.Parse(ReadHash(sID, "_chain"));

                            foreach (var sBlock in jObj.SelectTokens("Chain[*].data"))
                            {
                                try
                                {
                                    string sBlockID = sBlock.Value<string>();
                                    if (!string.IsNullOrEmpty(sBlockID))
                                    {
                                        var jBlock = GetRaw(ReadHash(sBlockID, "_assets"));
                                        //var jBlock = GetRaw(cache4.StringGet(sBlockID));
                                        jBlock.Remove("#id"); //old Version of jainDB 
                                        //jBlock.Remove("_date");
                                        jBlock.Remove("_index");

                                        //Remove Objects from Chain
                                        foreach (string sRemObj in RemoveObjects.Split(';'))
                                        {
                                            try
                                            {
                                                foreach (var oTok in jBlock.Descendants().Where(t => t.Path == sRemObj).ToList())
                                                {
                                                    oTok.Remove();
                                                    break;
                                                }
                                            }
                                            catch { }
                                        }

                                        //jBlock.Add("#id", sID);

                                        string sResult = UploadToREST(URL + "/upload/" + sID, jBlock.ToString(Formatting.None));
                                        if (!string.IsNullOrEmpty(sResult.Trim('"')))
                                        {
                                            Console.WriteLine("Exported: " + sResult);
                                            iCount++;
                                        }
                                        else
                                        {
                                            jBlock.ToString();
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine("Error: " + ex.Message);
                                    bResult = false;
                                }
                            }
                            //Thread.Sleep(100);

                            Console.WriteLine("Exported Asset: " + sID);
                        }
                        catch { bResult = false; }
                    }
                }

                if (UseRethinkDB)
                {
                    foreach (var sID in GetAllChainsAsync().Result) //Keep it single threaded, to prevent Redis Timeouts
                    {
                        try
                        {

                            var jObj = JObject.Parse(ReadHash(sID, "_chain"));
                            //var jObj = JObject.Parse(cache3.StringGet(sID));

                            foreach (var sBlock in jObj.SelectTokens("Chain[*].data"))
                            {
                                try
                                {
                                    string sBlockID = sBlock.Value<string>();
                                    if (!string.IsNullOrEmpty(sBlockID))
                                    {
                                        var jBlock = GetRaw(ReadHash(sBlockID, "_assets"));
                                        jBlock.Remove("#id"); //old Version of jainDB 
                                        //jBlock.Remove("_date");
                                        jBlock.Remove("_index");

                                        //Remove Objects from Chain
                                        foreach (string sRemObj in RemoveObjects.Split(';'))
                                        {
                                            try
                                            {
                                                foreach (var oTok in jBlock.Descendants().Where(t => t.Path == sRemObj).ToList())
                                                {
                                                    oTok.Remove();
                                                    break;
                                                }
                                            }
                                            catch { }
                                        }

                                        //jBlock.Add("#id", sID);


                                        string sResult = UploadToREST(URL + "/upload/" + sID, jBlock.ToString(Formatting.None));  //UploadToREST(URL + "/upload/" + sID, jBlock.ToString(Formatting.None));
                                                                                                                                  //System.Threading.Thread.Sleep(50);
                                        if (!string.IsNullOrEmpty(sResult.Trim('"')))
                                        {
                                            Console.WriteLine("Exported: " + sResult);
                                            iCount++;
                                        }
                                        else
                                        {
                                            jBlock.ToString();
                                        }

                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine("Error: " + ex.Message);
                                    bResult = false;
                                }
                            }
                            Thread.Sleep(100);
                        }
                        catch { bResult = false; }
                    }
                }

                if (UseFileStore)
                {
                    foreach (var sID in GetAllChainsAsync().Result)
                    {
                        try
                        {
                            var jObj = JObject.Parse(ReadHash(sID, "_chain"));
                            foreach (var sBlock in jObj.SelectTokens("Chain[*].data"))
                            {
                                try
                                {
                                    string sBlockID = sBlock.Value<string>();
                                    if (!string.IsNullOrEmpty(sBlockID))
                                    {
                                        var jBlock = GetRaw(ReadHash(sBlockID, "_assets"));
                                        jBlock.Remove("#id"); //old Version of jainDB 
                                        //jBlock.Remove("_date");
                                        jBlock.Remove("_index");
                                        //jBlock.Add("#id", sID);

                                        //Remove Objects from Chain
                                        foreach (string sRemObj in RemoveObjects.Split(';'))
                                        {
                                            try
                                            {
                                                foreach (var oTok in jBlock.Descendants().Where(t => t.Path == sRemObj).ToList())
                                                {
                                                    oTok.Remove();
                                                    break;
                                                }
                                            }
                                            catch { }
                                        }

                                        string sResult = UploadToREST(URL + "/upload/" + sID, jBlock.ToString(Formatting.None));
                                        //System.Threading.Thread.Sleep(50);
                                        if (!string.IsNullOrEmpty(sResult.Trim('"')))
                                        {
                                            Console.WriteLine("Exported: " + sResult);
                                            iCount++;
                                        }
                                        else
                                        {
                                            jBlock.ToString();
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine("Error: " + ex.Message);
                                    bResult = false;
                                }
                            }
                            System.Threading.Thread.Sleep(100);
                        }
                        catch { bResult = false; }
                    }
                }
            }
            catch { bResult = false; }
            Console.WriteLine("Done... " + iCount.ToString() + " Blocks exported");
            return bResult;
        }

        public static void FullReload(bool dedup = false)
        {
            try
            {
                foreach (var item in _Plugins.OrderBy(t => t.Key))
                {
                    try
                    {
                        foreach (string sID in item.Value.GetAllIDs())
                        {
                            string sJain = item.Value.ReadHash(sID, "_chain");

                            if (!string.IsNullOrEmpty(sJain))
                            {
                                //Write Hash to the first Plugin if the current plugin is not the first one
                                if (item.Key != _Plugins.OrderBy(t => t.Key).FirstOrDefault().Key)
                                {
                                    _Plugins.OrderBy(t => t.Key).FirstOrDefault().Value.WriteHash(sID, sJain, "_chain");
                                }
                            }
                        }

                        foreach (JObject jObj in item.Value.GetRawAssets("*"))
                        {
                            if (jObj.HasValues)
                            {
                                //Write Hash to the first Plugin if the current plugin is not the first one
                                if (item.Key != _Plugins.OrderBy(t => t.Key).FirstOrDefault().Key)
                                {
                                    _Plugins.OrderBy(t => t.Key).FirstOrDefault().Value.WriteHash(jObj["_hash"].Value<string>(), jObj.ToString(), "_full");
                                    if(dedup)
                                    {
                                        var jTest = Deduplicate(jObj);
                                        _Plugins.OrderBy(t => t.Key).FirstOrDefault().Value.WriteHash(jObj["_hash"].Value<string>(), jTest.ToString(Newtonsoft.Json.Formatting.None), "_assets");
                                    }
                                    else
                                    {
                                        _Plugins.OrderBy(t => t.Key).FirstOrDefault().Value.WriteHash(jObj["_hash"].Value<string>(), jObj.ToString(), "_assets");
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }

            Console.WriteLine("Done... All chains and assets re-loaded");
        }

        public static string UploadToREST(string URL, string content)
        {
            try
            {
                //oClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                HttpContent oCont = new StringContent(content);

                var response = oClient.PostAsync(URL, oCont);
                response.Wait(15000);
                if (response.IsCompleted)
                {
                    return response.Result.Content.ReadAsStringAsync().Result.ToString();
                }


            }
            catch (Exception ex)
            {
                ex.Message.ToString();
            }

            return "";

        }

        public static async Task<string> UploadToRESTAsync(string URL, string content)
        {
            return await Task.Run(() =>
            {
                return UploadToREST(URL, content);
            });
        }

        public static void JSort(JObject jObj, bool deep = false)
        {
            var props = jObj.Properties().ToList();
            foreach (var prop in props)
            {
                prop.Remove();
            }

            foreach (var prop in props.OrderBy(p => p.Name))
            {
                jObj.Add(prop);

                if (deep) //Deep Sort
                {
                    var child = prop.Descendants().Where(t => t.Type == (JTokenType.Object)).ToList();

                    foreach (JObject cChild in child)
                    {
                        JSort(cChild, false);
                    }
                }

                if (prop.Value is JObject)
                    JSort((JObject)prop.Value);
            }
        }

        public static int totalDeviceCount(string sPath = "")
        {
            int iCount = 0;

            foreach (var item in _Plugins.OrderBy(t => t.Key))
            {
                try
                {
                    iCount = item.Value.totalDeviceCount();

                    if (iCount > -1)
                    {
                        //Write Hash to the first Plugin if the current plugin is not the first one
                        if (item.Key != _Plugins.OrderBy(t => t.Key).FirstOrDefault().Key)
                        {
                            _Plugins.OrderBy(t => t.Key).FirstOrDefault().Value.WriteHash("", iCount.ToString(), "totaldevicecount");
                        }
                        return iCount;
                    }
                }
                catch { }
            }

            return iCount;
        }

        public static class GenericPluginLoader<T>
        {
            public static string PluginDirectory;

            public static ICollection<T> LoadPlugins(string path)
            {
                string[] dllFileNames = null;
                PluginDirectory = path;

                if (Directory.Exists(path))
                {
                    dllFileNames = Directory.GetFiles(path, "plugin*.dll").OrderBy(t=>t).ToArray();

                    ICollection<Assembly> assemblies = new List<Assembly>(dllFileNames.Length);
                    AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
                    foreach (string dllFile in dllFileNames)
                    {
                        AssemblyName an = AssemblyName.GetAssemblyName(dllFile);
                        //Assembly assembly = Assembly.Load(an);
                        Assembly assembly = Assembly.LoadFile(dllFile);
                        assemblies.Add(assembly);
                    }

                    Type pluginType = typeof(T);
                    ICollection<Type> pluginTypes = new List<Type>();
                    foreach (Assembly assembly in assemblies)
                    {
                        if (assembly != null)
                        {
                            Type[] types = assembly.GetTypes();

                            foreach (Type type in types)
                            {
                                if (type.IsInterface || type.IsAbstract)
                                {
                                    continue;
                                }
                                else
                                {
                                    if (type.GetInterface(pluginType.FullName) != null)
                                    {
                                        pluginTypes.Add(type);
                                    }
                                }
                            }
                        }
                    }

                    ICollection<T> plugins = new List<T>(pluginTypes.Count);
                    foreach (Type type in pluginTypes)
                    {
                        T plugin = (T)Activator.CreateInstance(type);
                        plugins.Add(plugin);
                    }

                    return plugins;
                }

                return null;
            }

            private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
            {
                var allDlls = new DirectoryInfo(PluginDirectory).GetFiles("*.dll");

                var dll = allDlls.FirstOrDefault(fi => fi.Name == args.Name.Split(',')[0] + ".dll");
                if (dll == null)
                {
                    return null;
                }

                return Assembly.LoadFrom(dll.FullName);
            }
        }
    }
}
