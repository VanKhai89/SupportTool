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
        // Tab + Grid
        private TabControl tabControl = null!;
        private TabPage tabAll = null!;
        private TabPage tabActive = null!;
        private DataGridView gridAll = null!;
        private DataGridView gridActive = null!;
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
            LoadState();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  UI Setup
        // ─────────────────────────────────────────────────────────────────────
        private void InitializeComponent()
        {
            this.Text = "Memory Structure Viewer v1.0";
            this.Size = new Size(1200, 700);
            this.StartPosition = FormStartPosition.CenterScreen;

            // ─── Top Panel ───────────────────────────────────────
            const int PH   = 130;   // panel height
            const int RH   = 26;    // row control height
            const int PAD  = 8;     // left/right margin
            const int YGAP = 34;    // row spacing

            var pnlTop = new Panel
            {
                Dock        = DockStyle.Top,
                Height      = PH,
                BackColor   = Color.FromArgb(245, 246, 250),
                Padding     = new Padding(PAD)
            };

            // ── Row 1: Select Process (left) ──────────────────────────────
            int y1 = PAD;
            btnSelectProcess = new Button
            {
                Text = "Select Process...",
                Location = new Point(PAD, y1),
                Width = 140, Height = RH,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White
            };
            btnSelectProcess.FlatAppearance.BorderColor = Color.SteelBlue;

            lblStatus = new Label
            {
                Text      = "● Not Attached",
                Location  = new Point(PAD + 148, y1 + 4),
                Width     = 420,
                ForeColor = Color.Gray,
                Font      = new Font(this.Font, FontStyle.Bold)
            };

            // ── Row 2: Base Address + Add Address (left)  ─────────────────
            int y2 = y1 + YGAP;
            var lblBase = new Label
            {
                Text      = "Base Address:",
                Location  = new Point(PAD, y2 + 4),
                Width     = 92,
                TextAlign = ContentAlignment.MiddleLeft
            };
            txtBaseAddress = new TextBox
            {
                Text     = "0x00000000",
                Location = new Point(PAD + 94, y2),
                Width    = 160,
                Height   = RH,
                Font     = new Font("Consolas", 9)
            };
            btnAddAddress = new Button
            {
                Text      = "Add Address / Pointer",
                Location  = new Point(PAD + 94 + 164, y2),
                Width     = 160, Height = RH,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White
            };
            btnAddAddress.FlatAppearance.BorderColor = Color.SteelBlue;

            // ── Row 3: Delta + Stride + Scan (left) ───────────────────────
            int y3 = y2 + YGAP;
            var lblFrom = new Label
            {
                Text      = "Delta From:",
                Location  = new Point(PAD, y3 + 4),
                Width     = 68,
                TextAlign = ContentAlignment.MiddleLeft
            };
            txtDeltaFrom = new TextBox
            {
                Text     = "-64",
                Location = new Point(PAD + 70, y3),
                Width    = 60, Height = RH,
                Font     = new Font("Consolas", 9),
                TextAlign = HorizontalAlignment.Center
            };
            var lblTo = new Label
            {
                Text      = "To:",
                Location  = new Point(PAD + 136, y3 + 4),
                Width     = 22,
                TextAlign = ContentAlignment.MiddleLeft
            };
            txtDeltaTo = new TextBox
            {
                Text      = "256",
                Location  = new Point(PAD + 160, y3),
                Width     = 60, Height = RH,
                Font      = new Font("Consolas", 9),
                TextAlign = HorizontalAlignment.Center
            };
            var lblStride = new Label
            {
                Text      = "Stride:",
                Location  = new Point(PAD + 228, y3 + 4),
                Width     = 44,
                TextAlign = ContentAlignment.MiddleLeft
            };
            cmbStride = new ComboBox
            {
                Location      = new Point(PAD + 274, y3),
                Width         = 90,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbStride.Items.AddRange(new object[] { "1 Byte", "2 Bytes", "4 Bytes", "8 Bytes" });
            cmbStride.SelectedIndex = 2;

            btnScan = new Button
            {
                Text      = "▶  Scan / Apply",
                Location  = new Point(PAD + 370, y3),
                Width     = 120, Height = RH,
                BackColor = Color.FromArgb(100, 180, 240),
                FlatStyle = FlatStyle.Flat,
                Font      = new Font(this.Font, FontStyle.Bold)
            };
            btnScan.FlatAppearance.BorderColor = Color.SteelBlue;

            // ── Row 1–3 RIGHT side: Snapshot + Filter + Save/Load ─────────
            //  These controls are pinned to the RIGHT of pnlTop via Anchor.
            //  We use absolute positions that get adjusted by the Anchor.

            // Snapshot button – anchored left so it stays at right side
            btnSnapshot = new Button
            {
                Text      = "📷  Take Snapshot",
                Location  = new Point(PAD, y1),    // repositioned below with Anchor
                Width     = 150, Height = RH,
                Font      = new Font(this.Font, FontStyle.Bold),
                BackColor = Color.FromArgb(255, 235, 150),
                FlatStyle = FlatStyle.Flat,
                Anchor    = AnchorStyles.Top | AnchorStyles.Right
            };
            btnSnapshot.FlatAppearance.BorderColor = Color.Goldenrod;

            // Save / Load – anchored right
            var btnSave = new Button
            {
                Text      = "💾 Save",
                Width     = 80, Height = RH,
                BackColor = Color.FromArgb(180, 230, 180),
                FlatStyle = FlatStyle.Flat,
                Anchor    = AnchorStyles.Top | AnchorStyles.Right
            };
            btnSave.FlatAppearance.BorderColor = Color.SeaGreen;
            var btnLoad = new Button
            {
                Text      = "📂 Load",
                Width     = 80, Height = RH,
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Anchor    = AnchorStyles.Top | AnchorStyles.Right
            };
            btnLoad.FlatAppearance.BorderColor = Color.SteelBlue;
            btnSave.Click += (s, e) => SaveState();
            btnLoad.Click += (s, e) => LoadState();

            // Filter row – anchored right
            chkFilter = new CheckBox
            {
                Text      = "Filter:",
                Width     = 55, Height = RH,
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor    = AnchorStyles.Top | AnchorStyles.Right
            };
            var lblFMin = new Label
            {
                Text      = "Min:",
                Width     = 30, Height = RH,
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor    = AnchorStyles.Top | AnchorStyles.Right
            };
            txtFilterMin = new TextBox
            {
                Text      = "0",
                Width     = 65, Height = RH,
                Font      = new Font("Consolas", 9),
                TextAlign = HorizontalAlignment.Center,
                Anchor    = AnchorStyles.Top | AnchorStyles.Right
            };
            var lblFMax = new Label
            {
                Text      = "Max:",
                Width     = 32, Height = RH,
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor    = AnchorStyles.Top | AnchorStyles.Right
            };
            txtFilterMax = new TextBox
            {
                Text      = "9999999",
                Width     = 80, Height = RH,
                Font      = new Font("Consolas", 9),
                TextAlign = HorizontalAlignment.Center,
                Anchor    = AnchorStyles.Top | AnchorStyles.Right
            };

            // ── Use a right-side helper panel to keep right-group tidy ────
            //  FlowLayoutPanel flowing RIGHT → LEFT using RightToLeft + reversed order
            var pnlRight = new Panel
            {
                Dock      = DockStyle.Right,
                Width     = 530,
                BackColor = Color.Transparent
            };

            // Manually position inside pnlRight (width=530, rows aligned with pnlTop rows)
            int rw = 530; // pnlRight width
            int rx; // running x from right

            // Row 1: [Save] [Load]   (right-aligned)
            btnLoad.Location  = new Point(rw - PAD - 80, y1);
            btnSave.Location  = new Point(rw - PAD - 80 - 86, y1);
            btnSnapshot.Location = new Point(rw - PAD - 80 - 86 - 158, y1);

            // Row 2: [Filter:] [Min] [txtMin] [Max] [txtMax]  (right-aligned)
            rx = rw - PAD;
            txtFilterMax.Location = new Point(rx - 80,  y2);   rx -= 80 + 4;
            lblFMax.Location      = new Point(rx - 32,  y2 + 4); rx -= 32 + 2;
            txtFilterMin.Location = new Point(rx - 65,  y2);   rx -= 65 + 4;
            lblFMin.Location      = new Point(rx - 30,  y2 + 4); rx -= 30 + 2;
            chkFilter.Location    = new Point(rx - 55,  y2 + 2); 

            pnlRight.Controls.AddRange(new Control[]
            {
                btnSnapshot, btnSave, btnLoad,
                chkFilter, lblFMin, txtFilterMin, lblFMax, txtFilterMax
            });

            pnlTop.Controls.AddRange(new Control[] {
                btnSelectProcess, lblStatus,
                lblBase, txtBaseAddress, btnAddAddress,
                lblFrom, txtDeltaFrom, lblTo, txtDeltaTo, lblStride, cmbStride, btnScan,
                pnlRight
            });

            // ─── TabControl ────────────────────────────────────────
            tabControl = new TabControl { Dock = DockStyle.Fill };
            tabAll    = new TabPage { Text = "🔵 All" };
            tabActive = new TabPage { Text = "✅ Active" };

            gridAll    = CreateGrid();
            gridActive = CreateGrid();

            tabAll.Controls.Add(gridAll);
            tabActive.Controls.Add(gridActive);
            tabControl.TabPages.Add(tabAll);
            tabControl.TabPages.Add(tabActive);

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

            this.Controls.Add(tabControl);
            this.Controls.Add(statusStrip);
            this.Controls.Add(pnlTop);

            // Enable High DPI AutoScaling
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
        }

        /// <summary>Tạo DataGridView với đầy đủ cột, dùng chung cho cả 2 tab.</summary>
        private DataGridView CreateGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,   // cho phép chọn nhiều hàng
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.AliceBlue }
            };

            // ── Right-click context menu: Show / Hide ─────────────────
            var ctxMenu   = new ContextMenuStrip();
            var miShow    = new ToolStripMenuItem("👁  Show selected");
            var miHide    = new ToolStripMenuItem("🚫 Hide selected");
            var miSep     = new ToolStripSeparator();
            var miDelSel  = new ToolStripMenuItem("🗑  Delete selected");
            ctxMenu.Items.AddRange(new ToolStripItem[] { miShow, miHide, miSep, miDelSel });

            // Select the right-clicked row if it isn't already selected
            grid.MouseDown += (s, e) =>
            {
                if (e.Button != MouseButtons.Right) return;
                var hit = grid.HitTest(e.X, e.Y);
                if (hit.RowIndex < 0) return;
                // If the clicked row is not in the current selection, select only that row
                if (!grid.Rows[hit.RowIndex].Selected)
                {
                    grid.ClearSelection();
                    grid.Rows[hit.RowIndex].Selected = true;
                }
            };

            miShow.Click += (s, e) =>
            {
                foreach (DataGridViewRow row in grid.SelectedRows)
                {
                    var item = GetItemFromRow(row.Index);
                    if (item != null) item.IsHidden = false;
                }
                RefreshActiveTabVisibility();
            };

            miHide.Click += (s, e) =>
            {
                foreach (DataGridViewRow row in grid.SelectedRows)
                {
                    var item = GetItemFromRow(row.Index);
                    if (item != null) item.IsHidden = true;
                }
                RefreshActiveTabVisibility();
            };

            miDelSel.Click += (s, e) =>
            {
                // Collect indices to remove (descending to keep indices valid)
                var indices = new System.Collections.Generic.List<int>();
                foreach (DataGridViewRow row in grid.SelectedRows)
                    indices.Add(row.Index);
                indices.Sort((a, b) => b.CompareTo(a));
                foreach (int idx in indices)
                {
                    if (idx < _memoryList.Count)
                        _memoryList.RemoveAt(idx);
                }
                RefreshActiveTabVisibility();
            };

            grid.ContextMenuStrip = ctxMenu;

            grid.Columns.Add(new DataGridViewTextBoxColumn  { HeaderText = "Offset",     DataPropertyName = "OffsetString", Width = 80,  ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn  { HeaderText = "Address",    DataPropertyName = "AddressHex",   Width = 105, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn  { HeaderText = "Type",       DataPropertyName = "TypeLabel",    Width = 75,  ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn  { HeaderText = "Hex",        DataPropertyName = "HexValue",     Width = 100, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn  { HeaderText = "Int / Long", DataPropertyName = "IntDisplay",   Width = 130, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn  { HeaderText = "Float / Dbl",DataPropertyName = "FloatDisplay", Width = 130, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn  { HeaderText = "Label",      DataPropertyName = "Label",        AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Freeze",     DataPropertyName = "IsFrozen",     Width = 55 });
            grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Hide",       DataPropertyName = "IsHidden",     Width = 50 });

            // CellFormatting: Highlight Xanh/Đỏ
            grid.CellFormatting += Grid_CellFormatting;

            // Commit checkbox edits immediately
            grid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            // Freeze via Checkbox click
            grid.CellValueChanged += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                var col = grid.Columns[e.ColumnIndex];
                if (col.HeaderText == "Freeze")
                {
                    var item = GetItemFromRow(grid, e.RowIndex);
                    if (item != null && item.IsFrozen) item.TakeSnapshot();
                }
                // When "Hide" is toggled, refresh the Active tab visibility
                if (col.HeaderText == "Hide")
                {
                    RefreshActiveTabVisibility();
                }
            };

            return grid;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Logic Setup
        // ─────────────────────────────────────────────────────────────────────
        private void SetupLogic()
        {
            _processManager = new ProcessManager();
            _memoryReader   = new MemoryReader(_processManager);
            _memoryList     = new BindingList<MemoryItem>();

            // Both grids share the same data source
            gridAll.DataSource    = _memoryList;
            gridActive.DataSource = _memoryList;

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
            // Cho phép mở dialog kể cả chưa attach (để nhập raw address).
            // Nếu dùng Pointer mode thì cần process đã attach để resolve.
            btnAddAddress.Click += (s, e) =>
            {
                var reader = _processManager.ProcessHandle != IntPtr.Zero ? _memoryReader : null;
                using var frm = new AddAddressForm(reader, _processManager);
                if (frm.ShowDialog(this) == DialogResult.OK && frm.ResolvedAddress != IntPtr.Zero)
                {
                    // Gán địa chỉ đã resolve vào Base Address
                    txtBaseAddress.Text = HexConverter.ToHexString(frm.ResolvedAddress);
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
                { MessageBox.Show("Delta From không hợp lệ.", "Error"); return; }
                if (!int.TryParse(txtDeltaTo.Text, out int to))
                { MessageBox.Show("Delta To không hợp lệ.", "Error"); return; }

                int stride = StrideBytes[cmbStride.SelectedIndex];
                var addresses = DeltaScanner.GenerateAddresses(baseAddr, from, to, stride);

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
                        Address        = addr,
                        AddressHex     = HexConverter.ToHexString(addr),
                        OffsetString   = $"{sign}0x{Math.Abs(offset):X}",
                        OffsetFromBase = (int)offset,
                        DataType       = dt
                    });
                }
                RefreshActiveTabVisibility();
            };

            // ── Snapshot ──────────────────────────────────────
            btnSnapshot.Click += (s, e) =>
            {
                foreach (var item in _memoryList)
                    item.TakeSnapshot();
                gridAll.Refresh();
                gridActive.Refresh();
                lblStatusSnapshot.Text = $"Snapshot: Active ({DateTime.Now:HH:mm:ss})";
                lblStatusSnapshot.ForeColor = Color.DarkGreen;
            };

            // ── Real-time Timer 100ms ─────────────────────────
            _updateTimer = new System.Windows.Forms.Timer { Interval = 100 };
            _updateTimer.Tick += Timer_Tick;
            _updateTimer.Start();

            // ── Save on close ─────────────────────────────────
            this.FormClosing += (s, e) => SaveState();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Timer Tick
        // ─────────────────────────────────────────────────────────────────────
        private void Timer_Tick(object? sender, EventArgs e)
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

                // Apply value filter on ALL grid
                if (i < gridAll.Rows.Count)
                {
                    bool passFilter = !needFilter || (item.FloatValue >= filterMin && item.FloatValue <= filterMax);
                    bool wantVisible = passFilter; // in "All" tab: visible regardless of IsHidden
                    try
                    {
                        if (!wantVisible && gridAll.Rows[i].Selected) gridAll.ClearSelection();
                        gridAll.Rows[i].Visible = wantVisible;
                    }
                    catch (InvalidOperationException) { }
                }

                // Apply hide + value filter on ACTIVE grid
                if (i < gridActive.Rows.Count)
                {
                    bool passFilter = !needFilter || (item.FloatValue >= filterMin && item.FloatValue <= filterMax);
                    bool wantVisible = passFilter && !item.IsHidden;
                    try
                    {
                        if (!wantVisible && gridActive.Rows[i].Selected) gridActive.ClearSelection();
                        gridActive.Rows[i].Visible = wantVisible;
                    }
                    catch (InvalidOperationException) { }
                }
            }

            if (anyChanged)
            {
                gridAll.Invalidate();
                gridActive.Invalidate();
            }

            // Status bar
            int visibleAll    = 0;
            int visibleActive = 0;
            for (int i = 0; i < gridAll.Rows.Count; i++)
                if (gridAll.Rows[i].Visible) visibleAll++;
            for (int i = 0; i < gridActive.Rows.Count; i++)
                if (gridActive.Rows[i].Visible) visibleActive++;

            string filterNote = needFilter ? $" | Filter: [{filterMin} – {filterMax}]" : "";
            lblStatusEntries.Text = $"All: {visibleAll} | Active: {visibleActive} / {_memoryList.Count} entries{filterNote}";
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Cell Formatting (shared handler)
        // ─────────────────────────────────────────────────────────────────────
        private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            var grid = sender as DataGridView;
            if (grid == null || e.RowIndex < 0 || e.RowIndex >= _memoryList.Count) return;
            var item = _memoryList[e.RowIndex];
            string hdr = grid.Columns[e.ColumnIndex].HeaderText;

            Color defaultBg = (e.RowIndex % 2 == 0) ? Color.White : Color.AliceBlue;

            if (hdr == "Int / Long")
            {
                if (item.IntStatus > 0)       e.CellStyle.BackColor = Color.LightGreen;
                else if (item.IntStatus < 0)  e.CellStyle.BackColor = Color.LightPink;
                else                           e.CellStyle.BackColor = defaultBg;
            }
            else if (hdr == "Float / Dbl")
            {
                if (item.FloatStatus > 0)      e.CellStyle.BackColor = Color.LightGreen;
                else if (item.FloatStatus < 0) e.CellStyle.BackColor = Color.LightPink;
                else                            e.CellStyle.BackColor = defaultBg;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Đồng bộ visibility của Active grid ngay sau khi Hide thay đổi.</summary>
        private void RefreshActiveTabVisibility()
        {
            for (int i = 0; i < _memoryList.Count && i < gridActive.Rows.Count; i++)
            {
                bool wantVisible = !_memoryList[i].IsHidden;
                try
                {
                    if (!wantVisible && gridActive.Rows[i].Selected) gridActive.ClearSelection();
                    gridActive.Rows[i].Visible = wantVisible;
                }
                catch (InvalidOperationException) { }
            }
        }

        /// <summary>Lấy MemoryItem từ row index, xử lý cả 2 grid cùng DataSource.</summary>
        private MemoryItem? GetItemFromRow(DataGridView grid, int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _memoryList.Count) return null;
            return _memoryList[rowIndex];
        }

        /// <summary>Overload không cần grid (dùng trong context menu).</summary>
        private MemoryItem? GetItemFromRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _memoryList.Count) return null;
            return _memoryList[rowIndex];
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Save / Load State
        // ─────────────────────────────────────────────────────────────────────
        private void SaveState()
        {
            var state = new AppState
            {
                BaseAddress  = txtBaseAddress.Text,
                DeltaFrom    = int.TryParse(txtDeltaFrom.Text, out int f) ? f : -64,
                DeltaTo      = int.TryParse(txtDeltaTo.Text,   out int t) ? t : 256,
                StrideIndex  = cmbStride.SelectedIndex
            };

            foreach (var item in _memoryList)
            {
                state.Items.Add(new SavedMemoryItem
                {
                    AddressHex     = item.AddressHex,
                    OffsetString   = item.OffsetString,
                    OffsetFromBase = item.OffsetFromBase,
                    DataType       = item.DataType.ToString(),
                    Label          = item.Label,
                    IsFrozen       = item.IsFrozen,
                    IsHidden       = item.IsHidden
                });
            }

            StateManager.Save(state);
        }

        private void LoadState()
        {
            var state = StateManager.Load();

            // Restore settings
            txtBaseAddress.Text     = state.BaseAddress;
            txtDeltaFrom.Text       = state.DeltaFrom.ToString();
            txtDeltaTo.Text         = state.DeltaTo.ToString();
            if (state.StrideIndex >= 0 && state.StrideIndex < cmbStride.Items.Count)
                cmbStride.SelectedIndex = state.StrideIndex;

            // Restore items
            _memoryList.Clear();
            foreach (var saved in state.Items)
            {
                if (!Enum.TryParse<MemoryDataType>(saved.DataType, out var dt))
                    dt = MemoryDataType.Int32;

                _memoryList.Add(new MemoryItem
                {
                    AddressHex     = saved.AddressHex,
                    OffsetString   = saved.OffsetString,
                    OffsetFromBase = saved.OffsetFromBase,
                    DataType       = dt,
                    Label          = saved.Label,
                    IsFrozen       = saved.IsFrozen,
                    IsHidden       = saved.IsHidden
                });
            }

            RefreshActiveTabVisibility();
        }
    }
}
