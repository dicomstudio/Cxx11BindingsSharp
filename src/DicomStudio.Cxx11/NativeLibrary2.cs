using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace DicomStudio.Cxx11
{
    internal sealed class CStreamHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        /// <summary>
        /// default constructor
        /// </summary>
        public CStreamHandle() : base(ownsHandle: true)
        {
            // pay attention that at this point 'handle' is still null pointer, the create_* function is still NOT called yet.
            //var s = Marshal.PtrToStructure<CStream>(handle);
            //Console.WriteLine(s.read.ToString());
            //Console.WriteLine(handle.ToString());
            //Func<byte[], int, int> read = Read;
        }

        internal NativeLibrary2.ReadDelegate? Read { get; set; }
        internal NativeLibrary2.WriteDelegate? Write { get; set; }
        internal NativeLibrary2.SeekDelegate? Seek { get; set; }
        internal NativeLibrary2.FlushDelegate? Flush { get; set; }

        /// <summary>
        /// release the handle
        /// </summary>
        /// <returns>true</returns>
        protected override bool ReleaseHandle()
        {
            return NativeLibrary2.cxx11_stream_delete(handle) == 0;
        }
    }

    public sealed class CStreamWrapper : IDisposable
    {
        private readonly CStreamHandle _handle;

        public CStreamWrapper(NativeLibrary2.ReadDelegate readFn, NativeLibrary2.WriteDelegate writeFn,
            NativeLibrary2.SeekDelegate seekFn, NativeLibrary2.FlushDelegate flushFn)
        {
            _handle = NativeLibrary2.cxx11_stream_create(readFn, writeFn, seekFn, flushFn);
            if (_handle.IsInvalid)
                throw new ArgumentException("Invalid parameters");
            _handle.Read = readFn;
            _handle.Write = writeFn;
            _handle.Seek = seekFn;
            _handle.Flush = flushFn;
        }

        public void Dispose()
        {
            _handle.Dispose();
        }
    }

    public static class NativeLibrary2
    {
        public static int ExceptionToInt(Exception? ex)
        {
            // The HResult for a NullReferenceException in .NET is 0x80004003.
            return ex?.HResult ?? (new NullReferenceException()).HResult;
        }

        #region managed streambuf

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ReadDelegate([Out] byte[] buffer, int size);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int WriteDelegate([In] byte[] buffer, int size);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate long SeekDelegate(long off, int dir);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int FlushDelegate();

        private const string LibraryName = "Cxx11Bindings";
        private const CallingConvention CallConv = CallingConvention.Cdecl;

        [DllImport(LibraryName, CallingConvention = CallConv)]
        internal static extern CStreamHandle cxx11_stream_create(ReadDelegate read, WriteDelegate write,
            SeekDelegate seek, FlushDelegate flush);

        [DllImport(LibraryName, CallingConvention = CallConv)]
        internal static extern int cxx11_stream_delete(IntPtr handle);

        #endregion
    }
}
