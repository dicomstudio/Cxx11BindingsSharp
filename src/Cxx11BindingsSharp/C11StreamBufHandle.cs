using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Cxx11BindingsSharp
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct ManagedBuffer
    {
        public IntPtr buf_read;
        public IntPtr buf_write;
        public IntPtr data;
        public UIntPtr size;
    }

    // The main struct that represents the C11 streambuf interface.
    [StructLayout(LayoutKind.Sequential)]
    internal struct C11StreamBuf
    {
        [MarshalAs(UnmanagedType.Struct)] public ManagedBuffer managedBuffer;
        public IntPtr seek;
        public IntPtr flush;
        public IntPtr trunc;
    }

    // The actual callbacks that will be used by the native library. They must be public for re-use.
    public static class NativeLibrary
    {
        #region c11 stream

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int BufReadDelegate(int size);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int BufWriteDelegate(int size);

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
        private readonly byte[] _buffer;
        private GCHandle _bufferHandle;

        public C11StreamBufHandle(NativeLibrary.ReadDelegate readFn, NativeLibrary.WriteDelegate writeFn,
            NativeLibrary.SeekDelegate seekFn, NativeLibrary.FlushDelegate flushFn,
            NativeLibrary.TruncDelegate truncFn, int buffering = 4096) : base(ownsHandle: true)
        {
            _buffer = new byte[buffering];
            // To avoid using `unsafe` we add an extra layer of indirection:
            _bufferHandle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
            NativeLibrary.BufReadDelegate bufReadFn = BufRead;
            NativeLibrary.BufWriteDelegate bufWriteFn = BufWrite;
            var c11StreamBufHandle = new C11StreamBuf
            {
                managedBuffer = new ManagedBuffer
                {
                    // cannot use readFn delegate directly since the byte[] is not valid in the managed code context
                    buf_read = Marshal.GetFunctionPointerForDelegate(bufReadFn),
                    // FIXME: it should be possible to use the same delegate for write
                    buf_write = Marshal.GetFunctionPointerForDelegate(bufWriteFn),
                    data = _bufferHandle.AddrOfPinnedObject(),
                    size = (UIntPtr) _buffer.Length
                },
                seek = Marshal.GetFunctionPointerForDelegate(seekFn),
                flush = Marshal.GetFunctionPointerForDelegate(flushFn),
                trunc = Marshal.GetFunctionPointerForDelegate(truncFn)
            };
            // Initialize unmanaged memory to hold the struct.
            handle = Marshal.AllocHGlobal(Marshal.SizeOf(c11StreamBufHandle));
            Marshal.StructureToPtr(c11StreamBufHandle, handle, false);
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

        private NativeLibrary.BufWriteDelegate BufWriteDelegate { get; set; }

        private NativeLibrary.BufReadDelegate BufReadDelegate { get; set; }

        private int BufRead(int count)
        {
            var ret = Read(_buffer, count);
            return ret;
        }

        private int BufWrite(int count)
        {
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
            _bufferHandle.Free();
            Marshal.FreeHGlobal(handle);
            return true;
        }
    }
}
