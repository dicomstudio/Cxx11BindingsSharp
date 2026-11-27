using System;
using System.IO;
#if !NET6_0_OR_GREATER
using System.Linq;
#endif
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Xunit;

namespace Cxx11BindingsSharp.Tests
{
    public static class NativeLibraryTests
    {
        #region c11 stream

        private const string LibraryName = "Cxx11BindingsTests";
        private const CallingConvention CallConv = CallingConvention.Cdecl;

        [DllImport(LibraryName, CallingConvention = CallConv, ExactSpelling = true)]
        internal static extern int test_c11_stream_read(IntPtr handle, [Out] byte[] buffer, int length);

        [DllImport(LibraryName, CallingConvention = CallConv, ExactSpelling = true)]
        internal static extern int test_c11_stream_write(IntPtr handle, [In] byte[] buffer, int length);

        [DllImport(LibraryName, CallingConvention = CallConv, ExactSpelling = true)]
        internal static extern long test_c11_stream_seek(IntPtr handle, long offset, SeekDirs seekDir);

        [DllImport(LibraryName, CallingConvention = CallConv, ExactSpelling = true)]
        internal static extern int test_c11_stream_flush(IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallConv, ExactSpelling = true)]
        internal static extern long test_c11_stream_trunc(IntPtr handle, long newSize);

        #endregion

        #region c11 file stream

        [DllImport(LibraryName, CallingConvention = CallConv, ExactSpelling = true, CharSet = CharSet.Unicode)]
        internal static extern int c11_file_stream_create1(out IntPtr pHandle,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string filename, [MarshalAs(UnmanagedType.LPUTF8Str)] string mode);

        [DllImport(LibraryName, CallingConvention = CallConv, ExactSpelling = true, CharSet = CharSet.Unicode)]
        internal static extern int c11_file_stream_create2(out IntPtr pHandle,
            [MarshalAs(UnmanagedType.LPWStr)] string filename, [MarshalAs(UnmanagedType.LPWStr)] string mode);

        [DllImport(LibraryName, CallingConvention = CallConv, ExactSpelling = true)]
        internal static extern int c11_file_stream_destroy(IntPtr handle);

        #endregion

        #region cxx11 file stream

        [DllImport(LibraryName, CallingConvention = CallConv, ExactSpelling = true, CharSet = CharSet.Unicode)]
        internal static extern int cxx11_file_stream_create1(out IntPtr pHandle,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string filename, [MarshalAs(UnmanagedType.LPUTF8Str)] string mode);

        [DllImport(LibraryName, CallingConvention = CallConv, ExactSpelling = true, CharSet = CharSet.Unicode)]
        internal static extern int cxx11_file_stream_create2(out IntPtr pHandle,
            [MarshalAs(UnmanagedType.LPWStr)] string filename, [MarshalAs(UnmanagedType.LPWStr)] string mode);

        [DllImport(LibraryName, CallingConvention = CallConv, ExactSpelling = true)]
        internal static extern int cxx11_file_stream_destroy(IntPtr handle);

