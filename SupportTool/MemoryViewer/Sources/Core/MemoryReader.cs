using System;
using System.Runtime.InteropServices;

namespace MemoryViewer.Sources.Core
{
    public class MemoryReader
    {
        private readonly ProcessManager _processManager;

        public MemoryReader(ProcessManager processManager)
        {
            _processManager = processManager;
        }

        public byte[] ReadBytes(IntPtr address, int length)
        {
            if (_processManager.ProcessHandle == IntPtr.Zero || length <= 0) return new byte[length];

            byte[] buffer = new byte[length];
            NativeMethods.ReadProcessMemory(_processManager.ProcessHandle, address, buffer, length, out _);
            return buffer;
        }

        public int ReadInt32(IntPtr address)
        {
            byte[] buffer = ReadBytes(address, 4);
            return BitConverter.ToInt32(buffer, 0);
        }

        public float ReadFloat(IntPtr address)
        {
            byte[] buffer = ReadBytes(address, 4);
            return BitConverter.ToSingle(buffer, 0);
        }

        public bool WriteBytes(IntPtr address, byte[] data)
        {
            if (_processManager.ProcessHandle == IntPtr.Zero) return false;
            return NativeMethods.WriteProcessMemory(_processManager.ProcessHandle, address, data, data.Length, out _);
        }

        public bool WriteInt32(IntPtr address, int value)
        {
            return WriteBytes(address, BitConverter.GetBytes(value));
        }

        public bool WriteFloat(IntPtr address, float value)
        {
            return WriteBytes(address, BitConverter.GetBytes(value));
        }

        public T Read<T>(IntPtr address) where T : unmanaged
        {
            int size = Marshal.SizeOf<T>();
            byte[] buffer = ReadBytes(address, size);
            if (buffer.Length < size) return default;
            return MemoryMarshal.Read<T>(buffer);
        }

        public bool Write<T>(IntPtr address, T value) where T : unmanaged
        {
            int size = Marshal.SizeOf<T>();
            byte[] buffer = new byte[size];
            MemoryMarshal.Write(buffer, ref value);
            return WriteBytes(address, buffer);
        }

        public IntPtr ResolvePointer(IntPtr baseAddress, int[] offsets)
        {
            IntPtr currentAddress = baseAddress;

            foreach (int offset in offsets)
            {
                byte[] ptrBytes = ReadBytes(currentAddress, IntPtr.Size);
                
                if (IntPtr.Size == 4) // 32-bit process
                {
                    currentAddress = (IntPtr)BitConverter.ToInt32(ptrBytes, 0);
                }
                else // 64-bit process
                {
                    currentAddress = (IntPtr)BitConverter.ToInt64(ptrBytes, 0);
                }

                if (currentAddress == IntPtr.Zero) break; // Invalid pointer chain
                
                currentAddress = IntPtr.Add(currentAddress, offset);
            }

            return currentAddress;
        }
    }
}
