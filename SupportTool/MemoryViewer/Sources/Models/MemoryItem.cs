using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace MemoryViewer.Sources.Models
{
    /// <summary>Loại dữ liệu được đọc tại mỗi địa chỉ.</summary>
    public enum MemoryDataType
    {
        Byte = 1,
        Int16 = 2,
        Int32 = 4,
        Int64 = 8,
        Float = -4,
        Double = -8,
        String = 0,
    }

    public class MemoryItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        // --- Static info ---
        public IntPtr Address { get; set; }
        public int OffsetFromBase { get; set; }

        private string _offsetString = string.Empty;
        public string OffsetString
        {
            get => _offsetString;
            set { _offsetString = value; OnPropertyChanged(); }
        }

        private string _addressHex = string.Empty;
        public string AddressHex
        {
            get => _addressHex;
            set { _addressHex = value; OnPropertyChanged(); }
        }

        private string _label = string.Empty;
        public string Label
        {
            get => _label;
            set { _label = value; OnPropertyChanged(); }
        }

        private bool _isFrozen;
        public bool IsFrozen
        {
            get => _isFrozen;
            set { _isFrozen = value; OnPropertyChanged(); }
        }

        private bool _isHidden;
        public bool IsHidden
        {
            get => _isHidden;
            set { _isHidden = value; OnPropertyChanged(); }
        }

        // --- Per-row data type: drives reading stride ---
        private MemoryDataType _dataType = MemoryDataType.Int32;
        public MemoryDataType DataType
        {
            get => _dataType;
            set { _dataType = value; OnPropertyChanged(); OnPropertyChanged(nameof(TypeLabel)); }
        }
        public string TypeLabel => DataType switch
        {
            MemoryDataType.Byte   => "Byte",
            MemoryDataType.Int16  => "2 Bytes",
            MemoryDataType.Int32  => "4 Bytes",
            MemoryDataType.Int64  => "8 Bytes",
            MemoryDataType.Float  => "Float",
            MemoryDataType.Double => "Double",
            MemoryDataType.String => "String",
            _ => "?"
        };

        // --- Current live values ---
        private string _hexValue = "--------";
        public string HexValue
        {
            get => _hexValue;
            set { _hexValue = value; OnPropertyChanged(); }
        }

        // Raw bytes for interpreting as all types
        private byte[] _rawBytes = new byte[8];

        // Int32 view (also used for Byte / Int16 / Int64 display)
        private long _intValue;
        public long IntValue
        {
            get => _intValue;
            set
            {
                if (_intValue == value) return;
                _intValue = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IntDisplay));
                OnPropertyChanged(nameof(IntStatus));
            }
        }

        // Float view
        private double _floatValue;
        public double FloatValue
        {
            get => _floatValue;
            set
            {
                if (_floatValue == value) return;
                _floatValue = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FloatDisplay));
                OnPropertyChanged(nameof(FloatStatus));
            }
        }

        // --- Snapshot ---
        public long? SnapshotInt { get; private set; }
        public double? SnapshotFloat { get; private set; }

        public void TakeSnapshot()
        {
            SnapshotInt = IntValue;
            SnapshotFloat = FloatValue;
            OnPropertyChanged(nameof(IntDisplay));
            OnPropertyChanged(nameof(IntStatus));
            OnPropertyChanged(nameof(FloatDisplay));
            OnPropertyChanged(nameof(FloatStatus));
        }

        // --- Status: 0 neutral, 1 increased, -1 decreased ---
        public int IntStatus
        {
            get
            {
                if (!SnapshotInt.HasValue) return 0;
                return IntValue > SnapshotInt.Value ? 1 : IntValue < SnapshotInt.Value ? -1 : 0;
            }
        }

        public string IntDisplay
        {
            get
            {
                if (!SnapshotInt.HasValue || IntStatus == 0) return IntValue.ToString();
                long delta = IntValue - SnapshotInt.Value;
                return $"{IntValue} ({(delta > 0 ? "+" : "")}{delta})";
            }
        }

        public int FloatStatus
        {
            get
            {
                if (!SnapshotFloat.HasValue) return 0;
                return FloatValue > SnapshotFloat.Value ? 1 : FloatValue < SnapshotFloat.Value ? -1 : 0;
            }
        }

        public string FloatDisplay
        {
            get
            {
                if (!SnapshotFloat.HasValue || FloatStatus == 0) return FloatValue.ToString("0.####");
                double delta = FloatValue - SnapshotFloat.Value;
                return $"{FloatValue:0.####} ({(delta > 0 ? "+" : "")}{delta:0.####})";
            }
        }

        // --- Update from raw bytes ---
        public void UpdateFromBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return;
            _rawBytes = bytes;

            // Always compute hex from the real byte width
            var sb = new StringBuilder();
            for (int i = bytes.Length - 1; i >= 0; i--)
                sb.Append(bytes[i].ToString("X2"));
            HexValue = sb.ToString();

            switch (DataType)
            {
                case MemoryDataType.Byte:
                    IntValue = bytes[0];
                    FloatValue = bytes[0];
                    break;
                case MemoryDataType.Int16:
                    IntValue = BitConverter.ToInt16(bytes, 0);
                    FloatValue = BitConverter.ToInt16(bytes, 0);
                    break;
                case MemoryDataType.Int32:
                    IntValue = BitConverter.ToInt32(bytes, 0);
                    FloatValue = BitConverter.ToSingle(bytes, 0);
                    break;
                case MemoryDataType.Int64:
                    IntValue = BitConverter.ToInt64(bytes, 0);
                    FloatValue = BitConverter.ToDouble(bytes, 0);
                    break;
                case MemoryDataType.Float:
                    IntValue = BitConverter.ToInt32(bytes, 0);
                    FloatValue = BitConverter.ToSingle(bytes, 0);
                    break;
                case MemoryDataType.Double:
                    IntValue = BitConverter.ToInt64(bytes, 0);
                    FloatValue = BitConverter.ToDouble(bytes, 0);
                    break;
                default:
                    break;
            }
        }

        /// <summary>Số byte cần đọc từ RAM dựa theo DataType.</summary>
        public int ByteCount => DataType switch
        {
            MemoryDataType.Byte   => 1,
            MemoryDataType.Int16  => 2,
            MemoryDataType.Int32  => 4,
            MemoryDataType.Int64  => 8,
            MemoryDataType.Float  => 4,
            MemoryDataType.Double => 8,
            _                     => 4
        };

        /// <summary>Trả về raw bytes để freeze (ghi lại vào RAM).</summary>
        public byte[] GetFreezeBytes() => _rawBytes;
    }
}
