using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using MemoryViewer.Sources.Core;

namespace MemoryViewer.Sources.Views
{
    public class SelectProcessForm : Form
    {
        private ListBox lstProcesses;
        private Button btnSelect;
        private Button btnRefresh;
        private ProcessManager _processManager;

        public Process SelectedProcess { get; private set; }

        public SelectProcessForm(ProcessManager processManager)
        {
            _processManager = processManager;
            InitializeComponent();
            LoadProcesses();
        }

        private void InitializeComponent()
        {
            this.Text = "Select Process";
            this.Size = new Size(400, 500);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            lstProcesses = new ListBox { Location = new Point(10, 10), Size = new Size(360, 380) };
            
            btnRefresh = new Button { Text = "Refresh", Location = new Point(10, 400), Width = 100 };
            btnSelect = new Button { Text = "Attach", Location = new Point(270, 400), Width = 100 };

            this.Controls.AddRange(new Control[] { lstProcesses, btnRefresh, btnSelect });

            // Enable High DPI AutoScaling
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;

            btnRefresh.Click += (s, e) => LoadProcesses();
            btnSelect.Click += (s, e) => {
                if (lstProcesses.SelectedItem is ProcessItem item)
                {
                    SelectedProcess = item.Process;
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
            };
            lstProcesses.DoubleClick += (s, e) => btnSelect.PerformClick();
        }

        private void LoadProcesses()
        {
            lstProcesses.Items.Clear();
            var list = _processManager.GetProcessList();
            foreach (var p in list)
            {
                lstProcesses.Items.Add(new ProcessItem(p));
            }
        }

        private class ProcessItem
        {
            public Process Process { get; }
            public ProcessItem(Process process) { Process = process; }
            public override string ToString() => $"{Process.Id.ToString().PadRight(6)} - {Process.ProcessName} ({Process.MainWindowTitle})";
        }
    }
}
