using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using MemoryViewer.Sources.Core;
using MemoryViewer.Sources.Models;
using MemoryViewer.Sources.Utils;

namespace MemoryViewer.Sources.Views
{
    public class MainForm : Form
    {
        // --- Controls ---
        private Button btnSelectProcess = null!;
        private Label lblStatus = null!;
        private TextBox txtBaseAddress = null!;
        private Button btnAddAddress = null!;
        private TextBox txtDeltaFrom = null!;
        private TextBox txtDeltaTo = null!;
        private ComboBox cmbStride = null!;
        private Button btnScan = null!;
        private Button btnSnapshot = null!;
        // Filter
        private TextBox txtFilterMin = null!;
        private TextBox txtFilterMax = null!;
        private CheckBox chkFilter = null!;
        private DataGridView gridMemory = null!;
        private StatusStrip statusStrip = null!;
        private ToolStripStatusLabel lblStatusEntries = null!;
        private ToolStripStatusLabel lblStatusSnapshot = null!;
        private ToolStripStatusLabel lblStatusLegend = null!;

        // --- Data ---
        private ProcessManager _processManager = null!;
        private MemoryReader _memoryReader = null!;
        private BindingList<MemoryItem> _memoryList = null!;
        private System.Windows.Forms.Timer _updateTimer = null!;

        // Map ComboBox stride text -> byte sizes
        private static readonly int[] StrideBytes = { 1, 2, 4, 8 };

        public MainForm()
        {
            InitializeComponent();
            SetupLogic();
        }

        private void InitializeComponent()
        {
            this.Text = "Memory Structure Viewer v1.0";
            this.Size = new Size(1000, 650);
            this.StartPosition = FormStartPosition.CenterScreen;

            // ─── Top Panel ───────────────────────────────────────
            var pnlTop = new Panel { Dock = DockStyle.Top, Height = 145, Padding = new Padding(10) };

            // Row 1: process
            btnSelectProcess = new Button { Text = "Select Process...", Location = new Point(10, 8), Width = 130, Height = 26 };
            lblStatus = new Label { Text = "Not Attached", Location = new Point(150, 12), Width = 420, Font = new Font(this.Font, FontStyle.Bold) };

            // Row 2: Base Address
            var lblBase = new Label { Text = "Base Address:", Location = new Point(10, 44), Width = 95, TextAlign = ContentAlignment.MiddleLeft };
            txtBaseAddress = new TextBox { Text = "0x00000000", Location = new Point(107, 41), Width = 150 };
            btnAddAddress = new Button { Text = "Add Address / Pointer", Location = new Point(267, 40), Width = 150, Height = 26 };

            // Row 3: Delta + Stride
            var lblFrom = new Label { Text = "Delta From:", Location = new Point(10, 76), Width = 70, TextAlign = ContentAlignment.MiddleLeft };
            txtDeltaFrom = new TextBox { Text = "-64", Location = new Point(82, 73), Width = 60 };
            var lblTo = new Label { Text = "To:", Location = new Point(150, 76), Width = 25, TextAlign = ContentAlignment.MiddleLeft };
            txtDeltaTo = new TextBox { Text = "256", Location = new Point(178, 73), Width = 60 };

            var lblStride = new Label { Text = "Stride:", Location = new Point(248, 76), Width = 42, TextAlign = ContentAlignment.MiddleLeft };
            cmbStride = new ComboBox { Location = new Point(292, 73), Width = 90, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbStride.Items.AddRange(new object[] { "1 Byte", "2 Bytes", "4 Bytes", "8 Bytes" });
            cmbStride.SelectedIndex = 2; // Default 4 bytes

            btnScan = new Button { Text = "Scan / Apply", Location = new Point(390, 72), Width = 110, Height = 26, BackColor = Color.LightBlue };

            // Row 4: Snapshot + Filter
            btnSnapshot = new Button { Text = "Take Snapshot (Apply Base)", Location = new Point(10, 108), Width = 200, Height = 26, Font = new Font(this.Font, FontStyle.Bold) };

            chkFilter = new CheckBox { Text = "Filter Value:", Location = new Point(220, 112), Width = 85, TextAlign = ContentAlignment.MiddleLeft };
            var lblFMin = new Label { Text = "Min:", Location = new Point(308, 112), Width = 30, TextAlign = ContentAlignment.MiddleLeft };
            txtFilterMin = new TextBox { Text = "0", Location = new Point(338, 109), Width = 70 };
            var lblFMax = new Label { Text = "Max:", Location = new Point(415, 112), Width = 32, TextAlign = ContentAlignment.MiddleLeft };
            txtFilterMax = new TextBox { Text = "9999999", Location = new Point(450, 109), Width = 80 };

            pnlTop.Controls.AddRange(new Control[] {
                btnSelectProcess, lblStatus,
                lblBase, txtBaseAddress, btnAddAddress,
                lblFrom, txtDeltaFrom, lblTo, txtDeltaTo, lblStride, cmbStride, btnScan,
                btnSnapshot, chkFilter, lblFMin, txtFilterMin, lblFMax, txtFilterMax
            });

            // ─── DataGridView ──────────────────────────────────────
            gridMemory = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.AliceBlue }
            };

