// ************************************************************************************
//          Blockchain library (c) Copyright 2018 by Roger Zander
// ************************************************************************************

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using System.Collections;

namespace jaindb
{
    public class BlockChain
    {
        public class Blockchain
        {
            private static int _complexity = 0;

            public Blockchain(string Data, string Blocktype = "root", int complexity = 0)
            {
                _complexity = complexity;
                block._complexity = complexity; 

                this.Chain = new List<block>()
            {
                new block
                {
                    index = 0,
                    timestamp = DateTime.Now.Ticks,
                    previous_hash = new byte[0], //block.GetHash("genesis"),
                    data = Data, //!! Data is not stored !!
                    blocktype = Blocktype
                }
            };


                block oGenesis = Chain.First();
                if (string.IsNullOrEmpty(Data))
                    oGenesis.data = "";
                oGenesis.nonce = Mine(0, Blocktype, new byte[0]);
                oGenesis.calc_hash();

                //File.WriteAllText(@"c:\temp\data\" + Base32Encoding.ToString(oGenesis.data) + ".json", Data);
            }

            /// <summary>
            /// Check all hashes from bottom to top
            /// </summary>
            /// <returns>true = all fine; false = something is wrong</returns>
            public bool ValidateChain()
            {
                foreach (block bCheck in Chain.OrderByDescending(t => t.timestamp))
                {
                    long Previous_nonce = 0;

                    var oParent = Chain.FirstOrDefault(t => t.hash == bCheck.previous_hash);
                    if (oParent != null)
                        Previous_nonce = oParent.nonce;

                    if (!bCheck.validate(Previous_nonce))
                        return false;
                }
                return true;
            }

            /// <summary>
            /// Compare another chain with the current and replace the current if necessary
            /// </summary>
            /// <param name="otherChain"></param>
            /// <returns>true = Chain replaced ; false = keep current Chain</returns>
            public bool resolve_conflicts(Blockchain otherChain)
            {
                if (otherChain.ValidateChain()) //Check if Chain is valid
                {
                    if (otherChain.Chain.Count() > this.Chain.Count()) //Check if Chain is longer than the current
                    {
                        this.Chain = otherChain.Chain;
                        return true;
                    }
                }
                return false;
            }

            public List<block> Chain { get; set; }

            /*public block NewBlock(string Data, block ParentBlock, string BlockType = "")
            {
                var oNew = new block()
                {
                    index = ParentBlock.index + 1,
                    timestamp = DateTime.Now.Ticks,
                    previous_hash = ParentBlock.hash,
                    data = block.GetHash(Data),
                    blocktype = BlockType
                };
                //oNew.hash = block.GetHash(oNew.index.ToString() + oNew.timestamp.ToString() + oNew.proof.ToString() + oNew.previous_hash.ToString() + oNew.data.ToString());
                oNew.calc_hash();
                Chain.Add(oNew);

                return oNew;
            }*/

            public block UseBlock(string Data, block FreeBlock)
            {
                /*if(!validateChain()) //Chain not Valid
                    return FreeBlock;*/

                //Check if FreeBlock is valid..
                var oParent = Chain.FirstOrDefault(t => t.hash == FreeBlock.previous_hash);
                if (oParent != null)
                {
                    /*if(!FreeBlock.validate(oParent.nonce))
                    {
                        Console.WriteLine("Invalid Block: \n" + JsonConvert.SerializeObject(FreeBlock));
                        return FreeBlock;
                    }*/
                }
                else
                {
                    Console.WriteLine("Invalid Block: \n" + JsonConvert.SerializeObject(FreeBlock));
                    return FreeBlock;
                }


                if (FreeBlock.data != null | FreeBlock.hash != null | FreeBlock.signature != null) //it's not a free Block
                    return FreeBlock;

                //FreeBlock.index = oParent.index + 1;
                FreeBlock.data = Data;
                FreeBlock.timestamp = DateTime.Now.Ticks;
                FreeBlock.calc_hash();

                return FreeBlock;
            }

            public block MineNewBlock(block ParentBlock, string Blocktype = "")
            {
                if (string.IsNullOrEmpty(Blocktype)) //Use ParentBlock.blocktype if Blocktype is empty
                    Blocktype = ParentBlock.blocktype;

                if (Chain.Count(t => t.previous_hash == ParentBlock.hash & t.blocktype == Blocktype) == 0) //only on Blocktype tree allowed
                {

                    int iIndex = Chain.Max(t => t.index) + 1;

                    var oNew = new block()
                    {
                        index = iIndex,
                        timestamp = DateTime.Now.Ticks,
                        previous_hash = ParentBlock.hash,
                        blocktype = Blocktype,
                        nonce = Mine(ParentBlock.nonce, Blocktype, ParentBlock.hash)
                    };

                    Chain.Add(oNew);
                    
                    return oNew;
                }

                return new block();
            }

