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
        public IntPtr ResolvedAddress { get; private set; }

        // ── Constants ─────────────────────────────────────────────────────────
        private const int FORM_W  = 640;
        private const int ROW_H   = 26;
        private const int BTN_W   = 22;
        private const int GAP     = 2;
        private int _pnlTop;            // Y of pnlPointer, set in InitializeComponent

        private static readonly string[] TypeItems =
        { "Byte", "2 Bytes (Int16)", "4 Bytes (Int32)", "8 Bytes (Int64)", "Float", "Double" };

        private static readonly MemoryDataType[] TypeMap =
        {
            MemoryDataType.Byte,  MemoryDataType.Int16, MemoryDataType.Int32,
            MemoryDataType.Int64, MemoryDataType.Float, MemoryDataType.Double,
        };

        // ─────────────────────────────────────────────────────────────────────
        public AddAddressForm(MemoryReader? reader = null, ProcessManager? processManager = null)
        {
            _reader  = reader;
            _procMgr = processManager;
            InitializeComponent();
            _offsets.Add(0);   // default: 1 offset row
            RebuildRows();
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
            ClientSize       = new Size(FORM_W, 300);

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
                Width     = FORM_W - 300,
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
                Width    = FORM_W - 20
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
            _pnlTop    = y;
            pnlPointer = new Panel
            {
                Location    = new Point(10, y),
                Width       = FORM_W - 20,
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
                Location         = new Point(2, 2),
                Width            = pnlPointer.Width - 130,
                Font             = new Font("Consolas", 9),
                PlaceholderText  = "game.exe+0x1234  or  0x12345678"
            };
            lblBasePreview = new Label
            {
                Location  = new Point(pnlPointer.Width - 125, 4),
                Width     = 120,
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
                    Height    = ROW_H,
                    BackColor = (i % 2 == 0) ? Color.White : Color.FromArgb(246, 249, 255)
                };

                int rx = 1;

                // ── [<] Decrement button ──────────────────────────────────
                var btnDec = new Button
                {
                    Text      = "<",
                    Location  = new Point(rx, 2),
                    Width     = BTN_W, Height = ROW_H - 4,
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
                rx += BTN_W + GAP;

                // ── "Offset N" prefix label for last row ──────────────────
                if (isLast)
                {
                    var pfx = new Label
                    {
                        Text      = $"Offset {_offsets.Count}",
                        Location  = new Point(rx, 4),
                        Width     = 58,
                        Height    = ROW_H - 6,
                        ForeColor = Color.Gray,
                        Font      = new Font(Font, FontStyle.Italic),
                        TextAlign = ContentAlignment.MiddleLeft
                    };
                    row.Controls.Add(pfx);
                    rx += 60;
                }

                // ── Offset TextBox ────────────────────────────────────────
                //  Remaining width: rowW - rx - GAP - BTN_W(>) - GAP - previewW - margin
                const int previewW = 240;
                int txtW = rowW - rx - GAP - BTN_W - GAP - previewW - 4;
                if (txtW < 80) txtW = 80;

                var txtOff = new TextBox
                {
                    Location  = new Point(rx, 3),
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
                rx += txtW + GAP;

                // ── [>] Increment button ──────────────────────────────────
                var btnInc = new Button
                {
                    Text      = ">",
                    Location  = new Point(rx, 2),
                    Width     = BTN_W, Height = ROW_H - 4,
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
                rx += BTN_W + GAP;

                // ── Step preview label (blue, link-cursor) ────────────────
                var lblStep = new Label
                {
                    Location  = new Point(rx, 4),
                    Width     = rowW - rx - 2,
                    Height    = ROW_H - 6,
                    ForeColor = Color.Blue,
                    Font      = new Font("Consolas", 8),
                    Text      = "...",
                    Tag       = "preview",
                    TextAlign = ContentAlignment.MiddleLeft,
                    Cursor    = Cursors.Hand
                };

                row.Controls.AddRange(new Control[] { btnDec, txtOff, btnInc, lblStep });
                pnlRows.Controls.Add(row);
                y += ROW_H;
            }

            pnlRows.Height = y;

            // Reposition base address row below offset rows
            int baseRowY = y + 1;
            txtBaseAddr.Location    = new Point(2, baseRowY + 1);
            lblBasePreview.Location = new Point(txtBaseAddr.Right + 4, baseRowY + 4);
            pnlPointer.Height       = baseRowY + ROW_H + 6;

            UpdateLayout();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Layout – repositions OK/Cancel, resizes form
        // ─────────────────────────────────────────────────────────────────────
        private void UpdateLayout()
        {
            int afterPanel = chkPointer.Checked
                ? _pnlTop + pnlPointer.Height + 6
                : _pnlTop;

            btnAddOffset.Location    = new Point(10,  afterPanel + 2);
            btnRemoveOffset.Location = new Point(118, afterPanel + 2);

            int okY = chkPointer.Checked ? afterPanel + 38 : afterPanel + 10;

            btnOk.Location     = new Point(FORM_W - 194, okY);
            btnCancel.Location = new Point(FORM_W - 102, okY);
            ClientSize         = new Size(FORM_W, okY + 42);
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
            IntPtr base_ = ResolveBaseAddr();
            lblBasePreview.Text = base_ != IntPtr.Zero
                ? $"-> {HexConverter.ToHexString(base_)}"
                : "-> ???";

            if (base_ == IntPtr.Zero || _reader == null)
            {
                SetStepPreviews(null, base_);
                txtFinalAddr.Text    = "???";
                lblFinalPreview.Text = "= ???";
                return;
            }

            // Walk pointer chain, capture each intermediate step
            var steps = new List<(IntPtr from, IntPtr to)>();
            IntPtr cur    = base_;
            bool   allOk  = true;

            for (int i = 0; i < _offsets.Count; i++)
            {
                IntPtr from = cur;
                try
                {
                    byte[] bytes = _reader.ReadBytes(cur, IntPtr.Size);
                    long   raw   = IntPtr.Size == 8
                        ? BitConverter.ToInt64(bytes, 0)
                        : BitConverter.ToInt32(bytes, 0);
                    cur = (IntPtr)raw;
                    if (cur == IntPtr.Zero) { allOk = false; break; }
                    cur = IntPtr.Add(cur, _offsets[i]);
                    steps.Add((from, cur));
                }
                catch { allOk = false; break; }
            }

            SetStepPreviews(allOk ? steps : null, base_);

            if (allOk && steps.Count > 0)
            {
                IntPtr final = steps[^1].to;
                txtFinalAddr.Text    = HexConverter.ToHexString(final);
                lblFinalPreview.Text = $"= {ReadValueAt(final)}";
            }
            else
            {
                txtFinalAddr.Text    = "???";
                lblFinalPreview.Text = "= ???";
            }
        }

        /// <summary>Update the step preview label of every offset row.</summary>
        private void SetStepPreviews(List<(IntPtr from, IntPtr to)>? steps, IntPtr baseAddr)
        {
            int idx = 0;
            foreach (Control row in pnlRows.Controls)
            {
                foreach (Control c in row.Controls)
                {
                    if (c is Label lbl && lbl.Tag?.ToString() == "preview")
                    {
                        if (idx >= _offsets.Count) break;
                        string offStr  = FormatOffset(_offsets[idx]);
                        bool   isFirst = (idx == 0);

                        if (steps == null || idx >= steps.Count)
                        {
                            string bh = baseAddr != IntPtr.Zero
                                ? HexConverter.ToHexString(baseAddr)
                                : "????????";
                            lbl.Text = isFirst
                                ? $"{bh}+{offStr} = ????????"
                                : $"[????????]+{offStr} -> ????????";
                        }
                        else
                        {
                            var (from, to) = steps[idx];
                            string fromH = HexConverter.ToHexString(from);
                            string toH   = HexConverter.ToHexString(to);
                            lbl.Text = isFirst
                                ? $"{fromH}+{offStr} = {toH}"
                                : $"[{fromH}]+{offStr} -> {toH}";
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
                ResolvedAddress = direct;
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
                    ResolvedAddress = resolved;
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
