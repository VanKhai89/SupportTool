using System.Collections.Generic;

namespace MemoryViewer.Sources.Models
{
    /// <summary>Trạng thái toàn bộ app, được serialize ra file JSON.</summary>
    public class AppState
    {
        public string BaseAddress  { get; set; } = "0x00000000";
        public int    DeltaFrom    { get; set; } = -64;
        public int    DeltaTo      { get; set; } = 256;
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
