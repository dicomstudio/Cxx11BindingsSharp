using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
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

    public enum SeekDirs
    {
        Begin = 0,
        Current = 1,
        End = 2,
    }

    internal enum ErrorCodes
    {
        // NullReferenceException
        C11_E_POINTER = unchecked((int) 0x80000000),

        // NotSupportedException
        C11_E_NOTSUPPORTED,

        // NotImplementedException
        C11_E_NOTIMPL,

        // InvalidArgument
        C11_E_INVALIDARG,

        // IOException
        C11_E_IO,
    };


    // The actual callbacks that will be used by the native library. They must be public for re-use.
    public static class NativeLibrary
    {
        #region c11 stream

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate int BufReadDelegate(IntPtr self, IntPtr buffer, int size);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate int BufWriteDelegate(IntPtr self, IntPtr buffer, int size);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ReadDelegate(IntPtr self, [Out] byte[] buffer, int size);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int WriteDelegate(IntPtr self, [In] byte[] buffer, int size);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate long SeekDelegate(IntPtr self, long offset, int seekDir);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int FlushDelegate(IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate long TruncDelegate(IntPtr self, long newSize);

        #endregion

        #region c11 error code

        internal static int ConvErrorToManaged(int errno)
        {
            var ret = (ErrorCodes) errno switch
            {
                ErrorCodes.C11_E_POINTER => new NullReferenceException().HResult,
                ErrorCodes.C11_E_NOTSUPPORTED => new NotSupportedException().HResult,
                ErrorCodes.C11_E_NOTIMPL => new NotImplementedException().HResult,
                ErrorCodes.C11_E_INVALIDARG => new ArgumentException(string.Empty).HResult,
                ErrorCodes.C11_E_IO => new IOException().HResult,
                _ => throw new NotImplementedException($"Error code {errno} not implemented")
            };

            return ret;
        }

        [DoesNotReturn]
        private static void HandleException(int errno)
        {
            var errorCode = ConvErrorToManaged(errno);
            var ex = Marshal.GetExceptionForHR(errorCode);
            if (ex == null)
            {
                throw new InvalidOperationException("Program error: cannot convert error code to exception");
            }

            throw ex;
        }

        internal static long HandleExceptionOrReturnValue(long err)
        {
            if (err >= 0) return err;
            var errno = checked((int) err); // throws OverflowException if out of range
            HandleException(errno);
            return 0; // Unreachable, but required for compilation
        }

        internal static int HandleExceptionOrReturnValue(int errno)
        {
            if (errno >= 0) return errno;
            HandleException(errno);
            return 0; // Unreachable, but required for compilation
        }

        #endregion
    }

    /// <summary>
    /// Main handle for the C11 streambuf struct. Can be used instead of a plain `IntPtr` in native p/invoke calls.
    /// Intended use is for c#/native author to access easily the underlying native code.
    /// </summary>
    public sealed class C11StreamHandle : SafeHandleMinusOneIsInvalid
    {
        private byte[] _buffer;

        public C11StreamHandle(NativeLibrary.ReadDelegate? readFn, NativeLibrary.WriteDelegate? writeFn,
            NativeLibrary.SeekDelegate? seekFn, NativeLibrary.FlushDelegate? flushFn,
            NativeLibrary.TruncDelegate? truncFn, int buffering = 4096) : base(ownsHandle: true)
        {
            _buffer = new byte[buffering];
            // To avoid using `unsafe`, we add an extra layer of indirection:
            NativeLibrary.BufReadDelegate bufReadFn = BufRead;
            NativeLibrary.BufWriteDelegate bufWriteFn = BufWrite;
            var c11StreamHandle = new C11Stream
            {
                read = readFn == null ? IntPtr.Zero : Marshal.GetFunctionPointerForDelegate(bufReadFn),
                write = writeFn == null ? IntPtr.Zero : Marshal.GetFunctionPointerForDelegate(bufWriteFn),
                seek = seekFn == null ? IntPtr.Zero : Marshal.GetFunctionPointerForDelegate(seekFn),
                flush = flushFn == null ? IntPtr.Zero : Marshal.GetFunctionPointerForDelegate(flushFn),
                trunc = truncFn == null ? IntPtr.Zero : Marshal.GetFunctionPointerForDelegate(truncFn)
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

        private int BufRead(IntPtr self, IntPtr outBuffer, int count)
        {
            try
            {
                Array.Resize(ref _buffer, count);
                var ret = Read!(self, _buffer, count);
                Marshal.Copy(_buffer, 0, outBuffer, count);
                return ret;
            }
            catch (Exception ex)
            {
                return ex.HResult;
            }
        }

        private int BufWrite(IntPtr self, IntPtr inBuffer, int count)
        {
            try
            {
                Array.Resize(ref _buffer, count);
                Marshal.Copy(inBuffer, _buffer, 0, count);
                var ret = Write!(self, _buffer, count);
                return ret;
            }
            catch (Exception ex)
            {
                return ex.HResult;
            }
        }

        internal NativeLibrary.TruncDelegate? Trunc { get; set; }

        internal NativeLibrary.FlushDelegate? Flush { get; set; }

        internal NativeLibrary.SeekDelegate? Seek { get; set; }

        internal NativeLibrary.WriteDelegate? Write { get; set; }

        internal NativeLibrary.ReadDelegate? Read { get; set; }

        public static implicit operator IntPtr(C11StreamHandle c11StreamHandle)
        {
            return c11StreamHandle.handle;
        }

        protected override bool ReleaseHandle()
        {
            Marshal.FreeHGlobal(handle);
            return true;
        }
    }
}
