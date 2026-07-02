using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MemoryViewer.Sources.Core
{
    public class ProcessManager
    {
        public IntPtr ProcessHandle { get; private set; } = IntPtr.Zero;
        public Process? SelectedProcess { get; private set; }

        public IEnumerable<Process> GetProcessList()
        {
            // Only list processes with a window to filter out system services
            return Process.GetProcesses()
                .Where(p => p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(p.MainWindowTitle))
                .OrderBy(p => p.ProcessName);
        }

        public bool AttachProcess(Process process)
        {
            CloseProcess();

            if (process == null) return false;

            IntPtr handle = NativeMethods.OpenProcess(
                NativeMethods.PROCESS_WM_READ | NativeMethods.PROCESS_VM_WRITE | NativeMethods.PROCESS_VM_OPERATION,
                false,
                process.Id);

            if (handle != IntPtr.Zero)
            {
                ProcessHandle = handle;
                SelectedProcess = process;
                return true;
            }

            return false;
        }

        public void CloseProcess()
        {
            if (ProcessHandle != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(ProcessHandle);
                ProcessHandle = IntPtr.Zero;
            }
            SelectedProcess = null;
        }
    }
}
