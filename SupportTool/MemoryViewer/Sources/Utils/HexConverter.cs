using System;
using System.Globalization;

namespace MemoryViewer.Sources.Utils
{
    public static class HexConverter
    {
        public static bool TryParseHexOrDec(string input, out IntPtr result)
        {
            result = IntPtr.Zero;
            if (string.IsNullOrWhiteSpace(input)) return false;

            input = input.Trim().ToLower();
            bool isHex = input.StartsWith("0x");
            if (isHex) input = input.Substring(2);

            if (IntPtr.Size == 8)
            {
                if (long.TryParse(input, isHex ? NumberStyles.HexNumber : NumberStyles.Integer, null, out long parsedLong))
                {
                    result = (IntPtr)parsedLong;
                    return true;
                }
            }
            else
            {
                if (int.TryParse(input, isHex ? NumberStyles.HexNumber : NumberStyles.Integer, null, out int parsedInt))
                {
                    result = (IntPtr)parsedInt;
                    return true;
                }
            }
            return false;
        }

        public static bool TryParseOffset(string input, out int result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(input)) return false;
            
            input = input.Trim().ToLower();
            bool isHex = input.StartsWith("0x");
            if (isHex) input = input.Substring(2);

            return int.TryParse(input, isHex ? NumberStyles.HexNumber : NumberStyles.Integer, null, out result);
        }

        public static string ToHexString(IntPtr value)
        {
            return IntPtr.Size == 8 ? value.ToString("X16") : value.ToString("X8");
        }
        
        public static string ToHexString(int value)
        {
            return value.ToString("X");
        }
    }
}
