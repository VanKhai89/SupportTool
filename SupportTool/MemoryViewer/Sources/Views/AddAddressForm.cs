using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using MemoryViewer.Sources.Core;
using MemoryViewer.Sources.Models;
using MemoryViewer.Sources.Utils;

namespace MemoryViewer.Sources.Views
{
    /// <summary>
    /// Dialog "Add address" – clone giao diện Cheat Engine.
    ///  - Address (top): readonly khi Pointer mode, hiện final resolved addr.
    ///  - Pointer panel: N offset rows, mỗi row [&lt;] [TextBox] [&gt;] [preview].
    ///      [&lt;] / [&gt;] = giảm / tăng giá trị offset đi 1.
    ///  - Base address textbox: ở DƯỚI CÙNg panel, hỗ trợ "module.exe+0x1234".
    ///  - [Add Offset] / [Remove Offset] nằm ngoài panel.
    /// </summary>
    public class AddAddressForm : Form
    {
        // ── Static controls ──────────────────────────────────────────────────
        private TextBox  txtFinalAddr    = null!;  // address field (top)
        private Label    lblFinalPreview = null!;  // "= value" to the right
        private TextBox  txtDescription  = null!;
        private ComboBox cmbType         = null!;
        private CheckBox chkHex          = null!;
        private CheckBox chkSigned       = null!;
        private CheckBox chkPointer      = null!;

        // ── Pointer panel ─────────────────────────────────────────────────────
        private Panel   pnlPointer     = null!;   // outer bordered panel
        private Panel   pnlRows        = null!;   // inner – holds dynamic offset rows
        private TextBox txtBaseAddr    = null!;   // base address (bottom of panel)
        private Label   lblBasePreview = null!;   // "-> 0xADDR"

        // ── Buttons outside pointer panel ─────────────────────────────────────
        private Button btnAddOffset    = null!;
        private Button btnRemoveOffset = null!;
        private Button btnOk           = null!;
        private Button btnCancel       = null!;

        // ── Data ───────────────────────────────────────────────────────────────
        private readonly List<int>      _offsets = new();
        private readonly MemoryReader?  _reader;
        private readonly ProcessManager? _procMgr;
        private bool _updatingOffset;   // suppress re-entrant TextChanged

        // ── Output ────────────────────────────────────────────────────────────
        public IntPtr    ResolvedAddress { get; private set; }
        /// <summary>true nếu user chọn Pointer mode.</summary>
        public bool      IsPointer       { get; private set; }
        /// <summary>Biểu thức base address mà user nhập (module+offset hoặc hex).</summary>
        public string    BaseExpression  { get; private set; } = string.Empty;
        /// <summary>Danh sách offsets (IsPointer=true); empty nếu static.</summary>
        public List<int> OutputOffsets   { get; private set; } = new();

        // ── Constants ─────────────────────────────────────────────────────────
        private const int BASE_FORM_W = 640;
        private const int BASE_ROW_H  = 26;
        private const int BASE_BTN_W  = 22;
        private const int BASE_GAP    = 2;
        private const int BASE_PREVIEW_W = 240;

        private SizeF _currentScale = new SizeF(1f, 1f);

        protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
        {
            base.ScaleControl(factor, specified);
            _currentScale = new SizeF(_currentScale.Width * factor.Width, _currentScale.Height * factor.Height);
        }

        private int S(int value) => (int)(value * _currentScale.Width);

        private static readonly string[] TypeItems =
        { "Byte", "2 Bytes (Int16)", "4 Bytes (Int32)", "8 Bytes (Int64)", "Float", "Double" };

        private static readonly MemoryDataType[] TypeMap =
        {
            MemoryDataType.Byte,  MemoryDataType.Int16, MemoryDataType.Int32,
            MemoryDataType.Int64, MemoryDataType.Float, MemoryDataType.Double,
        };

