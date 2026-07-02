using System;
using System.Drawing;
using System.Windows.Forms;
using MemoryViewer.Sources.Core;
using MemoryViewer.Sources.Models;
using MemoryViewer.Sources.Utils;

namespace MemoryViewer.Sources.Views
{
    /// <summary>
    /// Cửa sổ Add Address / Pointer
    /// - Cho nhập base address (hex/dec)
    /// - Chọn data type (Byte, 2 Bytes, 4 Bytes, Int64, Float, Double, String)
    /// - Tuỳ chọn bật Pointer Mode: nhập danh sách offset để giải mã chuỗi con trỏ
    /// - Preview giá trị thực tế realtime khi process đã attach
    /// - Trả về MemoryItem khi OK
    /// </summary>
    public class AddAddressForm : Form
    {
        // --- Controls ---
        private TextBox txtAddress;
        private Label lblPreview;
        private TextBox txtDescription;
        private ComboBox cmbType;
        private CheckBox chkPointer;
        private Panel pnlPointer;
        private ListBox lstOffsets;
        private TextBox txtNewOffset;
        private Button btnAddOffset;
        private Button btnRemoveOffset;
        private Button btnOk;
        private Button btnCancel;

        // --- Dependencies ---
        private readonly MemoryReader? _reader;

        // --- Output ---
        public MemoryItem? ResultItem { get; private set; }

        private static readonly string[] TypeItems =
            { "Byte", "2 Bytes (Int16)", "4 Bytes (Int32)", "8 Bytes (Int64)", "Float", "Double", "String" };

        private static readonly MemoryDataType[] TypeMap =
        {
            MemoryDataType.Byte,
            MemoryDataType.Int16,
            MemoryDataType.Int32,
            MemoryDataType.Int64,
            MemoryDataType.Float,
            MemoryDataType.Double,
            MemoryDataType.String,
        };

