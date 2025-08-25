using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Cxx11BindingsSharp
{
    // The main struct that represents the C11 stream interface.
    [StructLayout(LayoutKind.Sequential)]
    internal struct C11Stream
    {
        public IntPtr read;
        public IntPtr write;
        public IntPtr seek;
        public IntPtr flush;
        public IntPtr trunc;
    }

    // The actual callbacks that will be used by the native library. They must be public for re-use.
    public static class NativeLibrary
    {
        #region c11 stream

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int BufReadDelegate(IntPtr buffer, int size);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int BufWriteDelegate(IntPtr buffer, int size);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ReadDelegate([Out] byte[] buffer, int size);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int WriteDelegate([In] byte[] buffer, int size);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate long SeekDelegate(long off, int dir);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int FlushDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate long TruncDelegate(long length);

        #endregion
    }

    /// <summary>
    /// Main handle for the C11 streambuf struct. Can be used instead of a plain `IntPtr` in native p/invoke calls.
    /// Intended use is for c#/native author to access easily the underlying native code.
    /// </summary>
    public sealed class C11StreamBufHandle : SafeHandleMinusOneIsInvalid
    {
        private byte[] _buffer;

        public C11StreamBufHandle(NativeLibrary.ReadDelegate readFn, NativeLibrary.WriteDelegate writeFn,
            NativeLibrary.SeekDelegate seekFn, NativeLibrary.FlushDelegate flushFn,
            NativeLibrary.TruncDelegate truncFn, int buffering = 4096) : base(ownsHandle: true)
        {
            _buffer = new byte[buffering];
            // To avoid using `unsafe`, we add an extra layer of indirection:
            NativeLibrary.BufReadDelegate bufReadFn = BufRead;
            NativeLibrary.BufWriteDelegate bufWriteFn = BufWrite;
            var c11StreamHandle = new C11Stream
            {
                read = Marshal.GetFunctionPointerForDelegate(bufReadFn),
                write = Marshal.GetFunctionPointerForDelegate(bufWriteFn),
                seek = Marshal.GetFunctionPointerForDelegate(seekFn),
                flush = Marshal.GetFunctionPointerForDelegate(flushFn),
                trunc = Marshal.GetFunctionPointerForDelegate(truncFn)
            };
            // Initialize unmanaged memory to hold the struct.
            handle = Marshal.AllocHGlobal(Marshal.SizeOf(c11StreamHandle));
            Marshal.StructureToPtr(c11StreamHandle, handle, false);
            // keep external delegates alive:
            Read = readFn;
            Write = writeFn;
            Seek = seekFn;
            Flush = flushFn;
            Trunc = truncFn;
            // keep internal delegates alive:
            BufReadDelegate = bufReadFn;
            BufWriteDelegate = bufWriteFn;
        }

        private NativeLibrary.BufReadDelegate BufReadDelegate { get; set; }
        private NativeLibrary.BufWriteDelegate BufWriteDelegate { get; set; }

        private int BufRead(IntPtr outBuffer, int count)
        {
            Array.Resize(ref _buffer, count);
            var ret = Read(_buffer, count);
            Marshal.Copy(_buffer, 0, outBuffer, count);
            return ret;
        }

        private int BufWrite(IntPtr inBuffer, int count)
        {
            Array.Resize(ref _buffer, count);
            Marshal.Copy(inBuffer, _buffer, 0, count);
            var ret = Write(_buffer, count);
            return ret;
        }

        internal NativeLibrary.TruncDelegate Trunc { get; set; }

        internal NativeLibrary.FlushDelegate Flush { get; set; }

        internal NativeLibrary.SeekDelegate Seek { get; set; }

        internal NativeLibrary.WriteDelegate Write { get; set; }

        internal NativeLibrary.ReadDelegate Read { get; set; }

        protected override bool ReleaseHandle()
        {
            Marshal.FreeHGlobal(handle);
            return true;
        }
    }
}
