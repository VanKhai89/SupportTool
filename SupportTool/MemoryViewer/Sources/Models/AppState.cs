using System;
using System.Collections.Generic;

namespace MemoryViewer.Sources.Models
{
    /// <summary>
    /// Cấu hình base address: tĩnh hoặc pointer chain.
    /// </summary>
    public class BaseAddressConfig
    {
        /// <summary>true = pointer chain; false = địa chỉ tĩnh.</summary>
        public bool   IsPointer  { get; set; } = false;

        /// <summary>
        /// Biểu thức base:
        ///   - Static  → "0x0000000BCA5618"
        ///   - Pointer → "game.exe+0x1234" hoặc "0x00964CDC"
        /// </summary>
        public string Expression { get; set; } = "0x00000000";

        /// <summary>Danh sách offset (chỉ dùng khi IsPointer = true).</summary>
        public int[]  Offsets    { get; set; } = Array.Empty<int>();
    }

    /// <summary>Trạng thái toàn bộ app, được serialize ra file JSON.</summary>
    public class AppState
    {
        /// <summary>Cấu hình base address (pointer hoặc static).</summary>
        public BaseAddressConfig BaseConfig { get; set; } = new();

        // --- Legacy fallback (đọc file cũ) ---
        /// <summary>Dùng để migrate từ file JSON cũ (chỉ có BaseAddress string).</summary>
        public string? BaseAddress { get; set; }

        public int    DeltaFrom    { get; set; } = -1000;
        public int    DeltaTo      { get; set; } = 1000;
        public int    StrideIndex  { get; set; } = 2;

        public List<SavedMemoryItem> Items { get; set; } = new();
    }

    /// <summary>Phiên bản serializable của MemoryItem (chỉ lưu dữ liệu tĩnh).</summary>
    public class SavedMemoryItem
    {
        public string AddressHex    { get; set; } = string.Empty;
        public string OffsetString  { get; set; } = string.Empty;
        public int    OffsetFromBase{ get; set; }
        public string DataType      { get; set; } = nameof(MemoryDataType.Int32);
        public string Label         { get; set; } = string.Empty;
        public bool   IsFrozen      { get; set; }
        public bool   IsHidden      { get; set; }
    }
}
