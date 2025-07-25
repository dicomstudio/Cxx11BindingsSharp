using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Cxx11BindingsSharp
{
    internal sealed class C11StreamHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        /// <summary>
        /// default constructor
        /// </summary>
        public C11StreamHandle() : base(ownsHandle: true)
        {
        }

        internal NativeLibrary.ReadDelegate? Read { get; set; }
        internal NativeLibrary.WriteDelegate? Write { get; set; }
        internal NativeLibrary.SeekDelegate? Seek { get; set; }
        internal NativeLibrary.FlushDelegate? Flush { get; set; }
        internal NativeLibrary.TruncDelegate? Trunc { get; set; }

        /// <summary>
        /// release the handle
        /// </summary>
        /// <returns>true</returns>
        protected override bool ReleaseHandle()
        {
            return NativeLibrary.c11_stream_delete(handle) == 0;
        }
    }

    public sealed class C11StreamWrapper : IDisposable
    {
        private readonly C11StreamHandle _handle;

        public C11StreamWrapper(NativeLibrary.ReadDelegate readFn, NativeLibrary.WriteDelegate writeFn,
            NativeLibrary.SeekDelegate seekFn, NativeLibrary.FlushDelegate flushFn, NativeLibrary.TruncDelegate truncFn)
        {
            _handle = NativeLibrary.c11_stream_create(readFn, writeFn, seekFn, flushFn, truncFn);
            if (_handle.IsInvalid)
                throw new ArgumentException("Invalid parameters");
            // keep delegates alive:
            _handle.Read = readFn;
            _handle.Write = writeFn;
            _handle.Seek = seekFn;
            _handle.Flush = flushFn;
            _handle.Trunc = truncFn;
        }

        public void Dispose()
        {
            _handle.Dispose();
        }
    }

    public static class NativeLibrary
    {
        #region c11 stream

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ReadDelegate([Out] byte[] buffer, uint size);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int WriteDelegate([In] byte[] buffer, uint size);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate long SeekDelegate(long off, int dir);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int FlushDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int TruncDelegate(ulong length);

        private const string LibraryName = "Cxx11Bindings";
        private const CallingConvention CallConv = CallingConvention.Cdecl;

        [DllImport(LibraryName, CallingConvention = CallConv)]
        internal static extern C11StreamHandle c11_stream_create(ReadDelegate read, WriteDelegate write,
            SeekDelegate seek, FlushDelegate flush, TruncDelegate trunc);

        [DllImport(LibraryName, CallingConvention = CallConv)]
        internal static extern int c11_stream_delete(IntPtr handle);

        #endregion
    }
}
