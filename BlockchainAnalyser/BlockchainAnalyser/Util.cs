using System;
using System.Collections.Generic;
using System.Text;

namespace BlockchainAnalyser
{
    class Util
    {
        public static byte[] HexStringToByteArray(string hex)
        {
            int numChars = hex.Length;
            byte[] bytes = new byte[numChars / 2];
            for (int i = 0; i < numChars; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            return bytes;
        }

        public static string ByteArrayToString(byte[] byteArray)
        {
            return BitConverter.ToString(byteArray).Replace("-", "");
        }
    }
}