            gridMemory.Columns.Add(new DataGridViewTextBoxColumn  { HeaderText = "Offset",    DataPropertyName = "OffsetString", Width = 80, ReadOnly = true });
            gridMemory.Columns.Add(new DataGridViewTextBoxColumn  { HeaderText = "Address",   DataPropertyName = "AddressHex",   Width = 105, ReadOnly = true });
            gridMemory.Columns.Add(new DataGridViewTextBoxColumn  { HeaderText = "Type",      DataPropertyName = "TypeLabel",    Width = 75, ReadOnly = true });
            gridMemory.Columns.Add(new DataGridViewTextBoxColumn  { HeaderText = "Hex",       DataPropertyName = "HexValue",     Width = 100, ReadOnly = true });
            gridMemory.Columns.Add(new DataGridViewTextBoxColumn  { HeaderText = "Int / Long",DataPropertyName = "IntDisplay",   Width = 130, ReadOnly = true });
            gridMemory.Columns.Add(new DataGridViewTextBoxColumn  { HeaderText = "Float / Dbl",DataPropertyName = "FloatDisplay",Width = 130, ReadOnly = true });
            gridMemory.Columns.Add(new DataGridViewTextBoxColumn  { HeaderText = "Label",     DataPropertyName = "Label",        AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            gridMemory.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Freeze",    DataPropertyName = "IsFrozen",     Width = 55 });

            // ─── StatusStrip ────────────────────────────────
            statusStrip = new StatusStrip();
            lblStatusEntries  = new ToolStripStatusLabel { Text = "Ready.", BorderSides = ToolStripStatusLabelBorderSides.Right, AutoSize = true };
            lblStatusSnapshot = new ToolStripStatusLabel { Text = "Snapshot: --", BorderSides = ToolStripStatusLabelBorderSides.Right, AutoSize = true };
            lblStatusLegend   = new ToolStripStatusLabel
            {
                Text = "  ● Xanh = Lớn hơn snapshot   ● Đỏ = Nhỏ hơn snapshot",
                ForeColor = Color.DimGray,
                Spring = true,
                TextAlign = ContentAlignment.MiddleRight
            };
            statusStrip.Items.AddRange(new ToolStripItem[] { lblStatusEntries, lblStatusSnapshot, lblStatusLegend });

            this.Controls.Add(gridMemory);
            this.Controls.Add(statusStrip);
            this.Controls.Add(pnlTop);
        }

        private void SetupLogic()
        {
            _processManager = new ProcessManager();
            _memoryReader   = new MemoryReader(_processManager);
            _memoryList     = new BindingList<MemoryItem>();
            gridMemory.DataSource = _memoryList;

            // ── Select Process ──────────────────────────────
            btnSelectProcess.Click += (s, e) =>
            {
                using var frm = new SelectProcessForm(_processManager);
                if (frm.ShowDialog(this) == DialogResult.OK && frm.SelectedProcess != null)
                {
                    if (_processManager.AttachProcess(frm.SelectedProcess))
                    {
                        lblStatus.Text = $"Attached: {frm.SelectedProcess.ProcessName} (ID: {frm.SelectedProcess.Id})";
                        lblStatus.ForeColor = Color.DarkGreen;
                    }
                }
            };

            // ── Add Address / Pointer ─────────────────────────
            btnAddAddress.Click += (s, e) =>
            {
                if (_processManager.ProcessHandle == IntPtr.Zero)
                {
                    MessageBox.Show("Attach to a process first.", "No Process");
                    return;
                }
                using var frm = new AddAddressForm(_memoryReader);
                if (frm.ShowDialog(this) == DialogResult.OK && frm.ResultItem != null)
                {
                    _memoryList.Add(frm.ResultItem);
                }
            };

            // ── Scan / Apply ─────────────────────────────────
            btnScan.Click += (s, e) =>
            {
                if (_processManager.ProcessHandle == IntPtr.Zero)
                {
                    MessageBox.Show("Attach to a process first.", "No Process");
                    return;
                }

                if (!HexConverter.TryParseHexOrDec(txtBaseAddress.Text, out IntPtr baseAddr))
                {
                    MessageBox.Show("Invalid base address.", "Error");
                    return;
                }

                if (!int.TryParse(txtDeltaFrom.Text, out int from))
                {
                    MessageBox.Show("Delta From không hợp lệ.", "Error"); return;
                }
                if (!int.TryParse(txtDeltaTo.Text, out int to))
                {
                    MessageBox.Show("Delta To không hợp lệ.", "Error"); return;
                }

                int stride = StrideBytes[cmbStride.SelectedIndex];
                var addresses = DeltaScanner.GenerateAddresses(baseAddr, from, to, stride);

                // Determine per-row data type from stride
                MemoryDataType dt = stride switch
                {
                    1 => MemoryDataType.Byte,
                    2 => MemoryDataType.Int16,
                    8 => MemoryDataType.Int64,
                    _ => MemoryDataType.Int32
                };

                _memoryList.Clear();
                foreach (var addr in addresses)
                {
                    long offset = (long)addr - (long)baseAddr;
                    string sign = offset >= 0 ? "+" : "";
                    _memoryList.Add(new MemoryItem
                    {
                        Address     = addr,
                        AddressHex  = HexConverter.ToHexString(addr),
                        OffsetString = $"{sign}0x{Math.Abs(offset):X}",
                        OffsetFromBase = (int)offset,
                        DataType    = dt
                    });
                }
            };

            // ── Snapshot ──────────────────────────────────────
            btnSnapshot.Click += (s, e) =>
            {
                foreach (var item in _memoryList)
                    item.TakeSnapshot();
                gridMemory.Refresh();
                lblStatusSnapshot.Text = $"Snapshot: Active ({DateTime.Now:HH:mm:ss})";
                lblStatusSnapshot.ForeColor = Color.DarkGreen;
            };

            // ── CellFormatting: Highlight Xanh/Đỏ, reset về mặc định khi bằng gốc ──
            gridMemory.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= _memoryList.Count) return;
                var item = _memoryList[e.RowIndex];
                string hdr = gridMemory.Columns[e.ColumnIndex].HeaderText;

                // Default (alternating row color)
                Color defaultBg = (e.RowIndex % 2 == 0) ? Color.White : Color.AliceBlue;

                if (hdr == "Int / Long")
                {
                    if (item.IntStatus > 0)      e.CellStyle.BackColor = Color.LightGreen;
                    else if (item.IntStatus < 0) e.CellStyle.BackColor = Color.LightPink;
                    else                          e.CellStyle.BackColor = defaultBg;
                }
                else if (hdr == "Float / Dbl")
                {
                    if (item.FloatStatus > 0)      e.CellStyle.BackColor = Color.LightGreen;
                    else if (item.FloatStatus < 0) e.CellStyle.BackColor = Color.LightPink;
                    else                            e.CellStyle.BackColor = defaultBg;
                }
            };

