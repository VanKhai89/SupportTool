using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace MemoryViewer.Sources.Core
{
    public class ProcessManager
    {
        public IntPtr ProcessHandle { get; private set; } = IntPtr.Zero;
        public Process? SelectedProcess { get; private set; }

        /// <summary>True when the attached target is a 32-bit process.</summary>
        public bool IsTarget32Bit { get; private set; } = true;

        /// <summary>Pointer size in bytes for the attached target (4 or 8).</summary>
        public int TargetPtrSize => IsTarget32Bit ? 4 : 8;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool IsWow64Process(IntPtr hProcess, out bool wow64Process);

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

                // Detect if the target is a 32-bit process running under WOW64
                if (Environment.Is64BitOperatingSystem)
                {
                    IsWow64Process(handle, out bool isWow64);
                    // isWow64 == true  → 32-bit process on 64-bit OS
                    // isWow64 == false + tool is 64-bit → native 64-bit process
                    IsTarget32Bit = isWow64;
                }
                else
                {
                    // 32-bit OS → all processes are 32-bit
                    IsTarget32Bit = true;
                }

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
            IsTarget32Bit = true;
        }
    }
}
