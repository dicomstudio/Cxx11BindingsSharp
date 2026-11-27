using System;
#if NET6_0_OR_GREATER
using System.Buffers;
#endif
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
#if !NET6_0_OR_GREATER
using System.Threading;
#endif
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
        C11_E_INVALIDOPERATION,
        C11_E_UNAUTHORIZED,
        C11_E_OUTOFMEMORY,
        C11_E_TIMEOUT,
        C11_E_CANCELED,
        C11_E_FILENOTFOUND,
        C11_E_DIRECTORYNOTFOUND,
        C11_E_PATHTOOLONG,
        C11_E_ENDOFSTREAM,
        C11_E_ARGUMENTNULL,
        C11_E_NOTIMPLEMENTED,
        C11_E_UNMAPPED,
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
                ErrorToHResult.Add(ErrorCodes.C11_E_INVALIDOPERATION, new InvalidOperationException().HResult);
                ErrorToHResult.Add(ErrorCodes.C11_E_UNAUTHORIZED, new UnauthorizedAccessException().HResult);
                ErrorToHResult.Add(ErrorCodes.C11_E_OUTOFMEMORY, new OutOfMemoryException().HResult);
                ErrorToHResult.Add(ErrorCodes.C11_E_TIMEOUT, new TimeoutException().HResult);
                ErrorToHResult.Add(ErrorCodes.C11_E_CANCELED, new OperationCanceledException().HResult);
                ErrorToHResult.Add(ErrorCodes.C11_E_FILENOTFOUND, new FileNotFoundException().HResult);
                ErrorToHResult.Add(ErrorCodes.C11_E_DIRECTORYNOTFOUND, new DirectoryNotFoundException().HResult);
                ErrorToHResult.Add(ErrorCodes.C11_E_PATHTOOLONG, new PathTooLongException().HResult);
                ErrorToHResult.Add(ErrorCodes.C11_E_ENDOFSTREAM, new EndOfStreamException().HResult);
                ErrorToHResult.Add(ErrorCodes.C11_E_ARGUMENTNULL, new ArgumentNullException().HResult);
                ErrorToHResult.Add(ErrorCodes.C11_E_NOTIMPLEMENTED, new NotImplementedException().HResult);
                ErrorToHResult.Add(ErrorCodes.C11_E_UNMAPPED, unchecked((int) 0x80004005));

                foreach (var kvp in ErrorToHResult)
                    // NullReferenceException and ArgumentNullException share an HRESULT.
                    if (kvp.Key != ErrorCodes.C11_E_ARGUMENTNULL && kvp.Key != ErrorCodes.C11_E_UNMAPPED)
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
                if (HResultToError.TryGetValue(hresult, out var code))
                    return (int) code;
                Trace.TraceError("C11 callback failed with unmapped HRESULT 0x{0:X8}", hresult);
                return (int) ErrorCodes.C11_E_UNMAPPED;
            }

            public static int ToErrorCode(Exception exception)
            {
                if (exception is ArgumentNullException)
                    return (int) ErrorCodes.C11_E_ARGUMENTNULL;
                return ToErrorCode(exception.HResult);
            }
        }

        internal static long HandleExceptionOrReturnValue(long err)
        {
            if (err >= 0) return err;
            var errno = checked((int) err); // throws OverflowException if out of range
            if (errno == (int) ErrorCodes.C11_E_ARGUMENTNULL) throw new ArgumentNullException();
            if (errno == (int) ErrorCodes.C11_E_TIMEOUT) throw new TimeoutException();
            Marshal.ThrowExceptionForHR(ErrorCodeMap.ToHResult(errno));
            return 0; // Unreachable, but required for compilation
        }

        internal static int HandleExceptionOrReturnValue(int errno)
        {
            if (errno >= 0) return errno;
            if (errno == (int) ErrorCodes.C11_E_ARGUMENTNULL) throw new ArgumentNullException();
            if (errno == (int) ErrorCodes.C11_E_TIMEOUT) throw new TimeoutException();
            Marshal.ThrowExceptionForHR(ErrorCodeMap.ToHResult(errno));
            return 0; // Unreachable, but required for compilation
        }

        #endregion
    }

    /// <summary>
    /// Main handle for the C11 streambuf struct. Pass this handle directly to synchronous native P/Invoke calls
    /// so that its lifetime is protected for the duration of the call.
    /// </summary>
    public sealed class C11StreamHandle : SafeHandleMinusOneIsInvalid
    {
#if !NET6_0_OR_GREATER
        private byte[] _buffer;
#endif

        public C11StreamHandle(
#if NET6_0_OR_GREATER
            NativeLibrary.ReadDelegate? readFn, NativeLibrary.WriteDelegate? writeFn,
            NativeLibrary.SeekDelegate? seekFn, NativeLibrary.FlushDelegate? flushFn,
            NativeLibrary.TruncDelegate? truncFn,
#else
            NativeLibrary.ReadDelegate readFn, NativeLibrary.WriteDelegate writeFn,
            NativeLibrary.SeekDelegate seekFn, NativeLibrary.FlushDelegate flushFn,
            NativeLibrary.TruncDelegate truncFn,
#endif
            int buffering = 4096) : base(ownsHandle: true)
        {
#if !NET6_0_OR_GREATER
            _buffer = new byte[buffering];
#endif
            // To avoid using `unsafe`, we add an extra layer of indirection:
            NativeLibrary.BufReadDelegate bufReadFn = BufRead;
            NativeLibrary.BufWriteDelegate bufWriteFn = BufWrite;
            NativeLibrary.SeekDelegate bufSeekFn = BufSeek;
            NativeLibrary.FlushDelegate bufFlushFn = BufFlush;
            NativeLibrary.TruncDelegate bufTruncFn = BufTrunc;
            var c11StreamHandle = new C11Stream
            {
                read = readFn == null ? IntPtr.Zero : Marshal.GetFunctionPointerForDelegate(bufReadFn),
                write = writeFn == null ? IntPtr.Zero : Marshal.GetFunctionPointerForDelegate(bufWriteFn),
                seek = seekFn == null ? IntPtr.Zero : Marshal.GetFunctionPointerForDelegate(bufSeekFn),
                flush = flushFn == null ? IntPtr.Zero : Marshal.GetFunctionPointerForDelegate(bufFlushFn),
                trunc = truncFn == null ? IntPtr.Zero : Marshal.GetFunctionPointerForDelegate(bufTruncFn)
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
            BufSeekDelegate = bufSeekFn;
            BufFlushDelegate = bufFlushFn;
            BufTruncDelegate = bufTruncFn;
        }

        private NativeLibrary.BufReadDelegate BufReadDelegate { get; set; }
        private NativeLibrary.BufWriteDelegate BufWriteDelegate { get; set; }
        private NativeLibrary.SeekDelegate BufSeekDelegate { get; set; }
        private NativeLibrary.FlushDelegate BufFlushDelegate { get; set; }
        private NativeLibrary.TruncDelegate BufTruncDelegate { get; set; }

        private byte[] EnsureBufferSize(int newSize)
        {
#if NET6_0_OR_GREATER
            var buffer = ArrayPool<byte>.Shared.Rent(newSize);
            return buffer;
#else
            // Lease the cached buffer exclusively, including during reentrant callbacks.
            var buffer = Interlocked.Exchange(ref _buffer, null);
            return buffer == null || buffer.Length < newSize ? new byte[newSize] : buffer;
#endif
        }

        private void ReturnBuffer(byte[] buffer)
        {
#if NET6_0_OR_GREATER
            Array.Clear(buffer, 0, buffer.Length);
            ArrayPool<byte>.Shared.Return(buffer);
#else
            Interlocked.CompareExchange(ref _buffer, buffer, null);
#endif
        }

        private int BufRead(IntPtr self, IntPtr outBuffer, int count)
        {
            if (count < 0) return (int) ErrorCodes.C11_E_ARGUMENTOUTOFRANGE;
            if (count > 0 && outBuffer == IntPtr.Zero) return (int) ErrorCodes.C11_E_POINTER;
#if NET6_0_OR_GREATER
            byte[]? buffer = null;
#else
            byte[] buffer = null;
#endif
            try
            {
                buffer = EnsureBufferSize(count);
#if NET6_0_OR_GREATER
                var ret = Read!(self, buffer, count);
#else
                var ret = Read(self, buffer, count);
#endif
                if (ret < 0) return ret;
                if (ret > count) return (int) ErrorCodes.C11_E_IO;
                if (ret > 0) Marshal.Copy(buffer, 0, outBuffer, ret);
                return ret;
            }
            catch (Exception ex)
            {
                return NativeLibrary.ErrorCodeMap.ToErrorCode(ex);
            }
            finally
            {
                if (buffer != null) ReturnBuffer(buffer);
            }
        }

        private int BufWrite(IntPtr self, IntPtr inBuffer, int count)
        {
            if (count < 0) return (int) ErrorCodes.C11_E_ARGUMENTOUTOFRANGE;
            if (count > 0 && inBuffer == IntPtr.Zero) return (int) ErrorCodes.C11_E_POINTER;
#if NET6_0_OR_GREATER
            byte[]? buffer = null;
#else
            byte[] buffer = null;
#endif
            try
            {
                buffer = EnsureBufferSize(count);
                if (count > 0) Marshal.Copy(inBuffer, buffer, 0, count);
#if NET6_0_OR_GREATER
                var ret = Write!(self, buffer, count);
#else
                var ret = Write(self, buffer, count);
#endif
                if (ret > count) return (int) ErrorCodes.C11_E_IO;
                return ret;
            }
            catch (Exception ex)
            {
                return NativeLibrary.ErrorCodeMap.ToErrorCode(ex);
            }
            finally
            {
                if (buffer != null) ReturnBuffer(buffer);
            }
        }

        private long BufSeek(IntPtr self, long offset, int origin)
        {
            try
            {
#if NET6_0_OR_GREATER
                return Seek!(self, offset, origin);
#else
                return Seek(self, offset, origin);
#endif
            }
            catch (Exception ex)
            {
                return NativeLibrary.ErrorCodeMap.ToErrorCode(ex);
            }
        }

        private int BufFlush(IntPtr self)
        {
            try
            {
#if NET6_0_OR_GREATER
                return Flush!(self);
#else
                return Flush(self);
#endif
            }
            catch (Exception ex)
            {
                return NativeLibrary.ErrorCodeMap.ToErrorCode(ex);
            }
        }

        private long BufTrunc(IntPtr self, long length)
        {
            try
            {
#if NET6_0_OR_GREATER
                return Trunc!(self, length);
#else
                return Trunc(self, length);
#endif
            }
            catch (Exception ex)
            {
                return NativeLibrary.ErrorCodeMap.ToErrorCode(ex);
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