        public AddAddressForm(MemoryReader? reader = null)
        {
            _reader = reader;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Add Address / Pointer";
            this.Size = new Size(420, 540);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            // ----- Address -----
            var lblAddr = new Label { Text = "Address:", Location = new Point(10, 12), Width = 70 };
            txtAddress = new TextBox { Location = new Point(80, 9), Width = 200 };
            lblPreview = new Label { Text = "= ???", Location = new Point(285, 12), Width = 120, ForeColor = Color.DarkBlue };

            // ----- Description -----
            var lblDesc = new Label { Text = "Description:", Location = new Point(10, 42), Width = 80 };
            txtDescription = new TextBox { Location = new Point(90, 39), Width = 300 };

            // ----- Type -----
            var lblType = new Label { Text = "Type:", Location = new Point(10, 72), Width = 70 };
            cmbType = new ComboBox { Location = new Point(80, 69), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbType.Items.AddRange(TypeItems);
            cmbType.SelectedIndex = 2; // Default: 4 Bytes (Int32)

            // ----- Pointer checkbox -----
            chkPointer = new CheckBox { Text = "Pointer", Location = new Point(10, 105), Width = 100 };

            // ----- Pointer panel -----
            pnlPointer = new Panel
            {
                Location = new Point(10, 130),
                Size = new Size(385, 230),
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false
            };

            var lblOffsets = new Label { Text = "Offsets (thêm từng offset theo thứ tự duyệt pointer):", Location = new Point(5, 5), Width = 370 };
            lstOffsets = new ListBox { Location = new Point(5, 25), Size = new Size(370, 120), Font = new Font("Consolas", 9) };
            
            var lblNewOffset = new Label { Text = "Offset (hex):", Location = new Point(5, 155), Width = 75 };
            txtNewOffset = new TextBox { Location = new Point(82, 152), Width = 100, Text = "0" };
            btnAddOffset = new Button { Text = "Add", Location = new Point(190, 151), Width = 55 };
            btnRemoveOffset = new Button { Text = "Remove", Location = new Point(252, 151), Width = 70 };

            pnlPointer.Controls.AddRange(new Control[] { lblOffsets, lstOffsets, lblNewOffset, txtNewOffset, btnAddOffset, btnRemoveOffset });

            // ----- OK / Cancel -----
            btnOk     = new Button { Text = "OK",     Location = new Point(220, 480), Width = 80, DialogResult = DialogResult.OK };
            btnCancel = new Button { Text = "Cancel", Location = new Point(310, 480), Width = 80, DialogResult = DialogResult.Cancel };
            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;

            this.Controls.AddRange(new Control[] {
                lblAddr, txtAddress, lblPreview,
                lblDesc, txtDescription,
                lblType, cmbType,
                chkPointer, pnlPointer,
                btnOk, btnCancel
            });

            // ----- Events -----
            chkPointer.CheckedChanged += (s, e) =>
            {
                pnlPointer.Visible = chkPointer.Checked;
                this.Height = chkPointer.Checked ? 580 : 540;
                UpdatePreview();
            };

            txtAddress.TextChanged += (s, e) => UpdatePreview();
            cmbType.SelectedIndexChanged += (s, e) => UpdatePreview();
            lstOffsets.SelectedIndexChanged += (s, e) => UpdatePreview();

            btnAddOffset.Click += (s, e) =>
            {
                string raw = txtNewOffset.Text.Trim();
                if (string.IsNullOrEmpty(raw)) raw = "0";
                // Format: show as "0x..." for readability
                if (HexConverter.TryParseOffset(raw, out int _))
                {
                    lstOffsets.Items.Add(raw.ToLower().StartsWith("0x") ? raw : $"0x{raw}");
                    txtNewOffset.Clear();
                    UpdatePreview();
                }
                else
                {
                    MessageBox.Show("Offset không hợp lệ. Nhập số hex (0x1A4) hoặc dec (420).", "Invalid Offset");
                }
            };

            btnRemoveOffset.Click += (s, e) =>
            {
                if (lstOffsets.SelectedIndex >= 0)
                {
                    lstOffsets.Items.RemoveAt(lstOffsets.SelectedIndex);
                    UpdatePreview();
                }
            };

            btnOk.Click += (s, e) =>
            {
                if (!TryBuildResult(out var item, out string error))
                {
                    MessageBox.Show(error, "Invalid Input");
                    this.DialogResult = DialogResult.None;
                    return;
                }
                ResultItem = item;
            };
        }

        private void UpdatePreview()
        {
            if (_reader == null)
            {
                lblPreview.Text = "(no process)";
                return;
            }

            try
            {
                if (!HexConverter.TryParseHexOrDec(txtAddress.Text, out IntPtr baseAddr))
                {
                    lblPreview.Text = "= ???";
                    return;
                }

                IntPtr resolved = baseAddr;

                if (chkPointer.Checked && lstOffsets.Items.Count > 0)
                {
                    var offsets = BuildOffsetArray();
                    resolved = _reader.ResolvePointer(baseAddr, offsets);
                }

                if (resolved == IntPtr.Zero)
                {
                    lblPreview.Text = "= (null)";
                    return;
                }

                MemoryDataType dt = TypeMap[cmbType.SelectedIndex];
                int byteCount = dt switch
                {
                    MemoryDataType.Byte   => 1,
                    MemoryDataType.Int16  => 2,
                    MemoryDataType.Int32  => 4,
                    MemoryDataType.Int64  => 8,
                    MemoryDataType.Float  => 4,
                    MemoryDataType.Double => 8,
                    _                     => 4
                };

                byte[] bytes = _reader.ReadBytes(resolved, byteCount);
                string preview = dt switch
                {
                    MemoryDataType.Byte   => bytes[0].ToString(),
                    MemoryDataType.Int16  => BitConverter.ToInt16(bytes, 0).ToString(),
                    MemoryDataType.Int32  => BitConverter.ToInt32(bytes, 0).ToString(),
                    MemoryDataType.Int64  => BitConverter.ToInt64(bytes, 0).ToString(),
                    MemoryDataType.Float  => BitConverter.ToSingle(bytes, 0).ToString("0.####"),
                    MemoryDataType.Double => BitConverter.ToDouble(bytes, 0).ToString("0.####"),
                    _                     => "(string)"
                };

                lblPreview.Text = $"= {preview}";
            }
            catch
            {
                lblPreview.Text = "= (error)";
            }
        }

        private int[] BuildOffsetArray()
        {
            var result = new int[lstOffsets.Items.Count];
            for (int i = 0; i < lstOffsets.Items.Count; i++)
            {
                HexConverter.TryParseOffset(lstOffsets.Items[i]?.ToString() ?? "0", out result[i]);
            }
            return result;
        }

        private bool TryBuildResult(out MemoryItem? item, out string error)
        {
            item = null;
            if (!HexConverter.TryParseHexOrDec(txtAddress.Text, out IntPtr baseAddr))
            {
                error = "Địa chỉ không hợp lệ. Nhập hex (0x00401000) hoặc dec.";
                return false;
            }

            IntPtr resolved = baseAddr;
            if (chkPointer.Checked && lstOffsets.Items.Count > 0 && _reader != null)
            {
                resolved = _reader.ResolvePointer(baseAddr, BuildOffsetArray());
                if (resolved == IntPtr.Zero)
                {
                    error = "Chuỗi con trỏ không hợp lệ, trả về null.";
                    return false;
                }
            }

            MemoryDataType dt = TypeMap[cmbType.SelectedIndex];
            item = new MemoryItem
            {
                Address = resolved,
                AddressHex = HexConverter.ToHexString(resolved),
                OffsetString = "+0x00",
                Label = txtDescription.Text,
                DataType = dt,
            };
            error = string.Empty;
            return true;
        }
    }
}