        #endregion
    }

    // Test adapter: fopen cannot reproduce FileStream sharing or all open-mode semantics.
    public sealed class C11FileStream : Stream
    {
        private const FileShare DefaultShare = FileShare.Read;
        private readonly NativeFileHandle _handle;
        private readonly bool _canRead;
        private readonly bool _canWrite;
        private bool _disposed;

        private sealed class NativeFileHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            public NativeFileHandle(IntPtr handle) : base(true) => SetHandle(handle);

            protected override bool ReleaseHandle() =>
                NativeLibraryTests.c11_file_stream_destroy(handle) >= 0;
        }

        [DllImport("Cxx11BindingsTests", EntryPoint = "test_c11_stream_read", CallingConvention = CallingConvention.Cdecl)]
        private static extern int NativeRead(NativeFileHandle handle, [Out] byte[] buffer, int count);

        [DllImport("Cxx11BindingsTests", EntryPoint = "test_c11_stream_write", CallingConvention = CallingConvention.Cdecl)]
        private static extern int NativeWrite(NativeFileHandle handle, [In] byte[] buffer, int count);

        [DllImport("Cxx11BindingsTests", EntryPoint = "test_c11_stream_seek", CallingConvention = CallingConvention.Cdecl)]
        private static extern long NativeSeek(NativeFileHandle handle, long offset, SeekDirs origin);

        [DllImport("Cxx11BindingsTests", EntryPoint = "test_c11_stream_flush", CallingConvention = CallingConvention.Cdecl)]
        private static extern int NativeFlush(NativeFileHandle handle);

        [DllImport("Cxx11BindingsTests", EntryPoint = "test_c11_stream_trunc", CallingConvention = CallingConvention.Cdecl)]
        private static extern long NativeTruncate(NativeFileHandle handle, long length);

        private static string ToFopenMode(FileMode mode, FileAccess access, FileShare share = FileShare.None)
        {
            // FileShare ignored: C fopen doesn’t support it.
            switch (mode)
            {
                case FileMode.Open:
                    if (access == FileAccess.Read) return "r";
                    if (access == FileAccess.Write) return "r+"; // WARNING: may not always match semantics
                    if (access == FileAccess.ReadWrite) return "r+";
                    break;

                case FileMode.Create:
                case FileMode.Truncate:
                    if (access == FileAccess.Read) return "w+"; // read+truncate
                    if (access == FileAccess.Write) return "w";
                    if (access == FileAccess.ReadWrite) return "w+";
                    break;

                case FileMode.Append:
                    if (access == FileAccess.Read) return "a+"; // read + append
                    if (access == FileAccess.Write) return "a";
                    if (access == FileAccess.ReadWrite) return "a+";
                    break;

                case FileMode.OpenOrCreate:
                    // C’s fopen doesn’t have a direct OpenOrCreate, best effort:
                    if (access == FileAccess.Read) return "a+"; // opens existing or creates, allows read
                    if (access == FileAccess.Write) return "a";
                    if (access == FileAccess.ReadWrite) return "a+";
                    break;

                case FileMode.CreateNew:
                    throw new NotSupportedException("The C fopen backend cannot atomically create a new file.");
            }

            throw new ArgumentException($"Unsupported combination: {mode}, {access}");
        }

        /// <summary>
        /// constructor
        /// </summary>
        public C11FileStream(string filename, FileMode fileMode, FileAccess fileAccess, FileShare fileShare)
        {
            if (filename == null) throw new ArgumentNullException(nameof(filename));
            if ((fileMode == FileMode.Create || fileMode == FileMode.CreateNew || fileMode == FileMode.Truncate) &&
                fileAccess == FileAccess.Read)
                throw new ArgumentException("The selected mode requires write access.", "access");
            if (fileMode == FileMode.Append && fileAccess != FileAccess.Write)
                throw new ArgumentException("Append mode requires write-only access.", "access");
            var mode = ToFopenMode(fileMode, fileAccess, fileShare) + "b";

            IntPtr stream;
            var ret = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? NativeLibraryTests.c11_file_stream_create2(out stream, filename, mode)
                : NativeLibraryTests.c11_file_stream_create1(out stream, filename, mode);
            if (ret < 0)
            {
                throw new IOException($"Cannot open file: {filename} with mode: {mode}");
            }

            _handle = new NativeFileHandle(stream);
            _canRead = fileAccess == FileAccess.Read || fileAccess == FileAccess.ReadWrite;
            _canWrite = fileAccess == FileAccess.Write || fileAccess == FileAccess.ReadWrite;
        }

        public C11FileStream(string filename, FileMode fileMode, FileAccess fileAccess) : this(filename, fileMode,
            fileAccess, DefaultShare)
        {
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                try
                {
                    if (disposing && _canWrite) Flush();
                }
                finally
                {
                    _disposed = true;
                    _handle.Dispose();
                    base.Dispose(disposing);
                }
                return;
            }
            base.Dispose(disposing);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(C11FileStream));
        }

        private static void ValidateBuffer(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
#if NET6_0_OR_GREATER
                throw new ArgumentNullException(nameof(buffer));
#else
                throw new ArgumentNullException("array");
#endif
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (buffer.Length - offset < count)
#if NET6_0_OR_GREATER
                throw new ArgumentOutOfRangeException(nameof(count));
#else
                throw new ArgumentException("Offset and count exceed the buffer length.");
#endif
        }

        public override void Flush()
        {
            ThrowIfDisposed();
            if (!_canWrite) return;
            var ret = NativeFlush(_handle);
            NativeLibrary.HandleExceptionOrReturnValue(ret);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ValidateBuffer(buffer, offset, count);
            ThrowIfDisposed();
            if (!_canRead) throw new NotSupportedException("Stream does not support reading.");
            if (count == 0) return 0;
            if (_canWrite) Seek(0, SeekOrigin.Current);
            var bytes = new byte[count];
            var read = NativeLibrary.HandleExceptionOrReturnValue(NativeRead(_handle, bytes, count));
            Buffer.BlockCopy(bytes, 0, buffer, offset, read);
            return read;
        }


        public override long Seek(long offset, SeekOrigin origin)
        {
#if NET6_0
            ThrowIfDisposed();
#endif
            if (origin != SeekOrigin.Begin && origin != SeekOrigin.Current && origin != SeekOrigin.End)
#if NET6_0_OR_GREATER
                throw new ArgumentException("Invalid seek origin.", nameof(origin));
#else
                throw new ArgumentException("Invalid seek origin.");
#endif
            ThrowIfDisposed();
            var ret = NativeSeek(_handle, offset, ManagedC11Stream.ToSeekDirs(origin));
            if (ret == (long) ErrorCodes.C11_E_INVALIDARG) throw new IOException("Unable to seek to the requested position.");
            return NativeLibrary.HandleExceptionOrReturnValue(ret);
        }

        public override void SetLength(long value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            ThrowIfDisposed();
            if (!_canWrite) throw new NotSupportedException("Stream does not support writing.");
            var position = Position;
            Flush();
            var ret = NativeTruncate(_handle, value);
            NativeLibrary.HandleExceptionOrReturnValue(ret);
            Seek(Math.Min(position, value), SeekOrigin.Begin);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            ValidateBuffer(buffer, offset, count);
            ThrowIfDisposed();
            if (!_canWrite) throw new NotSupportedException("Stream does not support writing.");
            if (count == 0) return;
            if (_canRead) Seek(0, SeekOrigin.Current);
            var bytes = new byte[count];
            Buffer.BlockCopy(buffer, offset, bytes, 0, count);
            var written = NativeLibrary.HandleExceptionOrReturnValue(NativeWrite(_handle, bytes, count));
            if (written != count) throw new IOException("Unable to write the entire buffer.");
        }

        public override bool CanRead => !_disposed && _canRead;

        public override bool CanSeek => !_disposed;


        public override bool CanWrite => !_disposed && _canWrite;

        private long GetLength()
        {
            var current = Seek(0, SeekOrigin.Current);
            var size = Seek(0, SeekOrigin.End);
            Seek(current, SeekOrigin.Begin);
            return size;
        }

        public override long Length => GetLength();

        public override long Position
        {
            get => Seek(0, SeekOrigin.Current);
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
                Seek(value, SeekOrigin.Begin);
            }
        }
    }

    public sealed class Cxx11FileStream : Stream
    {
        private const FileShare DefaultShare = FileShare.Read;
        private readonly NativeFileHandle _handle;
        private readonly bool _canRead;
        private readonly bool _canWrite;
        private bool _disposed;

        private sealed class NativeFileHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            public NativeFileHandle(IntPtr handle) : base(true) => SetHandle(handle);

            protected override bool ReleaseHandle() =>
                NativeLibraryTests.cxx11_file_stream_destroy(handle) >= 0;
        }

        [DllImport("Cxx11BindingsTests", EntryPoint = "test_c11_stream_read", CallingConvention = CallingConvention.Cdecl)]
        private static extern int NativeRead(NativeFileHandle handle, [Out] byte[] buffer, int count);

        [DllImport("Cxx11BindingsTests", EntryPoint = "test_c11_stream_write", CallingConvention = CallingConvention.Cdecl)]
        private static extern int NativeWrite(NativeFileHandle handle, [In] byte[] buffer, int count);

        [DllImport("Cxx11BindingsTests", EntryPoint = "test_c11_stream_seek", CallingConvention = CallingConvention.Cdecl)]
        private static extern long NativeSeek(NativeFileHandle handle, long offset, SeekDirs origin);

        [DllImport("Cxx11BindingsTests", EntryPoint = "test_c11_stream_flush", CallingConvention = CallingConvention.Cdecl)]
        private static extern int NativeFlush(NativeFileHandle handle);

        [DllImport("Cxx11BindingsTests", EntryPoint = "test_c11_stream_trunc", CallingConvention = CallingConvention.Cdecl)]
        private static extern long NativeTruncate(NativeFileHandle handle, long length);

        private static string ToFopenMode(FileMode mode, FileAccess access, FileShare share = FileShare.None)
        {
            // FileShare ignored: C fopen doesn’t support it.
            switch (mode)
            {
                case FileMode.Open:
                    if (access == FileAccess.Read) return "r";
                    if (access == FileAccess.Write) return "r+"; // WARNING: may not always match semantics
                    if (access == FileAccess.ReadWrite) return "r+";
                    break;

                case FileMode.Create:
                case FileMode.CreateNew:
                case FileMode.Truncate:
                    if (access == FileAccess.Read) return "w+"; // read+truncate
                    if (access == FileAccess.Write) return "w";
                    if (access == FileAccess.ReadWrite) return "w+";
                    break;

                case FileMode.Append:
                    if (access == FileAccess.Read) return "a+"; // read + append
                    if (access == FileAccess.Write) return "a";
                    if (access == FileAccess.ReadWrite) return "a+";
                    break;

                case FileMode.OpenOrCreate:
                    // C’s fopen doesn’t have a direct OpenOrCreate, best effort:
                    if (access == FileAccess.Read) return "a+"; // opens existing or creates, allows read
                    if (access == FileAccess.Write) return "a";
                    if (access == FileAccess.ReadWrite) return "a+";
                    break;
            }

            throw new ArgumentException($"Unsupported combination: {mode}, {access}");
        }

        /// <summary>
        /// constructor
        /// </summary>
        public Cxx11FileStream(string filename, FileMode fileMode, FileAccess fileAccess, FileShare fileShare)
        {
            var mode = ToFopenMode(fileMode, fileAccess, fileShare) + "b";

            IntPtr stream;
            var ret = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? NativeLibraryTests.cxx11_file_stream_create2(out stream, filename, mode)
                : NativeLibraryTests.cxx11_file_stream_create1(out stream, filename, mode);
            if (ret < 0)
            {
                throw new IOException($"Cannot open file: {filename} with mode: {mode}");
            }

            _handle = new NativeFileHandle(stream);
            _canRead = fileAccess == FileAccess.Read || fileAccess == FileAccess.ReadWrite;
            _canWrite = fileAccess == FileAccess.Write || fileAccess == FileAccess.ReadWrite;
        }

        public Cxx11FileStream(string filename, FileMode fileMode, FileAccess fileAccess) : this(filename, fileMode,
            fileAccess, DefaultShare)
        {
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                _handle.Dispose();
            }
            base.Dispose(disposing);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(Cxx11FileStream));
        }

        private static void ValidateBuffer(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
#if NET6_0_OR_GREATER
                throw new ArgumentNullException(nameof(buffer));
#else
                throw new ArgumentNullException("array");
#endif
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (buffer.Length - offset < count)
#if NET6_0_OR_GREATER
                throw new ArgumentOutOfRangeException(nameof(count));
#else
                throw new ArgumentException("Offset and count exceed the buffer length.");
#endif
        }

        public override void Flush()
        {
            ThrowIfDisposed();
            var ret = NativeFlush(_handle);
            if (ret == -1) throw new IOException("Unable to flush the file.");
            NativeLibrary.HandleExceptionOrReturnValue(ret);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ValidateBuffer(buffer, offset, count);
            ThrowIfDisposed();
            if (!_canRead) throw new NotSupportedException("Stream does not support reading.");
            if (count == 0) return 0;
            var bytes = new byte[count];
            var read = NativeLibrary.HandleExceptionOrReturnValue(NativeRead(_handle, bytes, count));
            Buffer.BlockCopy(bytes, 0, buffer, offset, read);
            return read;
        }


        public override long Seek(long offset, SeekOrigin origin)
        {
#if NET6_0
            ThrowIfDisposed();
#endif
            if (origin != SeekOrigin.Begin && origin != SeekOrigin.Current && origin != SeekOrigin.End)
#if NET6_0_OR_GREATER
                throw new ArgumentException("Invalid seek origin.", nameof(origin));
#else
                throw new ArgumentException("Invalid seek origin.");
#endif
            var dir = ManagedC11Stream.ToSeekDirs(origin);
            ThrowIfDisposed();
            var ret = NativeSeek(_handle, offset, dir);
            if (ret == -1) throw new IOException("Unable to seek to the requested position.");
            return NativeLibrary.HandleExceptionOrReturnValue(ret);
        }

        public override void SetLength(long value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            ThrowIfDisposed();
            if (!_canWrite) throw new NotSupportedException("Stream does not support writing.");
            var ret = NativeTruncate(_handle, value);
            NativeLibrary.HandleExceptionOrReturnValue(ret);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            ValidateBuffer(buffer, offset, count);
            ThrowIfDisposed();
            if (!_canWrite) throw new NotSupportedException("Stream does not support writing.");
            if (count == 0) return;
            var bytes = new byte[count];
            Buffer.BlockCopy(buffer, offset, bytes, 0, count);
            var written = NativeLibrary.HandleExceptionOrReturnValue(NativeWrite(_handle, bytes, count));
            if (written != count) throw new IOException("Unable to write the entire buffer.");
        }

        public override bool CanRead => !_disposed && _canRead;

        public override bool CanSeek => !_disposed;


        public override bool CanWrite => !_disposed && _canWrite;

        private long GetLength()
        {
            var current = Seek(0, SeekOrigin.Current);
            var size = Seek(0, SeekOrigin.End);
            Seek(current, SeekOrigin.Begin);
            return size;
        }

        public override long Length => GetLength();

        public override long Position
        {
            get => Seek(0, SeekOrigin.Current);
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
                Seek(value, SeekOrigin.Begin);
            }
        }
    }

    public class C11BindingsTests
    {
        private const string TestText = "The quick brown fox jumps over the lazy dog";

        [Fact]
        public void ShortRead_ShouldSucceed()
        {
            var ms = new MemoryStream();
            var data = Encoding.UTF8.GetBytes(TestText);
            ms.Write(data, 0, data.Length);
            // Rewind for reading:
            ms.Position = 0;
            const int shortRead = 10;
            Assert.True(shortRead < data.Length);
            var buffer = new byte[shortRead];

            var managedC11Stream = new ManagedC11Stream(ms);
            var ret = NativeLibraryTests.test_c11_stream_read(managedC11Stream, buffer, buffer.Length);
            Assert.Equal(shortRead, ret);
#if NET6_0_OR_GREATER
            Assert.Equal(buffer, data[..ret]);
#else
            Assert.Equal(buffer, data.Take(ret).ToArray());
#endif
        }

        [Fact]
        public void ExactRead_ShouldSucceed()
        {
            var ms = new MemoryStream();
            var data = Encoding.UTF8.GetBytes(TestText);
            ms.Write(data, 0, data.Length);
            // Rewind for reading:
            ms.Position = 0;
            var buffer = new byte[data.Length];

            var managedC11Stream = new ManagedC11Stream(ms);
            var ret = NativeLibraryTests.test_c11_stream_read(managedC11Stream, buffer, buffer.Length);
            Assert.Equal(data.Length, ret);
            Assert.Equal(buffer, data);
        }

        [Fact]
        public void LongRead_ShouldSucceed()
        {
            var ms = new MemoryStream();
            var data = Encoding.UTF8.GetBytes(TestText);
            ms.Write(data, 0, data.Length);
            // Rewind for reading:
            ms.Position = 0;
            var buffer = new byte[data.Length + 1];

            var managedC11Stream = new ManagedC11Stream(ms);
            var ret = NativeLibraryTests.test_c11_stream_read(managedC11Stream, buffer, buffer.Length);
            Assert.Equal(data.Length, ret);
#if NET6_0_OR_GREATER
            Assert.Equal(buffer[..ret], data);
#else
            Assert.Equal(buffer.Take(ret).ToArray(), data);
#endif
        }

        [Fact]
        public void ExactWrite_ShouldSucceed()
        {
            var ms = new MemoryStream();
            var data = Encoding.UTF8.GetBytes(TestText);

            var managedC11Stream = new ManagedC11Stream(ms);
            var ret = NativeLibraryTests.test_c11_stream_write(managedC11Stream, data, data.Length);
            Assert.Equal(data.Length, ret);
            var buffer = ms.ToArray();
            Assert.Equal(buffer, data);
        }

        [Fact]
        public void SetLength_ShouldTruncateOrExtendFile()
        {
            // Truncate
            var ms = new MemoryStream();
            {
                var managedC11Stream = new ManagedC11Stream(ms);
                var ret = NativeLibraryTests.test_c11_stream_trunc(managedC11Stream, 10);
                Assert.Equal(10, ret);
                Assert.Equal(10, ms.Length);
            }

            // Extend
            {
                var managedC11Stream = new ManagedC11Stream(ms);
                var ret = NativeLibraryTests.test_c11_stream_trunc(managedC11Stream, 20);
                Assert.Equal(20, ret);
                Assert.Equal(20, ms.Length);
            }
        }

        [Fact]
        public void Read_Write_Seek_WorkCorrectly()
        {
            var ms = new MemoryStream();
            var managedC11Stream = new ManagedC11Stream(ms);
            var data = Encoding.UTF8.GetBytes(TestText);
            var ret = NativeLibraryTests.test_c11_stream_write(managedC11Stream, data, data.Length);
            Assert.Equal(data.Length, ret);
            Assert.Equal(data.Length, managedC11Stream.Position);
            var ret2 = NativeLibraryTests.test_c11_stream_seek(managedC11Stream, 0, (int) SeekDirs.Begin);
            Assert.Equal(0, ret2);
            Assert.Equal(0, managedC11Stream.Position);
            var buffer = new byte[data.Length];
            var ret3 = NativeLibraryTests.test_c11_stream_read(managedC11Stream, buffer, buffer.Length);
            Assert.Equal(data.Length, ret3);
            Assert.Equal(data, buffer);
        }

        [Fact]
        public void Length_ReturnsUnderlyingFileLength()
        {
            var tempFilePath = Path.GetTempFileName();
            try
            {
                using (var tempFileStream =
                       new FileStream(tempFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    var managedC11Stream = new ManagedC11Stream(tempFileStream);
                    var data = Encoding.UTF8.GetBytes(TestText);
                    var ret1 = NativeLibraryTests.test_c11_stream_write(managedC11Stream, data, data.Length);
                    Assert.Equal(data.Length, ret1);
                    var length1 = new FileInfo(tempFilePath).Length;
                    Assert.Equal(0, length1);
                    // native flush:
                    var ret2 = NativeLibraryTests.test_c11_stream_flush(managedC11Stream);
                    Assert.Equal(0, ret2);
                    var length2 = new FileInfo(tempFilePath).Length;
                    Assert.Equal(data.Length, length2);
                }
            }
            finally
            {
                File.Delete(tempFilePath);
            }
        }

        [Fact]
        public void Length_ReturnsUnderlyingFileLengthMixed()
        {
            var tempFilePath = Path.GetTempFileName();
            try
            {
                using (var tempFileStream =
                       new FileStream(tempFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    var managedC11Stream = new ManagedC11Stream(tempFileStream);
                    var data = Encoding.UTF8.GetBytes(TestText);
                    var ret1 = NativeLibraryTests.test_c11_stream_write(managedC11Stream, data, data.Length);
                    Assert.Equal(data.Length, ret1);
                    var length1 = new FileInfo(tempFilePath).Length;
                    Assert.Equal(0, length1);
                    // c# flush:
                    managedC11Stream.Flush();
                    // no exception
                    var length2 = new FileInfo(tempFilePath).Length;
                    Assert.Equal(data.Length, length2);
                }
            }
            finally
            {
                File.Delete(tempFilePath);
            }
        }


        [Fact]
        public void Write_NullByteArray_ShouldThrowArgumentNullException()
        {
            using (var ms = new MemoryStream())
            {
#if NET6_0_OR_GREATER
                byte[]? data = null;
                Assert.Throws<ArgumentNullException>(() => ms.Write(data!, 0, 1));
#else
                byte[] data = null;
                Assert.Throws<ArgumentNullException>(() => ms.Write(data, 0, 1));
#endif

                var managedC11Stream = new ManagedC11Stream(ms);
#if NET6_0_OR_GREATER
                var ret = NativeLibraryTests.test_c11_stream_write(managedC11Stream, data!, 1);
#else
                var ret = NativeLibraryTests.test_c11_stream_write(managedC11Stream, data, 1);
#endif
                Assert.True(ret < 0);
                var hresult = (new ArgumentNullException()).HResult;
                Assert.Equal(NativeLibrary.ErrorCodeMap.ToErrorCode(hresult), ret);
            }
        }

        [Fact]
        public void Write_ToReadOnlyMemoryStream_ShouldThrowNotSupportedException()
        {
            using (var ms = new MemoryStream(new byte[10], writable: false))
            {
                var data = new byte[] {1, 2, 3};
                Assert.Throws<NotSupportedException>(() => ms.Write(data, 0, data.Length));

                var managedC11Stream = new ManagedC11Stream(ms);
                var ret = NativeLibraryTests.test_c11_stream_write(managedC11Stream, data, data.Length);
                Assert.True(ret < 0);
                var hresult = (new NotSupportedException()).HResult;
                Assert.Equal(NativeLibrary.ErrorCodeMap.ToErrorCode(hresult), ret);
            }
        }

        [Fact]
        public void Write_InvalidLength_ShouldThrowArgumentOutOfRangeException()
        {
            using (var ms = new MemoryStream(new byte[10], writable: false))
            {
                var data = new byte[] {1, 2, 3};
                Assert.Throws<ArgumentOutOfRangeException>(() => ms.Write(data, 0, -42));

                var managedC11Stream = new ManagedC11Stream(ms);
                var ret = NativeLibraryTests.test_c11_stream_write(managedC11Stream, data, -42);
                Assert.True(ret < 0);
                var hresult = (new ArgumentOutOfRangeException()).HResult;
                Assert.Equal(NativeLibrary.ErrorCodeMap.ToErrorCode(hresult), ret);
            }
        }

        [Fact]
        public void Write_OnClosedStream_ShouldThrowArgumentOutOfRangeException()
        {
            var ms = new MemoryStream(new byte[10], writable: true);
            var data = new byte[] {1, 2, 3};
            ms.Close();
            Assert.Throws<ObjectDisposedException>(() => ms.Write(data, 0, data.Length));

            var managedC11Stream = new ManagedC11Stream(ms);
            var ret = NativeLibraryTests.test_c11_stream_write(managedC11Stream, data, data.Length);
            Assert.True(ret < 0);
            var hresult = (new ObjectDisposedException(string.Empty)).HResult;
            Assert.Equal(NativeLibrary.ErrorCodeMap.ToErrorCode(hresult), ret);
        }

        [Fact]
        public void Truncate_OnFixedStream_ShouldThrowNotSupportedException()
        {
            var ms = new MemoryStream(new byte[10], writable: true);
            Assert.Throws<NotSupportedException>(() => ms.SetLength(4096));

            var managedC11Stream = new ManagedC11Stream(ms);
            var ret = NativeLibraryTests.test_c11_stream_trunc(managedC11Stream, 4096);
            Assert.True(ret < 0);
            var hresult = (new NotSupportedException()).HResult;
            Assert.Equal(NativeLibrary.ErrorCodeMap.ToErrorCode(hresult), ret);
        }
    }
}