        // ─────────────────────────────────────────────────────────────────────
        public AddAddressForm(MemoryReader? reader = null, ProcessManager? processManager = null,
                              BaseAddressConfig? existingConfig = null)
        {
            _reader  = reader;
            _procMgr = processManager;
            InitializeComponent();

            if (existingConfig != null)
            {
                // Pre-populate từ existing config
                // 1. Setup offsets trước (trước khi RebuildRows)
                if (existingConfig.IsPointer)
                {
                    _offsets.Clear();
                    if (existingConfig.Offsets.Length > 0)
                        _offsets.AddRange(existingConfig.Offsets);
                    else
                        _offsets.Add(0);
                }
                else
                {
                    _offsets.Add(0);
                }
            }
            else
            {
                _offsets.Add(0);   // default: 1 offset row
            }

            RebuildRows();

            // 2. Set text sau RebuildRows (controls đã sẵn sàng), trước CheckedChanged
            if (existingConfig != null)
            {
                if (existingConfig.IsPointer)
                    txtBaseAddr.Text = existingConfig.Expression;
                else
                    txtFinalAddr.Text = existingConfig.Expression;

                // 3. Cuối cùng set checkbox → kích hoạt CheckedChanged → UpdatePreview()
                chkPointer.Checked = existingConfig.IsPointer;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  UI Setup
        // ─────────────────────────────────────────────────────────────────────
        private void InitializeComponent()
        {
            Text             = "Add address";
            FormBorderStyle  = FormBorderStyle.FixedDialog;
            MaximizeBox      = false;
            MinimizeBox      = false;
            StartPosition    = FormStartPosition.CenterParent;
            ClientSize       = new Size(BASE_FORM_W, 300);

            int y = 10;

            // ── Address (top) ──────────────────────────────────────────────
            var lblAddr = new Label { Text = "Address:", Location = new Point(10, y), AutoSize = true };
            y += 18;
            txtFinalAddr = new TextBox
            {
                Location = new Point(10, y),
                Width    = 280,
                Font     = new Font("Consolas", 9)
            };
            lblFinalPreview = new Label
            {
                Text      = "= ???",
                Location  = new Point(296, y + 3),
                Width     = BASE_FORM_W - 300,
                ForeColor = Color.DarkBlue,
                Font      = new Font("Consolas", 9)
            };
            y += 26;

            // ── Description ────────────────────────────────────────────────
            var lblDesc = new Label { Text = "Description", Location = new Point(10, y), AutoSize = true };
            y += 18;
            txtDescription = new TextBox
            {
                Text     = "No description",
                Location = new Point(10, y),
                Width    = BASE_FORM_W - 20
            };
            y += 26;

            // ── Type ───────────────────────────────────────────────────────
            var lblType = new Label { Text = "Type", Location = new Point(10, y), AutoSize = true };
            y += 18;
            cmbType = new ComboBox
            {
                Location      = new Point(10, y),
                Width         = 170,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbType.Items.AddRange(TypeItems);
            cmbType.SelectedIndex = 2;
            y += 26;

            // ── Hexadecimal / Signed ───────────────────────────────────────
            chkHex    = new CheckBox { Text = "Hexadecimal", Location = new Point(10,  y), Width = 110 };
            chkSigned = new CheckBox { Text = "Signed",      Location = new Point(125, y), Width = 70  };
            y += 26;

            // ── Pointer checkbox ───────────────────────────────────────────
            chkPointer = new CheckBox
            {
                Text     = "Pointer",
                Location = new Point(10, y),
                Width    = 90
            };
            y += 26;

            // ── Pointer panel ──────────────────────────────────────────────
            pnlPointer = new Panel
            {
                Location    = new Point(10, y),
                Width       = BASE_FORM_W - 20,
                Height      = 60,
                BorderStyle = BorderStyle.FixedSingle,
                Visible     = false
            };

            pnlRows = new Panel
            {
                Location = new Point(0, 0),
                Width    = pnlPointer.Width - 2,
                Height   = 0
            };

            // Base address row (repositioned by RebuildRows)
            txtBaseAddr = new TextBox
            {
                Location        = new Point(2, 2),
                Width           = BASE_FORM_W - 200,
                Font            = new Font("Consolas", 9),
                PlaceholderText = "game.exe+0x1234  or  0x12345678"
            };
            lblBasePreview = new Label
            {
                Location  = new Point(txtBaseAddr.Right + 6, 4),
                Width     = BASE_FORM_W - txtBaseAddr.Right - 10,
                ForeColor = Color.DarkBlue,
                Font      = new Font("Consolas", 9),
                Text      = "-> ???",
                TextAlign = ContentAlignment.MiddleLeft
            };

            pnlPointer.Controls.Add(pnlRows);
            pnlPointer.Controls.Add(txtBaseAddr);
            pnlPointer.Controls.Add(lblBasePreview);

            // ── Add / Remove Offset (outside panel) ───────────────────────
            btnAddOffset = new Button
            {
                Text    = "Add Offset",
                Width   = 100, Height = 26,
                Visible = false
            };
            btnRemoveOffset = new Button
            {
                Text    = "Remove Offset",
                Width   = 115, Height = 26,
                Visible = false
            };

            // ── OK / Cancel ────────────────────────────────────────────────
            btnOk     = new Button { Text = "OK",     Width = 85, Height = 26 };
            btnCancel = new Button { Text = "Cancel",  Width = 85, Height = 26, DialogResult = DialogResult.Cancel };
            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Controls.AddRange(new Control[] {
                lblAddr, txtFinalAddr, lblFinalPreview,
                lblDesc, txtDescription,
                lblType, cmbType,
                chkHex, chkSigned,
                chkPointer, pnlPointer,
                btnAddOffset, btnRemoveOffset,
                btnOk, btnCancel
            });

            // ── Events ────────────────────────────────────────────────────
            chkPointer.CheckedChanged += (s, e) =>
            {
                bool on = chkPointer.Checked;
                pnlPointer.Visible        = on;
                btnAddOffset.Visible      = on;
                btnRemoveOffset.Visible   = on;
                txtFinalAddr.ReadOnly     = on;
                txtFinalAddr.BackColor    = on ? SystemColors.Control : SystemColors.Window;
                UpdateLayout();
                UpdatePreview();
            };

            txtFinalAddr.TextChanged     += (s, e) => { if (!chkPointer.Checked) UpdatePreview(); };
            txtBaseAddr.TextChanged      += (s, e) => UpdatePreview();
            cmbType.SelectedIndexChanged += (s, e) => UpdatePreview();
            chkHex.CheckedChanged        += (s, e) => UpdatePreview();
            chkSigned.CheckedChanged     += (s, e) => UpdatePreview();

            btnAddOffset.Click += (s, e) =>
            {
                _offsets.Add(0);
                RebuildRows();
                UpdatePreview();
            };
            btnRemoveOffset.Click += (s, e) =>
            {
                if (_offsets.Count > 1)
                {
                    _offsets.RemoveAt(_offsets.Count - 1);
                    RebuildRows();
                    UpdatePreview();
                }
            };
            btnOk.Click += BtnOk_Click;

            UpdateLayout();

            // Enable High DPI AutoScaling
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Rebuild dynamic offset rows
        // ─────────────────────────────────────────────────────────────────────
        private void RebuildRows()
        {
            pnlRows.Controls.Clear();

            int rowW = pnlRows.Width;
            int y    = 0;

            for (int i = 0; i < _offsets.Count; i++)
            {
                int  ci     = i;
                bool isLast = (i == _offsets.Count - 1) && (_offsets.Count > 1);

                var row = new Panel
                {
                    Location  = new Point(0, y),
                    Width     = rowW,
                    Height    = S(BASE_ROW_H),
                    BackColor = (i % 2 == 0) ? Color.White : Color.FromArgb(246, 249, 255)
                };

                int rx = S(1);

                // ── [<] Decrement button ──────────────────────────────────
                var btnDec = new Button
                {
                    Text      = "<",
                    Location  = new Point(rx, S(2)),
                    Width     = S(BASE_BTN_W), Height = S(BASE_ROW_H) - S(4),
                    Font      = new Font("Consolas", 8),
                    FlatStyle = FlatStyle.Flat
                };
                btnDec.FlatAppearance.BorderColor = Color.Silver;
                btnDec.Click += (s, e) =>
                {
                    _offsets[ci]--;
                    SetOffsetBox(ci, _offsets[ci]);
                    UpdatePreview();
                };
                rx += S(BASE_BTN_W) + S(BASE_GAP);

                // ── "Offset N" prefix label for last row ──────────────────
                if (isLast)
                {
                    var pfx = new Label
                    {
                        Text      = $"Offset {_offsets.Count}",
                        Location  = new Point(rx, S(4)),
                        Width     = S(58),
                        Height    = S(BASE_ROW_H) - S(6),
                        ForeColor = Color.Gray,
                        Font      = new Font(Font, FontStyle.Italic),
                        TextAlign = ContentAlignment.MiddleLeft
                    };
                    row.Controls.Add(pfx);
                    rx += S(60);
                }

                // ── Offset TextBox ────────────────────────────────────────
                //  Remaining width: rowW - rx - GAP - BTN_W(>) - GAP - previewW - margin
                int txtW = rowW - rx - S(BASE_GAP) - S(BASE_BTN_W) - S(BASE_GAP) - S(BASE_PREVIEW_W) - S(4);
                if (txtW < S(80)) txtW = S(80);

                var txtOff = new TextBox
                {
                    Location  = new Point(rx, S(3)),
                    Width     = txtW,
                    Text      = FormatOffset(_offsets[i]),
                    Font      = new Font("Consolas", 9),
                    TextAlign = HorizontalAlignment.Center,
                    Tag       = $"off_{ci}"
                };
                txtOff.TextChanged += (s, e) =>
                {
                    if (_updatingOffset) return;
                    string raw = txtOff.Text;
                    if (HexConverter.TryParseOffset(raw, out int v) || TryParseNegativeHex(raw, out v))
                        _offsets[ci] = v;
                    UpdatePreview();
                };
                rx += txtW + S(BASE_GAP);

                // ── [>] Increment button ──────────────────────────────────
                var btnInc = new Button
                {
                    Text      = ">",
                    Location  = new Point(rx, S(2)),
                    Width     = S(BASE_BTN_W), Height = S(BASE_ROW_H) - S(4),
                    Font      = new Font("Consolas", 8),
                    FlatStyle = FlatStyle.Flat
                };
                btnInc.FlatAppearance.BorderColor = Color.Silver;
                btnInc.Click += (s, e) =>
                {
                    _offsets[ci]++;
                    SetOffsetBox(ci, _offsets[ci]);
                    UpdatePreview();
                };
                rx += S(BASE_BTN_W) + S(BASE_GAP);

                // ── Step preview label (blue, link-cursor) ────────────────
                var lblStep = new Label
                {
                    Location  = new Point(rx, S(4)),
                    Width     = rowW - rx - S(2),
                    Height    = S(BASE_ROW_H) - S(6),
                    ForeColor = Color.Blue,
                    Font      = new Font("Consolas", 8),
                    Text      = "...",
                    Tag       = "preview",
                    TextAlign = ContentAlignment.MiddleLeft,
                    Cursor    = Cursors.Hand
                };

                row.Controls.AddRange(new Control[] { btnDec, txtOff, btnInc, lblStep });
                pnlRows.Controls.Add(row);
                y += S(BASE_ROW_H);
            }

            pnlRows.Height = y;

            // Reposition base address row below offset rows
            int baseRowY = y + S(1);
            int baseTxtW = S(BASE_FORM_W - 200);   // same proportion as creation
            txtBaseAddr.Location    = new Point(S(2), baseRowY + S(1));
            txtBaseAddr.Width       = baseTxtW;
            lblBasePreview.Location = new Point(txtBaseAddr.Right + S(4), baseRowY + S(2));
            lblBasePreview.Width    = pnlPointer.Width - txtBaseAddr.Right - S(6);
            lblBasePreview.Height   = S(BASE_ROW_H);
            pnlPointer.Height       = baseRowY + S(BASE_ROW_H) + S(6);

            UpdateLayout();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Layout – repositions OK/Cancel, resizes form
        // ─────────────────────────────────────────────────────────────────────
        private void UpdateLayout()
        {
            if (pnlPointer == null || btnAddOffset == null || btnOk == null || btnCancel == null) return;

            int afterPanel = chkPointer.Checked
                ? pnlPointer.Bottom + S(6)
                : pnlPointer.Top;

            btnAddOffset.Location    = new Point(S(10),  afterPanel + S(2));
            btnRemoveOffset.Location = new Point(S(118), afterPanel + S(2));

            int okY = chkPointer.Checked ? afterPanel + S(38) : afterPanel + S(10);

            int formW = ClientSize.Width;
            if (formW == 0) formW = BASE_FORM_W; // fallback if called early

            btnOk.Location     = new Point(formW - S(194), okY);
            btnCancel.Location = new Point(formW - S(102), okY);
            ClientSize         = new Size(formW, okY + S(42));
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Real-time preview
        // ─────────────────────────────────────────────────────────────────────
        private void UpdatePreview()
        {
            // ── Direct address mode ──────────────────────────────────────
            if (!chkPointer.Checked)
            {
                if (_reader != null && HexConverter.TryParseHexOrDec(txtFinalAddr.Text, out IntPtr a))
                    lblFinalPreview.Text = $"= {ReadValueAt(a)}";
                else
                    lblFinalPreview.Text = "= ???";
                return;
            }

            // ── Pointer mode ─────────────────────────────────────────────
            //  Cheat Engine pointer logic:
            //    base_     = static address (what user typed, e.g. 0x00964CDC)
            //    basePtrVal = *base_  (value read FROM that address)
            //    offset row 0: basePtrVal + offsets[0]  -> addr0
            //    offset row 1: *addr0  + offsets[1]     -> addr1
            //    ...
            //    final address = last computed addr
            //    final value   = read value at final address

            IntPtr base_ = ResolveBaseAddr();

            // Show the resolved static address in the base field preview only
            // (no memory read here – that's done per-step below)
            if (base_ == IntPtr.Zero || _reader == null)
            {
                lblBasePreview.Text = "-> ???";
                SetStepPreviews(null, base_);
                txtFinalAddr.Text    = "???";
                lblFinalPreview.Text = "= ???";
                return;
            }

            // Step 0: read pointer from base_
            int ptrSize = _procMgr?.TargetPtrSize ?? IntPtr.Size;
            IntPtr basePtrVal = IntPtr.Zero;
            bool baseReadOk = false;
            try
            {
                byte[] bytes = _reader.ReadBytes(base_, ptrSize);
                long raw = ptrSize == 4 ? BitConverter.ToInt32(bytes, 0) : BitConverter.ToInt64(bytes, 0);
                basePtrVal = (IntPtr)(uint)raw;   // mask to 32-bit if needed
                if (ptrSize == 8) basePtrVal = (IntPtr)raw;
                baseReadOk = true;
            }
            catch { }

            // lblBasePreview shows the pointer value at base_
            lblBasePreview.Text = baseReadOk
                ? $"-> {HexConverter.ToHexString(basePtrVal)}"
                : "-> ???";

            if (!baseReadOk)
            {
                SetStepPreviews(null, base_);
                txtFinalAddr.Text    = "???";
                lblFinalPreview.Text = "= ???";
                return;
            }

            // Walk the offset chain.
            // Each step: take current ptr value, add offset → new address.
            // For next step: dereference that new address to get next ptr value.
            var steps = new List<(IntPtr addrIn, int offset, IntPtr addrOut)>();
            IntPtr cur = basePtrVal;   // start from the dereferenced base
            bool allOk = true;

            for (int i = 0; i < _offsets.Count; i++)
            {
                IntPtr addrOut = IntPtr.Add(cur, _offsets[i]);
                steps.Add((cur, _offsets[i], addrOut));

                // For next iteration: dereference addrOut to get next pointer
                if (i < _offsets.Count - 1)
                {
                    try
                    {
                        byte[] b = _reader.ReadBytes(addrOut, ptrSize);
                        long r = ptrSize == 4 ? BitConverter.ToInt32(b, 0) : BitConverter.ToInt64(b, 0);
                        cur = ptrSize == 4 ? (IntPtr)(uint)r : (IntPtr)r;
                    }
                    catch { allOk = false; break; }
                }
                else
                {
                    cur = addrOut; // last step: final address
                }
            }

            SetStepPreviews(allOk ? steps : null, base_);

            if (allOk && steps.Count > 0)
            {
                IntPtr final = steps[^1].addrOut;
                txtFinalAddr.Text    = HexConverter.ToHexString(final);
                lblFinalPreview.Text = $"= {ReadValueAt(final)}";
            }
            else
            {
                txtFinalAddr.Text    = "???";
                lblFinalPreview.Text = "= ???";
            }
        }

        private void SetStepPreviews(List<(IntPtr addrIn, int offset, IntPtr addrOut)>? steps, IntPtr baseAddr)
        {
            int idx = 0;
            foreach (Control row in pnlRows.Controls)
            {
                foreach (Control c in row.Controls)
                {
                    if (c is Label lbl && lbl.Tag?.ToString() == "preview")
                    {
                        if (idx >= _offsets.Count) break;

                        if (steps == null || idx >= steps.Count)
                        {
                            lbl.Text = "????????";
                        }
                        else
                        {
                            var (addrIn, offset, addrOut) = steps[idx];
                            string inH  = HexConverter.ToHexString(addrIn);
                            string outH = HexConverter.ToHexString(addrOut);
                            string offStr = offset >= 0
                                ? $"+0x{offset:X}"
                                : $"-0x{-offset:X}";
                            lbl.Text = $"{inH} {offStr} = {outH}";
                        }
                        break;
                    }
                }
                idx++;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Base address resolver – supports "module.exe+0x1234"
        // ─────────────────────────────────────────────────────────────────────
        private IntPtr ResolveBaseAddr()
        {
            string input = txtBaseAddr.Text.Trim().Trim('[', ']');
            if (string.IsNullOrWhiteSpace(input)) return IntPtr.Zero;

            // Check for module+offset pattern
            int plusIdx = input.LastIndexOf('+');
            if (plusIdx > 0)
            {
                string modName    = input[..plusIdx].Trim('"', '\'', ' ');
                string offsetPart = input[(plusIdx + 1)..].Trim();

                if (!HexConverter.TryParseOffset(offsetPart, out int modOffset) &&
                    !TryParseNegativeHex(offsetPart, out modOffset))
                    modOffset = 0;

                var proc = _procMgr?.SelectedProcess;
                if (proc != null)
                {
                    try
                    {
                        foreach (ProcessModule m in proc.Modules)
                        {
                            if (m.ModuleName.Equals(modName, StringComparison.OrdinalIgnoreCase))
                                return IntPtr.Add(m.BaseAddress, modOffset);
                        }
                    }
                    catch { /* process may have exited, or access denied */ }
                }
            }

            // Fallback: raw hex/dec address
            HexConverter.TryParseHexOrDec(input, out IntPtr addr);
            return addr;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Read value for preview display
        // ─────────────────────────────────────────────────────────────────────
        private string ReadValueAt(IntPtr addr)
        {
            if (_reader == null || addr == IntPtr.Zero) return "???";
            try
            {
                MemoryDataType dt = TypeMap[cmbType.SelectedIndex];
                int n = dt switch
                {
                    MemoryDataType.Byte   => 1,
                    MemoryDataType.Int16  => 2,
                    MemoryDataType.Int32  => 4,
                    MemoryDataType.Int64  => 8,
                    MemoryDataType.Float  => 4,
                    MemoryDataType.Double => 8,
                    _                     => 4
                };
                byte[] b      = _reader.ReadBytes(addr, n);
                bool   hex    = chkHex.Checked;
                bool   signed = chkSigned.Checked;

                return dt switch
                {
                    MemoryDataType.Byte   => hex ? $"0x{b[0]:X}"
                                                 : (signed ? ((sbyte)b[0]).ToString() : b[0].ToString()),
                    MemoryDataType.Int16  => hex ? $"0x{BitConverter.ToUInt16(b, 0):X}"
                                                 : (signed ? BitConverter.ToInt16(b, 0).ToString()
                                                           : BitConverter.ToUInt16(b, 0).ToString()),
                    MemoryDataType.Int32  => hex ? $"0x{BitConverter.ToUInt32(b, 0):X}"
                                                 : (signed ? BitConverter.ToInt32(b, 0).ToString()
                                                           : BitConverter.ToUInt32(b, 0).ToString()),
                    MemoryDataType.Int64  => hex ? $"0x{BitConverter.ToUInt64(b, 0):X}"
                                                 : (signed ? BitConverter.ToInt64(b, 0).ToString()
                                                           : BitConverter.ToUInt64(b, 0).ToString()),
                    MemoryDataType.Float  => BitConverter.ToSingle(b, 0).ToString("0.####"),
                    MemoryDataType.Double => BitConverter.ToDouble(b, 0).ToString("0.########"),
                    _                     => "???"
                };
            }
            catch { return "???"; }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  OK handler
        // ─────────────────────────────────────────────────────────────────────
        private void BtnOk_Click(object? sender, EventArgs e)
        {
            if (!chkPointer.Checked)
            {
                if (!HexConverter.TryParseHexOrDec(txtFinalAddr.Text, out IntPtr direct))
                {
                    MessageBox.Show("Địa chỉ không hợp lệ.\nNhập hex (0x00401000) hoặc decimal.", "Error");
                    return;
                }
                // Static address
                ResolvedAddress = direct;
                IsPointer       = false;
                BaseExpression  = txtFinalAddr.Text.Trim();
                OutputOffsets   = new List<int>();
            }
            else
            {
                IntPtr base_ = ResolveBaseAddr();
                if (base_ == IntPtr.Zero)
                {
                    MessageBox.Show(
                        "Base address không hợp lệ hoặc không tìm thấy module.\n" +
                        "Ví dụ hợp lệ: game.exe+0x1234  hoặc  0x12345678",
                        "Error");
                    return;
                }
                if (_reader == null)
                {
                    MessageBox.Show("Cần attach process trước để resolve pointer chain.", "Error");
                    return;
                }
                try
                {
                    IntPtr resolved = _reader.ResolvePointer(base_, _offsets.ToArray());
                    if (resolved == IntPtr.Zero)
                    {
                        MessageBox.Show("Pointer chain trả về null (địa chỉ không hợp lệ).", "Null Pointer");
                        return;
                    }
                    // Pointer chain
                    ResolvedAddress = resolved;
                    IsPointer       = true;
                    BaseExpression  = txtBaseAddr.Text.Trim();
                    OutputOffsets   = new List<int>(_offsets);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi resolve pointer: {ex.Message}", "Error");
                    return;
                }
            }
            DialogResult = DialogResult.OK;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Set a row's TextBox value without triggering TextChanged.</summary>
        private void SetOffsetBox(int rowIndex, int value)
        {
            if (rowIndex >= pnlRows.Controls.Count) return;
            _updatingOffset = true;
            try
            {
                var row = pnlRows.Controls[rowIndex];
                foreach (Control c in row.Controls)
                {
                    if (c is TextBox txt && txt.Tag?.ToString().StartsWith("off_") == true)
                    {
                        txt.Text = FormatOffset(value);
                        break;
                    }
                }
            }
            finally { _updatingOffset = false; }
        }

        private static string FormatOffset(int v)
            => v >= 0 ? $"0x{v:X}" : $"-0x{-v:X}";

        private static bool TryParseNegativeHex(string input, out int result)
        {
            result = 0;
            input  = input.Trim().ToLower();
            if (!input.StartsWith("-0x")) return false;
            if (int.TryParse(input[3..], NumberStyles.HexNumber, null, out int abs))
            { result = -abs; return true; }
            return false;
        }
    }
}
