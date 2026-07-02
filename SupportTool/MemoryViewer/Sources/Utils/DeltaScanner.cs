using System;
using System.Collections.Generic;

namespace MemoryViewer.Sources.Utils
{
    public static class DeltaScanner
    {
        public static List<IntPtr> GenerateAddresses(IntPtr baseAddress, int deltaFrom, int deltaTo, int stride)
        {
            var result = new List<IntPtr>();
            if (stride <= 0) stride = 4; // default safety

            // Step negatively
            for (int offset = 0; offset >= deltaFrom; offset -= stride)
            {
                if (offset < 0) result.Add(IntPtr.Add(baseAddress, offset));
            }
            
            // Step positively
            for (int offset = 0; offset <= deltaTo; offset += stride)
            {
                result.Add(IntPtr.Add(baseAddress, offset));
            }

            // Sort addresses ascending
            result.Sort((a, b) => a.ToInt64().CompareTo(b.ToInt64()));
            return result;
        }
    }
}