            private long Mine(long Previos_noonce, string Blocktype, byte[] Previous_Hash)
            {
                byte[] bHash = new byte[0];

                long nonce = Previos_noonce;

                //current implementation allows to mine new a block without proof of work...
                bool DoWork = false;

                if (_complexity > 0 && nonce != 0 && DoWork) //Skip nonce generation if complexity is 0
                {
                    string sGoal = new string('0', _complexity); //generate a string with leading '0'
                    //string sRes = "";

                    do
                    {
                        if (nonce >= 9223372036854775807) //check overflow
                        {
                            nonce = 0; //reset nonce
                        }

                        nonce++;

                        bHash = block.GetHash((Previos_noonce + nonce).ToString() + Blocktype + Convert.ToBase64String(Previous_Hash));

                    } while (!Hash.checkTrailingZero(bHash, _complexity, sGoal));
                    
                }
                else
                {
                    nonce++;
                }

                return nonce;
            }

            public static string ByteArrayToString(byte[] input)
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < input.Length; i++)
                {
                    sb.Append(input[i].ToString("X2"));
                }
                return sb.ToString();
            }

            public block GetLastBlock(string blockType = "")
            {
                if (string.IsNullOrEmpty(blockType))
                    return Chain.FirstOrDefault(t => Chain.Count(q => ByteArrayToString(q.previous_hash) == ByteArrayToString(t.hash)) == 0);
                else
                {
                    var oBlock = Chain.FirstOrDefault(t => Chain.Count(q => ByteArrayToString(q.previous_hash) == ByteArrayToString(t.hash)) == 0 && (t.blocktype == blockType));

                    //return genesis block if no other block was found
                    if (oBlock == null)
                        return GetBlock(0);
                    else
                        return oBlock;
                }
            }

