// ************************************************************************************
//          jaindb (c) Copyright 2018 by Roger Zander
// ************************************************************************************

using System;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace jaindb
{
    public class Hash
    {
        //Base58 Digits
        private const string Digits = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

        public enum hashType { MD5, SHA2_256, unknown } //Implemented Hash types

        public static byte[] CalculateHash(string input, hashType HashType = hashType.MD5)
        {
            if (HashType == hashType.MD5)
                return CalculateMD5Hash(input);

            if (HashType == hashType.SHA2_256)
                return CalculateSHA2_256Hash(input);

            return null;
        }

        public static string CalculateHashString(string input, hashType HashType = hashType.MD5)
        {
            if (HashType == hashType.MD5)
                return CalculateMD5HashString(input);

            if (HashType == hashType.SHA2_256)
                return CalculateSHA2_256HashString(input);

            return null;
        }

        public static async Task<string> CalculateHashStringAsync(string input, hashType HashType = hashType.MD5, CancellationToken ct = default(CancellationToken))
        {
            if (HashType == hashType.MD5)
                return await CalculateMD5HashStringAsync(input, ct);

            if (HashType == hashType.SHA2_256)
                return await CalculateSHA2_256HashStringAsync(input, ct);

            return null;
        }

        public static byte[] CalculateMD5Hash(string input)
        {
            MD5 md5 = System.Security.Cryptography.MD5.Create();
            byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
            byte[] hash = md5.ComputeHash(inputBytes);
            byte[] mhash = new byte[hash.Length + 2];
            hash.CopyTo(mhash, 2);
            //Add Multihash identifier
            mhash[0] = 0xD5; //MD5
            mhash[1] = Convert.ToByte(hash.Length); //Hash legth
            return mhash;
        }

        public static async Task<byte[]> CalculateMD5HashAsync(string input, CancellationToken ct = default(CancellationToken))
        {
            return await Task.Run(() => CalculateMD5Hash(input), ct);
        }

        public static string CalculateMD5HashString(string input)
        {
            return Encode58(CalculateMD5Hash(input));
        }

        public static async Task<string> CalculateMD5HashStringAsync(string input, CancellationToken ct = default(CancellationToken))
        {
            return await Task.Run(() => CalculateMD5HashString(input), ct);
        }

        public static byte[] CalculateSHA2_256Hash(string input)
        {
            SHA256 sha = SHA256.Create();
            byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
            byte[] hash = sha.ComputeHash(inputBytes);
            byte[] mhash = new byte[hash.Length + 2]; //we need two additional bytes

            //Add Multihash identifier
            hash.CopyTo(mhash, 2);
            mhash[0] = 0x12; //SHA256
            mhash[1] = Convert.ToByte(hash.Length); //Hash length

            return mhash;
        }

        public static async Task<byte[]> CalculateSHA2_256HashAsync(string input, CancellationToken ct = default(CancellationToken))
        {
            return await Task.Run(() => CalculateSHA2_256Hash(input), ct);
        }

        public static string CalculateSHA2_256HashString(string input)
        {
            return Encode58(CalculateSHA2_256Hash(input));
        }

        public static async Task<string> CalculateSHA2_256HashStringAsync(string input, CancellationToken ct = default(CancellationToken))
        {
            return await Task.Run(() => CalculateSHA2_256HashString(input), ct);
        }

        public static bool checkTrailingZero(byte[] bHash, int complexity, string sGoal = "")
        {
            bool bRes = false;
            try
            {
                if (complexity > 0)
                {
                    if (string.IsNullOrEmpty(sGoal)) //create TrailingZero string if it does not exists
                        sGoal = new string('0', complexity);

                    //Check the last n Bits of the hash if they are 0, where n is the complexity
                    int iBytes = 1 + (complexity / 8); //Nr of bytes we have toc get
                    var aLast = bHash.Skip(bHash.Length - iBytes); //Get the last n Bytes
                    string sRes = string.Join("", aLast.Select(x => Convert.ToString(x, 2).PadLeft(8, '0'))); //Convert to bit string

                    if (sRes.Substring(sRes.Length - complexity) == sGoal) //do we have a match ?
                        return true;
                }
                else
                    return true;
            }
            catch { }

            return bRes;
        }

        public static string Encode58(byte[] data)
        {
            // Decode byte[] to BigInteger
            BigInteger intData = 0;
            for (int i = 0; i < data.Length; i++)
            {
                intData = intData * 256 + data[i];
            }

            // Encode BigInteger to Base58 string
            string result = "";
            while (intData > 0)
            {
                int remainder = (int)(intData % 58);
                intData /= 58;
                result = Digits[remainder] + result;
            }

            // Append `1` for each leading 0 byte
            for (int i = 0; i < data.Length && data[i] == 0; i++)
            {
                result = '1' + result;
            }

            return result;
        }

        //Source https://gist.github.com/CodesInChaos/3175971
        public static byte[] Decode58(string data)
        {
            // Decode Base58 string to BigInteger 
            BigInteger intData = 0;
            for (int i = 0; i < data.Length; i++)
            {
                int digit = Digits.IndexOf(data[i]); //Slow
                if (digit < 0)
                    throw new FormatException(string.Format("Invalid Base58 character `{0}` at position {1}", data[i], i));
                intData = intData * 58 + digit;
            }

            // Encode BigInteger to byte[]
            // Leading zero bytes get encoded as leading `1` characters
            int leadingZeroCount = data.TakeWhile(c => c == '1').Count();
            var leadingZeros = Enumerable.Repeat((byte)0, leadingZeroCount);
            var bytesWithoutLeadingZeros =
                intData.ToByteArray()
                .Reverse()// to big endian
                .SkipWhile(b => b == 0);//strip sign byte
            var result = leadingZeros.Concat(bytesWithoutLeadingZeros).ToArray();
            return result;
        }

        public static hashType GetHashType(string sHash)
        {
            if (!string.IsNullOrEmpty(sHash))
            {
                byte[] bIdentifier = Encoding.UTF8.GetBytes(sHash);
                if (bIdentifier[0] == 0x51)
                    return hashType.SHA2_256;
                if (bIdentifier[0] == 0x39)
                    return hashType.MD5;
            }
            return hashType.unknown;
        }

        public static hashType GetHashType(byte[] bHash)
        {
            if (bHash.Length > 5)
            {
                if (bHash[0] == 0x12)
                    return hashType.SHA2_256;
                if (bHash[0] == 0xD5)
                    return hashType.MD5;
            }
            return hashType.unknown;
        }
    }
}