using System;
#if NET6_0_OR_GREATER
using System.Buffers;
#endif
using System.Collections.Generic;
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

        // InvalidArgument
        C11_E_INVALIDARG,

        // ArgumentOutOfRangeException
        C11_E_ARGUMENTOUTOFRANGE,

        // ObjectDisposedException
        C11_E_OBJECTDISPOSED,

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

        #region c11 file descriptor

        private const string LibraryName = "Cxx11BindingsTests";
        private const CallingConvention CallConv = CallingConvention.Cdecl;

        [DllImport(LibraryName, CallingConvention = CallConv, ExactSpelling = true)]
        internal static extern int c11_handle_stream_init(out IntPtr pHandle, IntPtr intPtr);

        [DllImport(LibraryName, CallingConvention = CallConv, ExactSpelling = true)]
        internal static extern int c11_fd_stream_init(out IntPtr pHandle, IntPtr intPtr);

        #endregion

        #region c11 error code

        internal static class ErrorCodeMap
        {
            private static readonly Dictionary<ErrorCodes, int> ErrorToHResult = new Dictionary<ErrorCodes, int>();
            private static readonly Dictionary<int, ErrorCodes> HResultToError = new Dictionary<int, ErrorCodes>();

            // https://learn.microsoft.com/en-us/dotnet/framework/interop/how-to-map-hresults-and-exceptions
            static ErrorCodeMap()
            {
                ErrorToHResult.Add(ErrorCodes.C11_E_POINTER, new NullReferenceException().HResult);
                ErrorToHResult.Add(ErrorCodes.C11_E_NOTSUPPORTED, new NotSupportedException().HResult);
                ErrorToHResult.Add(ErrorCodes.C11_E_INVALIDARG, new ArgumentException(string.Empty).HResult);
                ErrorToHResult.Add(ErrorCodes.C11_E_ARGUMENTOUTOFRANGE, new ArgumentOutOfRangeException().HResult);
                ErrorToHResult.Add(ErrorCodes.C11_E_OBJECTDISPOSED, new ObjectDisposedException(string.Empty).HResult);
                ErrorToHResult.Add(ErrorCodes.C11_E_IO, new IOException().HResult);

                foreach (var kvp in ErrorToHResult)
                    HResultToError.Add(kvp.Value, kvp.Key);
            }

            public static int ToHResult(int errno)
            {
                var code = (ErrorCodes) errno;
                return ErrorToHResult.TryGetValue(code, out var hr)
                    ? hr
                    : throw new NotImplementedException("Error code " + code + " not implemented");
            }

            public static int ToErrorCode(int hresult)
            {
                return HResultToError.TryGetValue(hresult, out var code)
                    ? (int) code
                    : throw new NotImplementedException("HResult " + hresult + " not implemented");
            }
        }

        internal static long HandleExceptionOrReturnValue(long err)
        {
            if (err >= 0) return err;
            var errno = checked((int) err); // throws OverflowException if out of range
            Marshal.ThrowExceptionForHR(ErrorCodeMap.ToHResult(errno));
            return 0; // Unreachable, but required for compilation
        }

        internal static int HandleExceptionOrReturnValue(int errno)
        {
            if (errno >= 0) return errno;
            Marshal.ThrowExceptionForHR(ErrorCodeMap.ToHResult(errno));
            return 0; // Unreachable, but required for compilation
        }

        #endregion
    }

    public class C11StreamHandle : SafeHandleMinusOneIsInvalid
    {
        protected C11StreamHandle() : base(ownsHandle: true)
        {
        }

        protected override bool ReleaseHandle()
        {
            return true;
        }

        public static implicit operator IntPtr(C11StreamHandle c11StreamHandle)
        {
            return c11StreamHandle.handle;
        }
    }

    public sealed class C11StreamHandleNative : C11StreamHandle
    {
        public C11StreamHandleNative(SafeFileHandle sfh)
        {
            var ptr = sfh.DangerousGetHandle();
            var ret = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? NativeLibrary.c11_handle_stream_init(out handle, ptr)
                : NativeLibrary.c11_fd_stream_init(out handle, ptr);
            if (ret < 0)
            {
                throw new IOException($"Cannot handle file {ptr}");
            }
        }

        protected override bool ReleaseHandle()
        {
            // NativeLibraryTests.c11_handle_stream_delete(handle)
            return true;
        }
    }


    /// <summary>
    /// Main handle for the C11 streambuf struct. Can be used instead of a plain `IntPtr` in native p/invoke calls.
    /// Intended use is for c#/native author to access easily the underlying native code.
    /// </summary>
    public sealed class C11StreamHandleManaged : C11StreamHandle
    {
#if !NET6_0_OR_GREATER
        private byte[] _buffer;
#endif

        public C11StreamHandleManaged(
#if NET6_0_OR_GREATER
            NativeLibrary.ReadDelegate? readFn, NativeLibrary.WriteDelegate? writeFn,
            NativeLibrary.SeekDelegate? seekFn, NativeLibrary.FlushDelegate? flushFn,
            NativeLibrary.TruncDelegate? truncFn,
#else
            NativeLibrary.ReadDelegate readFn, NativeLibrary.WriteDelegate writeFn,
            NativeLibrary.SeekDelegate seekFn, NativeLibrary.FlushDelegate flushFn,
            NativeLibrary.TruncDelegate truncFn,
#endif
            int buffering = 4096)
        {
#if !NET6_0_OR_GREATER
            _buffer = new byte[buffering];
#endif
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

        private byte[] EnsureBufferSize(int newSize)
        {
#if NET6_0_OR_GREATER
            var buffer = ArrayPool<byte>.Shared.Rent(newSize);
            return buffer;
#else
            if (_buffer.Length < newSize)
            {
                Array.Resize(ref _buffer, newSize);
            }

            return _buffer;
#endif
        }

        private int BufRead(IntPtr self, IntPtr outBuffer, int count)
        {
            try
            {
                var buffer = EnsureBufferSize(count);
#if NET6_0_OR_GREATER
                var ret = Read!(self, buffer, count);
#else
                var ret = Read(self, buffer, count);
#endif
                Marshal.Copy(buffer, 0, outBuffer, count);
                return ret;
            }
            catch (Exception ex)
            {
                return NativeLibrary.ErrorCodeMap.ToErrorCode(ex.HResult);
            }
        }

        private int BufWrite(IntPtr self, IntPtr inBuffer, int count)
        {
            try
            {
                var buffer = EnsureBufferSize(count);
                Marshal.Copy(inBuffer, buffer, 0, count);
#if NET6_0_OR_GREATER
                var ret = Write!(self, buffer, count);
#else
                var ret = Write(self, buffer, count);
#endif
                return ret;
            }
            catch (Exception ex)
            {
                return NativeLibrary.ErrorCodeMap.ToErrorCode(ex.HResult);
            }
        }

#if NET6_0_OR_GREATER
        internal NativeLibrary.ReadDelegate? Read { get; set; }
        internal NativeLibrary.WriteDelegate? Write { get; set; }
        internal NativeLibrary.SeekDelegate? Seek { get; set; }
        internal NativeLibrary.FlushDelegate? Flush { get; set; }
        internal NativeLibrary.TruncDelegate? Trunc { get; set; }
#else
        internal NativeLibrary.ReadDelegate Read { get; set; }
        internal NativeLibrary.WriteDelegate Write { get; set; }
        internal NativeLibrary.SeekDelegate Seek { get; set; }
        internal NativeLibrary.FlushDelegate Flush { get; set; }
        internal NativeLibrary.TruncDelegate Trunc { get; set; }
#endif


        protected override bool ReleaseHandle()
        {
            Marshal.FreeHGlobal(handle);
            return true;
        }
    }
}