            public block GetBlock(int index, string blockType = "")
            {
                if (string.IsNullOrEmpty(blockType))
                    return Chain.FirstOrDefault(t => t.index == index);
                else
                    return Chain.FirstOrDefault(t => t.index == index && t.blocktype == blockType);
            }
        }

        public class block
        {
            internal static int _complexity = 0;

            public int index { get; set; }

            public long timestamp { get; set; }

            public byte[] previous_hash { get; set; }

            public byte[] hash { get; set; }

            public long nonce { get; set; }

            public string data { get; set; }

            public byte[] signature { get; set; }

            public string blocktype { get; set; }

            public static byte[] GetHash(string input)
            {
                if (!string.IsNullOrEmpty(input))
                {
                    return Hash.CalculateSHA2_256Hash(input);
                }

                return null;
            }

            public void calc_hash()
            {
                try
                {
                    string sData = index.ToString() + timestamp.ToString() + previous_hash.ToString() + data.ToString() + nonce.ToString() + blocktype;

                    byte[] bHash = GetHash(sData);

                    //Do a ProofOfWork if complexity is > 0
                    bool DoWork = true;
                    DateTime dStart = DateTime.Now;

                    if (_complexity > 0 && nonce != 0 && DoWork)
                    {
                        string sGoal = new string('0', _complexity); //generate a string with leading '0'

                        if (!Hash.checkTrailingZero(bHash, _complexity, sGoal)) //only calc nonce if it's not valid
                        {
                            do
                            {
                                if (nonce >= 9223372036854775807) //check overflow
                                {
                                    nonce = 0; //reset nonce
                                    timestamp = DateTime.Now.Ticks; //reset timestamp
                                }
                                nonce++;

                                bHash = block.GetHash(index.ToString() + timestamp.ToString() + previous_hash.ToString() + data.ToString() + nonce.ToString() + blocktype);

                            } while (!Hash.checkTrailingZero(bHash, _complexity, sGoal));
                        }
                    }
                    TimeSpan tDur = DateTime.Now - dStart;
                    tDur.TotalMilliseconds.ToString();

                    hash = bHash;

                    signature = Sign(hash, ""); //Add CertSubject as Parameter
                }
                catch { }
            }

            static byte[] Sign(byte[] hash, string certSubject)
            {
                if (!string.IsNullOrEmpty(certSubject))
                {
                    // Access Personal (MY) certificate store of current user
                    X509Store my = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                    my.Open(OpenFlags.ReadOnly);

                    // Find the certificate we'll use to sign
                    foreach (X509Certificate2 cert in my.Certificates)
                    {
                        if (cert.Subject.Contains(certSubject) & cert.HasPrivateKey)
                        {
                            // We found it.
                            // Get its associated CSP and private key
                            using (var key = cert.GetRSAPrivateKey())
                            {
                                return key.SignHash(hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                            }
                        }
                    }
                }

                return new byte[0];
            }

            public bool validate(long Previous_nonce = 0)
            {
                if (data != null)
                {
                    /*if (signature == null)
                        return false;*/

                    if (hash == null)
                        return false;

                    //Check hash
                    byte[] bHash = GetHash(index.ToString() + timestamp.ToString() + previous_hash.ToString() + data.ToString() + nonce.ToString() + blocktype);
                    if (Convert.ToBase64String(bHash) != Convert.ToBase64String(hash))
                        return false;

                    //Check signature
                    /*X509Store my = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                    my.Open(OpenFlags.ReadOnly);
                    foreach (X509Certificate2 cert in my.Certificates)
                    {
                        if (cert.Subject.Contains("xxx"))
                        {
                            using (var key = cert.GetRSAPublicKey())
                            {
                                if (!key.VerifyHash(hash, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                                {
                                    return false;
                                }
                            }
                        }
                    }*/
                }

                if (index > 0)
                {
                    //Validate nonce...
                    string sGoal = new string('0', _complexity); //generate a string with leading '0'
                    if (!Hash.checkTrailingZero(hash, _complexity, sGoal))
                        return false;
                }

                return true;
            }
        }

        public class Base32Encoding
        {
            public static byte[] ToBytes(string input)
            {
                if (string.IsNullOrEmpty(input))
                {
                    throw new ArgumentNullException("input");
                }

                input = input.TrimEnd('='); //remove padding characters
                int byteCount = input.Length * 5 / 8; //this must be TRUNCATED
                byte[] returnArray = new byte[byteCount];

                byte curByte = 0, bitsRemaining = 8;
                int mask = 0, arrayIndex = 0;

                foreach (char c in input)
                {
                    int cValue = CharToValue(c);

                    if (bitsRemaining > 5)
                    {
                        mask = cValue << (bitsRemaining - 5);
                        curByte = (byte)(curByte | mask);
                        bitsRemaining -= 5;
                    }
                    else
                    {
                        mask = cValue >> (5 - bitsRemaining);
                        curByte = (byte)(curByte | mask);
                        returnArray[arrayIndex++] = curByte;
                        curByte = (byte)(cValue << (3 + bitsRemaining));
                        bitsRemaining += 3;
                    }
                }

                //if we didn't end with a full byte
                if (arrayIndex != byteCount)
                {
                    returnArray[arrayIndex] = curByte;
                }

                return returnArray;
            }

            public static string ToString(byte[] input)
            {
                if (input == null || input.Length == 0)
                {
                    throw new ArgumentNullException("input");
                }

                int charCount = (int)Math.Ceiling(input.Length / 5d) * 8;
                char[] returnArray = new char[charCount];

                byte nextChar = 0, bitsRemaining = 5;
                int arrayIndex = 0;

                foreach (byte b in input)
                {
                    nextChar = (byte)(nextChar | (b >> (8 - bitsRemaining)));
                    returnArray[arrayIndex++] = ValueToChar(nextChar);

                    if (bitsRemaining < 4)
                    {
                        nextChar = (byte)((b >> (3 - bitsRemaining)) & 31);
                        returnArray[arrayIndex++] = ValueToChar(nextChar);
                        bitsRemaining += 5;
                    }

                    bitsRemaining -= 3;
                    nextChar = (byte)((b << bitsRemaining) & 31);
                }

                //if we didn't end with a full char
                if (arrayIndex != charCount)
                {
                    returnArray[arrayIndex++] = ValueToChar(nextChar);
                    while (arrayIndex != charCount) returnArray[arrayIndex++] = '='; //padding
                }

                return new string(returnArray);
            }

            private static int CharToValue(char c)
            {
                int value = (int)c;

                //65-90 == uppercase letters
                if (value < 91 && value > 64)
                {
                    return value - 65;
                }
                //50-55 == numbers 2-7
                if (value < 56 && value > 49)
                {
                    return value - 24;
                }
                //97-122 == lowercase letters
                if (value < 123 && value > 96)
                {
                    return value - 97;
                }

                throw new ArgumentException("Character is not a Base32 character.", "c");
            }

            private static char ValueToChar(byte b)
            {
                if (b < 26)
                {
                    return (char)(b + 65);
                }

                if (b < 32)
                {
                    return (char)(b + 24);
                }

                throw new ArgumentException("Byte is not a value Base32 value.", "b");
            }

        }
    }
}
