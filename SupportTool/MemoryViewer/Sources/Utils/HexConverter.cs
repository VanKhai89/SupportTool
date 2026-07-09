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
                // Fallback: try hex even without 0x prefix (e.g. user typed "0BCA5618" directly)
                if (!isHex && long.TryParse(input, NumberStyles.HexNumber, null, out long hexFallback))
                {
                    result = (IntPtr)hexFallback;
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
                // Fallback: try hex even without 0x prefix
                if (!isHex && int.TryParse(input, NumberStyles.HexNumber, null, out int hexFallback))
                {
                    result = (IntPtr)hexFallback;
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
            return IntPtr.Size == 8
                ? "0x" + ((ulong)(long)value).ToString("X16")
                : "0x" + ((uint)(int)value).ToString("X8");
        }
        
        public static string ToHexString(int value)
        {
            return "0x" + value.ToString("X");
        }
    }
}
