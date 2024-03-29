﻿// ************************************************************************************
//          jaindb (c) Copyright 2023 by Roger Zander
// ************************************************************************************

using JainDBProvider;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using static jaindb.BlockChain;

namespace jaindb
{
    public static class jDB
    {
        public static string BlockType = "INV";

        public static string FilePath = "wwwroot";

        public static hashType HashType = hashType.MD5;

        public static int PoWComplexitity = 0;

        //Proof of Work complexity; 0 = no PoW; 8 = 8 trailing bits of the block hash must be '0'
        public static bool ReadOnly = false;

        public static string wwwPath = "wwwroot";

        public static string CosmosPartitionKeyId = Environment.GetEnvironmentVariable("CosmosPartitionKeyId") ?? "ROMAWOCustomerID";

        internal static Dictionary<string, IStore> _Plugins = new Dictionary<string, IStore>();

        public enum ChangeType { New, Update };

        public enum hashType { MD5, SHA2_256 } //Implemented Hash types

        public static async Task<string> CalculateHashAsync(string input, CancellationToken ct = default(CancellationToken))
        {
            switch (HashType)
            {
                case hashType.MD5:
                    return await Hash.CalculateMD5HashStringAsync(input, ct);
                case hashType.SHA2_256:
                    return await Hash.CalculateSHA2_256HashStringAsync(input, ct);
                default:
                    return await Hash.CalculateMD5HashStringAsync(input, ct); ;
            }
        }