            // ── Real-time Timer 100ms ─────────────────────────
            _updateTimer = new System.Windows.Forms.Timer { Interval = 100 };
            _updateTimer.Tick += (s, e) =>
            {
                if (_processManager.ProcessHandle == IntPtr.Zero) return;

                double filterMin = 0, filterMax = 0;
                bool needFilter = chkFilter.Checked
                    && double.TryParse(txtFilterMin.Text, out filterMin)
                    && double.TryParse(txtFilterMax.Text, out filterMax);

                bool anyChanged = false;

                for (int i = _memoryList.Count - 1; i >= 0; i--)
                {
                    var item = _memoryList[i];

                    if (item.IsFrozen)
                    {
                        var frozen = item.GetFreezeBytes();
                        if (frozen.Length > 0)
                            _memoryReader.WriteBytes(item.Address, frozen);
                        continue;
                    }

                    byte[] bytes = _memoryReader.ReadBytes(item.Address, item.ByteCount);
                    item.UpdateFromBytes(bytes);
                    anyChanged = true;

                    // Apply filter: hide row if value out of range
                    if (needFilter && i < gridMemory.Rows.Count)
                    {
                        bool rowVisible = item.FloatValue >= filterMin && item.FloatValue <= filterMax;
                        try
                        {
                            // Must clear selection first - WinForms cannot hide the currently selected/focused row
                            if (!rowVisible && gridMemory.Rows[i].Selected)
                                gridMemory.ClearSelection();
                            gridMemory.Rows[i].Visible = rowVisible;
                        }
                        catch (InvalidOperationException) { /* skip if still currency manager position */ }
                    }
                    else if (!needFilter && i < gridMemory.Rows.Count && !gridMemory.Rows[i].Visible)
                    {
                        gridMemory.Rows[i].Visible = true;
                    }
                }

                if (anyChanged) gridMemory.Invalidate();

                // Update status bar
                int visibleCount = 0;
                for (int i = 0; i < gridMemory.Rows.Count; i++)
                    if (gridMemory.Rows[i].Visible) visibleCount++;
                string filterNote = needFilter ? $" | Filter: [{filterMin} – {filterMax}]" : "";
                lblStatusEntries.Text = $"Reading {visibleCount} / {_memoryList.Count} entries{filterNote}";
            };
            _updateTimer.Start();

            // ── Freeze via Checkbox click ─────────────────────
            gridMemory.CellValueChanged += (s, e) =>
            {
                if (e.RowIndex >= 0 && gridMemory.Columns[e.ColumnIndex].HeaderText == "Freeze")
                {
                    // Take snapshot of current value as the freeze target
                    var item = _memoryList[e.RowIndex];
                    if (item.IsFrozen) item.TakeSnapshot();
                }
            };
            gridMemory.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (gridMemory.IsCurrentCellDirty) gridMemory.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
        }
    }
}