        public static async Task<JObject> DeduplicateAsync(JObject FullObject, CancellationToken ct = default(CancellationToken))
        {
            return await Task.Run(() =>
            {
                JObject oObj = FullObject;
                JObject oStatic = oObj.ToObject<JObject>();

                //Dedublicate arrays
                foreach (var oChild in oObj.Descendants().Where(t => t.Type == JTokenType.Array).Reverse())
                {
                    if (ct.IsCancellationRequested)
                        throw new TaskCanceledException();

                    bool bHasObjects = false;

                    if (oChild.ToString(Formatting.None).Length > 96) //only dedup Array items larger than 96 characters
                    {
                        //Loop through all Child Objects
                        foreach (var oChildObj in oChild.Parent.Descendants().Where(t => t.Type == JTokenType.Object).Reverse())
                        {
                            if (ct.IsCancellationRequested)
                                throw new TaskCanceledException();

                            //Array has child Objects -> ignore array as we use the objects
                            bHasObjects = true;
                            break;
                        }
                    }
                    else
                    {
                        continue; //array is too small for dedup
                    }

                    if (bHasObjects)
                        continue;

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

                    WriteHash(ref tRef, ref oStatic, sName);

                    oChild.ToString();
                    oObj.SelectToken(oChild.Path).Replace(tRef);
                }

                //Loop through all ChildObjects
                foreach (var oChild in oObj.Descendants().Where(t => t.Type == JTokenType.Object).Reverse())
                {
                    if (ct.IsCancellationRequested)
                        throw new TaskCanceledException();
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
                            if (ct.IsCancellationRequested)
                                throw new TaskCanceledException();

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
                        foreach (var oKey in tRef.Parent.Descendants().Where(t => t.Type == JTokenType.Property && (((JProperty)t).Name.StartsWith("#") || ((JProperty)t).Name.StartsWith("@")) && !((JProperty)t).Name.StartsWith("##")).ToList())
                        {
                            if (ct.IsCancellationRequested)
                                throw new TaskCanceledException();

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
                    if (ct.IsCancellationRequested)
                        throw new TaskCanceledException();
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
            });


        }

        public static async Task FullReloadAsync(bool dedup = false, CancellationToken ct = default(CancellationToken))
        {
            try
            {
                foreach (var item in _Plugins.OrderBy(t => t.Key))
                {
                    if (ct.IsCancellationRequested)
                        throw new OperationCanceledException();

                    try
                    {
                        foreach (string sID in await item.Value.GetAllIDsAsync(ct))
                        {
                            if (ct.IsCancellationRequested)
                                throw new TaskCanceledException();

                            string sJain = await item.Value.ReadHashAsync(sID, "_chain", ct);

                            if (!string.IsNullOrEmpty(sJain))
                            {
                                //Write Hash to the first Plugin if the current plugin is not the first one
                                if (item.Key != _Plugins.OrderBy(t => t.Key).FirstOrDefault().Key)
                                {
                                    _ = _Plugins.OrderBy(t => t.Key).FirstOrDefault().Value.WriteHashAsync(sID, sJain, "_chain", ct);
                                }
                            }
                        }

                        if (!dedup)
                        {
                            await foreach (JObject jObj in item.Value.GetRawAssetsAsync("", ct))
                            {
                                //if (jObj.HasValues)
                                //{
                                //    //Write Hash to the first Plugin if the current plugin is not the first one
                                //    if (item.Key != _Plugins.OrderBy(t => t.Key).FirstOrDefault().Key)
                                //    {
                                //        //_Plugins.OrderBy(t => t.Key).FirstOrDefault().Value.WriteHash(jObj["_hash"].Value<string>(), jObj.ToString(), "_assets");
                                //    }
                                //}
                            }
                        }
                        else
                        {
                            await foreach (JObject jObj in item.Value.GetRawAssetsAsync("", ct))
                            {
                                if (ct.IsCancellationRequested)
                                    throw new OperationCanceledException();

                                if (jObj.HasValues)
                                {
                                    //Write Hash to the first Plugin if the current plugin is not the first one
                                    if (item.Key != _Plugins.OrderBy(t => t.Key).FirstOrDefault().Key)
                                    {
                                        _ = _Plugins.OrderBy(t => t.Key).FirstOrDefault().Value.WriteHashAsync(jObj["_hash"].Value<string>(), jObj.ToString(), "_full", ct);
                                        if (dedup)
                                        {
                                            var jTest = await DeduplicateAsync(jObj);
                                            _ = _Plugins.OrderBy(t => t.Key).FirstOrDefault().Value.WriteHashAsync(jObj["_hash"].Value<string>(), jTest.ToString(Newtonsoft.Json.Formatting.None), "_assets", ct);
                                        }
                                        else
                                        {
                                            //_Plugins.OrderBy(t => t.Key).FirstOrDefault().Value.WriteHash(jObj["_hash"].Value<string>(), jObj.ToString(), "_assets");
                                        }
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

        /// <summary>
        /// Get all BlockChains
        /// </summary>
        /// <returns>List of BlockChain (Device) ID's</returns>
        public static async Task<List<string>> GetAllChainsAsync(CancellationToken ct = default(CancellationToken))
        {

            List<string> lResult = new List<string>();

            foreach (var item in _Plugins.OrderBy(t => t.Key))
            {
                if (ct.IsCancellationRequested)
                    throw new TaskCanceledException();
                try
                {
                    lResult = await item.Value.GetAllIDsAsync(ct);

                    if (lResult.Count() > 0)
                    {
                        return lResult;
                    }
                }
                catch { }
            }
            return lResult;
        }

        public static async Task<Blockchain> GetChainAsync(string DeviceID, CancellationToken ct = default(CancellationToken), string partitionkey = "")
        {
            Blockchain oChain;
            string sData = "";

            string Collection = "_chain";
            
            if (!string.IsNullOrEmpty(partitionkey))
            {
                DeviceID = DeviceID + ";" + partitionkey;
            }

            sData = await ReadHashAsync(DeviceID, Collection, ct);

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

        public static async Task<JArray> GetChangesAsync(TimeSpan age, int changeType = -1, CancellationToken ct = default(CancellationToken))
        {
            age.ToString();
            List<Change> lRes = new List<Change>();

            foreach (var sID in await GetAllChainsAsync(ct))
            {
                if (ct.IsCancellationRequested)
                    throw new OperationCanceledException();

                Change oRes = new Change();
                oRes.id = sID;
                var jObj = JObject.Parse(await ReadHashAsync(sID, "_chain", ct));
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

        public static async Task<JObject> GetDiffAsync(string DeviceId, int IndexLeft, int mode = -1, int IndexRight = -1, string blockType = "", CancellationToken ct = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(blockType))
                blockType = BlockType;

            try
            {
                var right = await GetFullAsync(DeviceId, IndexRight, blockType, false, ct);

                if (IndexLeft == 0)
                {
                    IndexLeft = ((int)right["_index"]) - 1;
                }
                var left = await GetFullAsync(DeviceId, IndexLeft, blockType, false, ct);

                foreach (var oTok in right.Descendants().Where(t => t.Type == JTokenType.Property && ((JProperty)t).Name.StartsWith("@")).ToList())
                {
                    if (ct.IsCancellationRequested)
                        throw new TaskCanceledException();
                    oTok.Remove();
                }
                foreach (var oTok in left.Descendants().Where(t => t.Type == JTokenType.Property && ((JProperty)t).Name.StartsWith("@")).ToList())
                {
                    if (ct.IsCancellationRequested)
                        throw new TaskCanceledException();
                    oTok.Remove();
                }

                //Remove NULL values
                foreach (var oTok in left.Descendants().Where(t => t.Parent.Type == (JTokenType.Property) && t.Type == JTokenType.Null).ToList())
                {
                    if (ct.IsCancellationRequested)
                        throw new TaskCanceledException();
                    oTok.Parent.Remove();
                }
                foreach (var oTok in right.Descendants().Where(t => t.Parent.Type == (JTokenType.Property) && t.Type == JTokenType.Null).ToList())
                {
                    if (ct.IsCancellationRequested)
                        throw new TaskCanceledException();
                    oTok.Parent.Remove();
                }

                await JSortAsync(right, false, ct);
                await JSortAsync(left, false, ct);

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

                    return JObject.Parse(oDiff.ToString());
                }
            }
            catch { }

            return new JObject();
        }

        public static async Task<JObject> GetFullAsync(string DeviceID, int Index = -1, string blockType = "", bool checkFullIndex = false, CancellationToken ct = default(CancellationToken))
        {
            if (ct.IsCancellationRequested)
                throw new OperationCanceledException();

            try
            {
                JObject oInv = new JObject();

                if (string.IsNullOrEmpty(blockType))
                    blockType = BlockType;

                JObject oRaw = new JObject();
                if (Index == -1)
                {
                    string sFull = "";
                    if (blockType == BlockType)
                        sFull = await ReadHashAsync(DeviceID, "_full", ct);
                    else
                        sFull = await ReadHashAsync(DeviceID + "_" + blockType, "_full", ct);

                    if (!string.IsNullOrEmpty(sFull))
                    {
                        JObject jRes = JObject.Parse(sFull);
                        if (checkFullIndex)
                        {
                            oRaw = await GetRawIdAsync(DeviceID, Index, blockType, ct);
                            if ((oRaw["_index"].Value<string>() == jRes["_index"].Value<string>()) && ((jRes["_type"] ?? "").ToString() == "INV"))
                            {
                                Debug.WriteLine("Full hash is up to date.");
                                return jRes;
                            }
                            else
                            {
                                Debug.WriteLine("Full hash is not up to date, recreate it...");
                            }
                        }
                        else
                        {
                            return jRes;
                        }

                    }
                    else
                    {
                        if (checkFullIndex)
                            oRaw = await GetRawIdAsync(DeviceID, Index, blockType, ct);
                        else 
                            return null;
                    }
                }
                else
                {
                    oRaw = await GetRawIdAsync(DeviceID, Index, blockType, ct);
                }

                string sData = "";

                if (oRaw["_hash"] == null)
                    return new JObject();

                if (blockType == BlockType)
                    sData = await ReadHashAsync(oRaw["_hash"].ToString(), "_assets", ct);
                else
                    sData = await ReadHashAsync(oRaw["_hash"].ToString() + "_" + blockType, "_assets", ct);

                if (!string.IsNullOrEmpty(sData))
                {
                    return await Task.Run(async () =>
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
                            if (oInv["_date"] == null || !oInv["_date"].HasValues)   //why date?
                                oInv["_date"] = oRaw["_inventoryDate"];
                        }
                        catch { }

                        List<string> lHashes = new List<string>();

                        //Load hashed values
                        foreach (JProperty oTok in oInv.Descendants().Where(t => t.Type == JTokenType.Property && ((JProperty)t).Name.StartsWith("##hash")).ToList())
                        {
                            lHashes.Add(oTok.Path);
                        }

                        //Remove merge ##hash with hashed value
                        foreach (string sHash in lHashes.OrderBy(t => t))
                        {
                            if (ct.IsCancellationRequested)
                                throw new OperationCanceledException();

                            try
                            {
                                JProperty oTok = oInv.SelectToken(sHash).Parent as JProperty;
                                string sH = oTok.Value.ToString();

                                List<string> aPathItems = oTok.Path.Split('.').ToList();
                                aPathItems.Reverse();
                                string sRoot = "";
                                if (aPathItems.Count > 1)
                                    sRoot = aPathItems[1].Split('[')[0];

                                string sObj = await ReadHashAsync(sH, sRoot, ct);
                                if (!string.IsNullOrEmpty(sObj))
                                {
                                    if (sObj.StartsWith('[')) //Check if value is an Array
                                    {
                                        var jStatic = JArray.Parse(sObj); //get Array Object

                                        var oPar = oTok.Parent.Parent.Parent;
                                        oTok.Parent.Parent.Remove(); //Remove the Object with the ##hash
                                        oPar.Add(new JProperty(sRoot, jStatic)); //add Array Object

                                    }
                                    else
                                    {
                                        var jStatic = JObject.Parse(sObj);
                                        oTok.Parent.Merge(jStatic);
                                    }
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
                                                    //only remove if original value does not match
                                                    if (((oTok.Parent[((Newtonsoft.Json.Linq.JProperty)jObj.Parent).Name] ?? "").ToString()) != jObj.ToString())
                                                    {
                                                        jObj.Parent.Remove();
                                                        bLoop = true;
                                                    }

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
                            if (ct.IsCancellationRequested)
                                throw new OperationCanceledException();

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

                        await JSortAsync(oInv, true, ct);

                        if (Index == -1)
                        {
                            if (!ct.IsCancellationRequested)
                            {
                                //Cache Full
                                _ = WriteHashAsync(DeviceID, oInv.ToString(), "_full", ct);
                            }
                        }
                        return oInv;
                    }, ct);
                }

            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error GetFull_5: " + ex.Message.ToString());
            }

            return null;
        }

        public static async Task<JObject> GetHistoryAsync(string DeviceID, string blockType = "", CancellationToken ct = default(CancellationToken))
        {
            JObject jResult = new JObject();

            if (string.IsNullOrEmpty(blockType))
                blockType = BlockType;

            try
            {

                string sChain = await ReadHashAsync(DeviceID, "_chain", ct);
                var oChain = JsonConvert.DeserializeObject<Blockchain>(sChain);
                foreach (Block oBlock in oChain.Chain.Where(t => t.blocktype == blockType))
                {
                    if (ct.IsCancellationRequested)
                        throw new TaskCanceledException();
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

            await JSortAsync(jResult, false, ct);
            return jResult;
        }

        public static async Task<JArray> GetJHistoryAsync(string DeviceID, string blockType = "", CancellationToken ct = default(CancellationToken))
        {
            JArray jResult = new JArray();

            if (string.IsNullOrEmpty(blockType))
                blockType = BlockType;

            try
            {
                string sChain = await ReadHashAsync(DeviceID, "_chain", ct);
                var oChain = JsonConvert.DeserializeObject<Blockchain>(sChain);
                foreach (Block oBlock in oChain.Chain.Where(t => t.blocktype == blockType))
                {
                    if (ct.IsCancellationRequested)
                        throw new TaskCanceledException();

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

        public static async Task<JObject> GetRawAsync(string RawID, string path = "", CancellationToken ct = default(CancellationToken))
        {
            if (ct.IsCancellationRequested)
                throw new OperationCanceledException();

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
                            if (ct.IsCancellationRequested)
                                throw new TaskCanceledException();

                            string sLookupPath = spath.Replace(".##hash", "");
                            var aPath = spath.Split('.');
                            if (aPath.Length > 1) //at least 2 segments...
                            {
                                sLookupPath = sLookupPath.Substring(0, sLookupPath.LastIndexOf('.'));
                            }
                            //foreach (JProperty oTok in oInv.Descendants().Where(t => t.Path.StartsWith(spath.Split('.')[0]) && t.Type == JTokenType.Property && ((JProperty)t).Name.StartsWith("##hash")).ToList())
                            foreach (JProperty oTok in oInv.Descendants().Where(t => t.Path.StartsWith(sLookupPath) && t.Type == JTokenType.Property && ((JProperty)t).Name.StartsWith("##hash")).ToList())
                            {
                                if (ct.IsCancellationRequested)
                                    throw new TaskCanceledException();

                                string sH = oTok.Value.ToString();
                                string sRoot = oTok.Path.Split('.').Reverse().ToList()[1]; //second last as last is ##hash
                                                                                           //string sRoot = oTok.Path.Split('.')[0].Split('[')[0]; //AppMgmtDigest.Application.DisplayInfo.Info.##hash
                                string sObj = await ReadHashAsync(sH, sRoot, ct);
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
                            if (ct.IsCancellationRequested)
                                throw new OperationCanceledException();

                            string sH = oTok.Value.ToString();
                            string sRoot = oTok.Path.Split('.')[0].Split('[')[0];
                            string sObj = await ReadHashAsync(sH, sRoot);
                            if (!string.IsNullOrEmpty(sObj))
                            {
                                var jStatic = JObject.Parse(sObj);
                                oTok.Parent.Merge(jStatic);
                                oTok.Remove();
                            }
                        }
                    }

                    await JSortAsync(oInv, false, ct);
                }

                return oInv;
            }
            catch (Exception ex)
            {
                ex.Message.ToString();
            }

            return jResult;
        }

        public static async Task<JObject> GetRawIdAsync(string DeviceID, int Index = -1, string blockType = "", CancellationToken ct = default(CancellationToken))
        {
            if (ct.IsCancellationRequested)
                throw new OperationCanceledException();

            JObject jResult = new JObject();

            if (string.IsNullOrEmpty(blockType))
                blockType = BlockType;

            try
            {
                Blockchain oChain;

                Block lBlock = null;

                if (Index == -1)
                {
                    oChain = await GetChainAsync(DeviceID, ct);
                    lBlock = oChain.GetLastBlock(blockType);
                }
                else
                {
                    oChain = await GetChainAsync(DeviceID, ct);
                    lBlock = oChain.GetBlock(Index, blockType);
                }

                if (lBlock != null)
                {
                    int index = lBlock.index;
                    DateTime dInvDate = new DateTime(lBlock.timestamp).ToUniversalTime();
                    string sRawId = lBlock.data;

                    jResult.Add(new JProperty("_index", index));
                    jResult.Add(new JProperty("_inventoryDate", dInvDate));
                    jResult.Add(new JProperty("_hash", sRawId));
                }
            }
            catch { }

            return jResult;
        }

        public static async Task JSortAsync(JObject jObj, bool deep = false, CancellationToken ct = default(CancellationToken))
        {
            await Task.Run(async () =>
            {
                var props = jObj.Properties().ToList();
                foreach (var prop in props)
                {
                    if (ct.IsCancellationRequested)
                        throw new OperationCanceledException();

                    prop.Remove();
                }

                foreach (var prop in props.OrderBy(p => p.Name))
                {
                    if (ct.IsCancellationRequested)
                        throw new OperationCanceledException();

                    jObj.Add(prop);

                    if (deep) //Deep Sort
                    {
                        var child = prop.Descendants().Where(t => t.Type == (JTokenType.Object)).ToList();

                        foreach (JObject cChild in child)
                        {
                            if (ct.IsCancellationRequested)
                                throw new OperationCanceledException();

                            await JSortAsync(cChild, false, ct);
                        }
                    }

                    if (prop.Value is JObject)
                        await JSortAsync((JObject)prop.Value, false, ct);
                }
            });
        }

        public static void loadPlugins(string PluginPath = "")
        {
            if (string.IsNullOrEmpty(PluginPath))
            {
                PluginPath = AppDomain.CurrentDomain.BaseDirectory;
                wwwPath = PluginPath;
            }

            _Plugins.Clear();

            ICollection<IStore> plugins = GenericPluginLoader<IStore>.LoadPlugins(PluginPath);
            foreach (var item in plugins)
            {
                try
                {
                    _Plugins.Add(item.Name, item);
                    Console.WriteLine(item.Name);
                    item.Settings = new Dictionary<string, string>();
                    item.Settings.Add("FilePath", FilePath);
                    item.Settings.Add("wwwPath", wwwPath);
                    item.Init();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Plugin Error: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Lookup Key ID's to search for Objects based on their Key
        /// </summary>
        /// <param name="name">Key name to search. E.g. "name"</param>
        /// <param name="value">Value to search. E.g. "computer01"</param>
        /// <returns>Hash ID of the Object</returns>
        public static async Task<string> LookupIDAsync(string name, string value, CancellationToken ct = default(CancellationToken))
        {
            string sResult = "";
            try
            {
                foreach (var item in _Plugins.OrderBy(t => t.Key))
                {
                    if (ct.IsCancellationRequested)
                        throw new OperationCanceledException();

                    try
                    {
                        sResult = await item.Value.LookupIDAsync(name, value, ct);

                        if (!string.IsNullOrEmpty(sResult))
                        {
                            //Write Hash to the first Plugin if the current plugin is not the first one
                            if (item.Key != _Plugins.OrderBy(t => t.Key).FirstOrDefault().Key)
                            {
                                _ = _Plugins.OrderBy(t => t.Key).FirstOrDefault().Value.WriteLookupIDAsync(name, value, sResult, ct);
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

        public static async Task<JArray> QueryAllAsync(string paths, string select, string exclude, string where, CancellationToken ct = default(CancellationToken))
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
                select = "#id"; //,#Name,_inventoryDate
            }

            //JObject lRes = new JObject();
            JArray aRes = new JArray();
            List<string> lHashes = new List<string>();
            try
            {
                foreach (var item in _Plugins.OrderBy(t => t.Key))
                {
                    if (ct.IsCancellationRequested)
                        throw new OperationCanceledException();

                    try
                    {
                        bool bHasValues = false;

                        await foreach (JObject jObj in item.Value.GetRawAssetsAsync(paths, ct))
                        {
                            if (ct.IsCancellationRequested)
                                throw new OperationCanceledException();

                            bHasValues = true;
                            bool foundData = false;
                            //Where filter..
                            if (lWhere.Count > 0)
                            {
                                bool bWhere = false;
                                foreach (string sWhere in lWhere)
                                {
                                    if (ct.IsCancellationRequested)
                                        throw new OperationCanceledException();

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
                                if (ct.IsCancellationRequested)
                                    throw new OperationCanceledException();

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
                                        if (ct.IsCancellationRequested)
                                            throw new OperationCanceledException();

                                        sExclPath.Add(oRem.Path);
                                    }
                                }

                                foreach (string path in paths.Split(';'))
                                {
                                    if (ct.IsCancellationRequested)
                                        throw new OperationCanceledException();

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
                                string sHa = await CalculateHashAsync(oRes.ToString(Formatting.None), ct);
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
                                        if (ct.IsCancellationRequested)
                                            throw new OperationCanceledException();

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

        public static async Task<JArray> QueryAsync(string paths, string select, string exclude, string where, bool fixFullIndex = false, CancellationToken ct = default(CancellationToken))
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
                select = "#id"; //,#Name,_inventoryDate
            }

            JArray aRes = new JArray();
            List<string> lLatestHash = await GetAllChainsAsync(ct);
            Random rand = new Random();

            foreach (string sHash in lLatestHash.OrderBy(_ => rand.Next()).ToList())
            {
                bool foundData = false;

                if (ct.IsCancellationRequested)
                    throw new OperationCanceledException();

                try
                {
                    var jObj = await GetFullAsync(sHash, -1, "INV", fixFullIndex, ct);

                    if (jObj == null)
                        continue;

                    //Where filter..
                    if (lWhere.Count > 0)
                    {
                        bool bWhere = false;
                        foreach (string sWhere in lWhere)
                        {
                            if (ct.IsCancellationRequested)
                                throw new OperationCanceledException();
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
                            //return;
                            continue;
                        }
                    }

                    JObject oRes = new JObject();

                    foreach (string sAttrib in select.Split(';'))
                    {
                        if (ct.IsCancellationRequested)
                            throw new OperationCanceledException();

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
                            if (ct.IsCancellationRequested)
                                throw new OperationCanceledException();

                            foreach (var oRem in jObj.SelectTokens(sExclude, false).ToList())
                            {
                                if (ct.IsCancellationRequested)
                                    throw new OperationCanceledException();

                                sExclPath.Add(oRem.Path);
                            }
                        }

                        foreach (string path in paths.Split(';'))
                        {
                            if (ct.IsCancellationRequested)
                                throw new OperationCanceledException();

                            try
                            {
                                bool bArray = false;
                                string sPath = path.Trim();
                                if (path.EndsWith("[]"))  //if path ends with [] is a marker to convert the resut into an array
                                {
                                    bArray = true;
                                    sPath = path.TrimEnd(']').TrimEnd('[');
                                }

                                var oToks = jObj.SelectTokens(sPath, false);

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
                                            if (bArray) //Convert result to array
                                            {
                                                JArray jNew = new JArray();
                                                jNew.Add(oTok);
                                                oRes.Add(new JProperty(sPath, jNew));
                                            }
                                            else
                                            {
                                                oRes.Merge(oTok);
                                            }
                                            //oRes.Add(jObj[select.Split(',')[0]].ToString(), oTok);
                                            continue;
                                        }
                                        if (oTok.Type == JTokenType.Array)
                                        {
                                            oRes.Add(new JProperty(sPath, oTok));
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
                                        {
                                            if (!oRes.ContainsKey(oTok.Path))
                                                oRes.Add(oTok.Parent);
                                        }


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
                                if (ct.IsCancellationRequested)
                                    throw new OperationCanceledException();

                                oRem.Parent.Remove();
                            }
                        }

                        aRes.Add(oRes);
                        if ((aRes.Count % 10) == 0)
                        {
                            Console.WriteLine("Full count: " + aRes.Count);
                        }
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

        public static async Task<string> ReadHashAsync(string Hash, string Collection, CancellationToken ct = default(CancellationToken))
        {
            string sResult = "";
            try
            {
                if (ct.IsCancellationRequested)
                    throw new OperationCanceledException();

                Collection = Collection.ToLower();

                foreach (var item in _Plugins.OrderBy(t => t.Key))
                {
                    if (ct.IsCancellationRequested)
                        throw new OperationCanceledException();

                    try
                    {
                        DateTime dStart = DateTime.Now;
                        sResult = await item.Value.ReadHashAsync(Hash, Collection, ct);

                        //Debug.WriteLine("ReadHash-" + item.Key + "-" + Collection + " duration:" + (DateTime.Now - dStart).TotalMilliseconds.ToString() + " ms");

                        if (!string.IsNullOrEmpty(sResult))
                        {
                            //Write Hash to the first Plugin if the current plugin is not the first one
                            if (item.Key != _Plugins.OrderBy(t => t.Key).FirstOrDefault().Key)
                            {
                                _ = _Plugins.OrderBy(t => t.Key).FirstOrDefault().Value.WriteHashAsync(Hash, sResult, Collection, ct);
                            }
                            else
                            {
                                //Debug.WriteLine("Hash was from memory cache...");
                            }
                            return sResult;
                        }
                    }
                    catch (Exception ex)
                    {
                        ex.Message.ToString();
                    }
                }

                return sResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error ReadHash_1: " + ex.Message.ToString());
            }

            return sResult;

        }

        public static async Task<int> totalDeviceCountAsync(string sPath = "", CancellationToken ct = default(CancellationToken))
        {
            int iCount = 0;

            foreach (var item in _Plugins.OrderBy(t => t.Key))
            {
                if (ct.IsCancellationRequested)
                    throw new OperationCanceledException();

                try
                {
                    iCount = await item.Value.totalDeviceCountAsync("", ct);

                    if (iCount > -1)
                    {
                        //Write Hash to the first Plugin if the current plugin is not the first one
                        if (item.Key != _Plugins.OrderBy(t => t.Key).FirstOrDefault().Key)
                        {
                            _ = _Plugins.OrderBy(t => t.Key).FirstOrDefault().Value.WriteHashAsync("", iCount.ToString(), "totaldevicecount", ct);
                        }
                        return iCount;
                    }
                }
                catch { }
            }

            return iCount;
        }

        public static async Task<string> UploadFullAsync(string JSON, string DeviceID, string blockType = "", CancellationToken ct = default(CancellationToken))
        {
            if (ct.IsCancellationRequested)
                throw new OperationCanceledException();

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
                    if (ct.IsCancellationRequested)
                        throw new OperationCanceledException();

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
                    if (ct.IsCancellationRequested)
                        throw new OperationCanceledException();

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


                await JSortAsync(oObj, true, ct); //Enforce full sort

                //JObject oStatic = oObj.ToObject<JObject>();
                JObject jTemp = oObj.ToObject<JObject>();

                //Load BlockChain
                Blockchain oChain = await GetChainAsync(DeviceID, ct, oObj[CosmosPartitionKeyId]?.Value<string>() ?? "");

                await JSortAsync(oObj, false, ct);
                //JSort(oStatic);

                var jObj = oObj;

                JObject oStatic = await DeduplicateAsync(oObj);
                await JSortAsync(oStatic, false, ct);
                //JSort(oStatic, true);

                string sResult = await CalculateHashAsync(oStatic.ToString(Newtonsoft.Json.Formatting.None), ct);

                var oBlock = oChain.GetLastBlock();

                if (oBlock.data != sResult) //only update chain if something has changed...
                {
                    var oNew = await oChain.MineNewBlockAsync(oBlock, BlockType, ct);
                    await oChain.UseBlockAsync(sResult, oNew, ct);

                    if (await oChain.ValidateChainAsync(false, ct))
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

                        JObject jChainheader = new JObject();
                        //AddOn for e.g. CosmosDB
                        try
                        {
                            jChainheader.Add("customerid", oObj[CosmosPartitionKeyId]?.Value<string>() ?? "");
                            jChainheader.Add("modifydate", jTemp["_date"].Value<DateTime>());
                            jChainheader.Add("index", oChain.GetLastBlock().index);
                            jChainheader.Add("assetid", oChain.GetLastBlock().data);
                            JArray jKeys = new JArray();
                            JObject jKey = new JObject();
                            jKey.Add("name", oObj["#Name"]?.Value<string>() ?? "");
                            jKey.Add("serial", oObj["#SerialNumber"]?.Value<string>() ?? "");
                            jKey.Add("uuid", oObj["#UUID"]?.Value<string>() ?? "");
                            jKeys.Add(jKey);
                            jChainheader.Add("keys", jKeys);
                        }
                        catch { }

                        await WriteHashAsync(DeviceID, JsonConvert.SerializeObject(oChain), "_chain", ct, jChainheader);

                        if (blockType == BlockType)
                            await WriteHashAsync(DeviceID, jTemp.ToString(Formatting.None), "_full", ct, PartitionKey: jTemp[CosmosPartitionKeyId]?.Value<string>() ?? "");
                        else
                            await WriteHashAsync(DeviceID + "_" + blockType, jTemp.ToString(Formatting.None), "_full", ct);

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


                    if (jTemp["_date"] == null)
                        jTemp.AddFirst(new JProperty("_date", DateTime.Now.ToUniversalTime()));


                    if (jTemp["_type"] == null)
                        jTemp.AddFirst(new JProperty("_type", blockType));
                    if (jTemp["#id"] == null)
                        jTemp.AddFirst(new JProperty("#id", DeviceID));

                    //Only store Full data for default BlockType
                    if (blockType == BlockType)
                        await WriteHashAsync(DeviceID, jTemp.ToString(Formatting.None), "_full");

                }

                if (blockType == BlockType)
                    await WriteHashAsync(sResult, oStatic.ToString(Newtonsoft.Json.Formatting.None), "_assets");
                else
                    await WriteHashAsync(sResult + "_" + blockType, oStatic.ToString(Newtonsoft.Json.Formatting.None), "_assets");

                return sResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error UploadFull_6: " + ex.Message.ToString());
            }

            return "";
        }

        public static void WriteHash(ref JToken oRoot, ref JObject oStatic, string Collection, CancellationToken ct = default(CancellationToken))
        {
            //Collection = Collection.ToLower(); do NOT change to lowercase to prevent duplicate JSON entries
            if (ct.IsCancellationRequested)
                throw new OperationCanceledException();

            if (ReadOnly)
                return;
            try
            {
                if (!oRoot.HasValues)
                    return;

                //JSort(oStatic);
                string sHash = CalculateHashAsync(oRoot.ToString(Newtonsoft.Json.Formatting.None), ct).Result;

                string sPath = oRoot.Path;

                var oClass = oStatic.SelectToken(sPath);// as JObject;

                if (oClass != null)
                {
                    if (oClass.Type == JTokenType.Object)
                    {
                        if (oClass["##hash"] == null)
                        {
                            ((JObject)oClass).Add("##hash", sHash);
                            _ = WriteHashAsync(sHash, oRoot.ToString(Formatting.None), Collection, ct); //not async, it's faster
                            oRoot = oClass;
                        }
                    }
                    if (oClass.Type == JTokenType.Array)
                    {
                        JObject jNew = new JObject();
                        jNew.Add("##hash", sHash);
                        _ = WriteHashAsync(sHash, oRoot.ToString(Formatting.None), Collection, ct); //not async, it's faster
                        var oPar = oClass.Parent.Parent;
                        oClass.Parent.Remove(); //remove parent Property as we hvae to change thy type from array to object
                        JProperty jProp = new JProperty(Collection, jNew);
                        oPar.Add(jProp);
                        oRoot = jNew;
                    }
                }

            }
            catch (Exception ex)
            {
                ex.Message.ToString();
            }
        }

        public static async Task<bool> WriteHashAsync(string Hash, string Data, string Collection, CancellationToken ct = default(CancellationToken), JObject Extensiondata = null, string PartitionKey = "")
        {
            try
            {
                if (ct.IsCancellationRequested)
                    throw new OperationCanceledException();

                Collection = Collection.ToLower();

                if (ReadOnly)
                    return false;

                foreach (var item in _Plugins.OrderBy(t => t.Key))
                {
                    if (ct.IsCancellationRequested)
                        throw new OperationCanceledException();

                    try
                    {
                        if(Extensiondata != null && Extensiondata.HasValues && item.Key.Contains("CosmosDB", StringComparison.OrdinalIgnoreCase))
                        {
                            JObject jData = JObject.Parse(Data);
                            jData.Merge(Extensiondata);
                            Data = jData.ToString(Formatting.None);
                        }
                        if(!String.IsNullOrEmpty(PartitionKey))
                        {
                            Hash = Hash + ";" + PartitionKey;
                        }
                        DateTime dStart = DateTime.Now;
                        var write = await item.Value.WriteHashAsync(Hash, Data, Collection, ct);
                        Debug.WriteLine("WriteHashAsync-" + item.Key + "-" + Collection + " duration:" + (DateTime.Now - dStart).TotalMilliseconds.ToString() + " ms");
                        if (write)
                            return true; //abort if true
                    }
                    catch { }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static class GenericPluginLoader<T>
        {
            public static string PluginDirectory;

            public static ICollection<T> LoadPlugins(string path)
            {
                List<string> dllFileNames = null;
                PluginDirectory = path;

                if (Directory.Exists(path))
                {
                    //dllFileNames = Directory.GetFiles(path, "plugin*.dll").OrderBy(t=>t).ToArray();
                    dllFileNames = Directory.GetFiles(path, "jaindb.storage.*.dll").OrderBy(t => t).ToList();
                    //dllFileNames.AddRange(Directory.GetFiles(path, "plugin*.dll").OrderBy(t => t).ToArray()); //old naming

                    if (dllFileNames.Count() == 0)
                    {
                        dllFileNames = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "jaindb.storage.*.dll").OrderBy(t => t).ToList();
                    }

                    ICollection<Assembly> assemblies = new List<Assembly>(dllFileNames.Count());
                    AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
                    foreach (string dllFile in dllFileNames)
                    {
                        AssemblyName an = AssemblyName.GetAssemblyName(dllFile);
                        Assembly assembly = Assembly.Load(an); //required for fnJainDB !!!
                        //Assembly assembly = Assembly.LoadFile(dllFile);
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
                try
                {

                    var allDlls = new DirectoryInfo(PluginDirectory).GetFiles("*.dll");

                    var dll = allDlls.FirstOrDefault(fi => fi.Name == args.Name.Split(',')[0] + ".dll");
                    if (dll == null)
                    {
                        return null;
                    }

                    //return Assembly.LoadFrom(dll.FullName);
                    return Assembly.LoadFile(dll.FullName);
                }
                catch (Exception ex)
                {
                    ex.Message.ToString();
                }

                return null;
            }
        }

        //        return string.Compare(x, y);
        //    }
        //}
        public class Change
        {
            public ChangeType changeType;
            public string id;
            public int index;
            public DateTime lastChange;
        }
    }
}
