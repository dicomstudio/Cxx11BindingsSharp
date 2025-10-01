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
        internal static extern int test_c11_stream_trunc(IntPtr handle, long newSize);

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

        #region c11 file descriptor

        [DllImport(LibraryName, CallingConvention = CallConv, ExactSpelling = true)]
        internal static extern int c11_handle_stream_init(out IntPtr pHandle, IntPtr intPtr);

        [DllImport(LibraryName, CallingConvention = CallConv, ExactSpelling = true)]
        internal static extern int c11_fd_stream_init(out IntPtr pHandle, IntPtr intPtr);

        #endregion
    }

    public sealed class C11StreamHandle2 : SafeHandleMinusOneIsInvalid
    {
        public C11StreamHandle2(IntPtr intPtr) : base(ownsHandle: true)
        {
            var ret = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? NativeLibraryTests.c11_handle_stream_init(out handle, intPtr)
                : NativeLibraryTests.c11_fd_stream_init(out handle, intPtr);
            if (ret < 0)
            {
                throw new IOException($"Cannot handle file {intPtr}");
            }
        }

        protected override bool ReleaseHandle()
        {
            // NativeLibraryTests.c11_handle_stream_delete(handle)
            return true;
        }
    }

    public sealed class C11FileStream : Stream
    {
        private const FileShare DefaultShare = FileShare.Read;
        private readonly IntPtr _c11StreamPtr;

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
        public C11FileStream(string filename, FileMode fileMode, FileAccess fileAccess, FileShare fileShare)
        {
            var mode = ToFopenMode(fileMode, fileAccess, fileShare);

            var ret = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? NativeLibraryTests.c11_file_stream_create2(out _c11StreamPtr, filename, mode)
                : NativeLibraryTests.c11_file_stream_create1(out _c11StreamPtr, filename, mode);
            if (ret < 0)
            {
                throw new IOException($"Cannot open file: {filename} with mode: {mode}");
            }

            // now is a good time to compute CanRead/CanWrite/CanSeek:
            (CanRead, CanWrite, CanSeek) = ComputeCapabilities(fileMode, fileAccess, fileShare);
        }

        private static (bool canRead, bool canWrite, bool canSeek) ComputeCapabilities(FileMode mode, FileAccess access,
            FileShare share)
        {
            var canRead = access == FileAccess.Read || access == FileAccess.ReadWrite;
            var canWrite = access == FileAccess.Write || access == FileAccess.ReadWrite;
            // C11FileStream is always seekable unless wrapping a non-seekable handle (?)
            var canSeek = true;
            return (canRead, canWrite, canSeek);
        }

        public C11FileStream(string filename, FileMode fileMode, FileAccess fileAccess) : this(filename, fileMode,
            fileAccess, DefaultShare)
        {
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Dispose managed resources here
                var ret = NativeLibraryTests.c11_file_stream_destroy(_c11StreamPtr);
                if (ret < 0)
                {
                    throw new IOException("Cannot destroy file");
                }
            }

            // Dispose unmanaged resources here (if any)
            base.Dispose(disposing);
        }

        public override void Flush()
        {
            var ret = NativeLibraryTests.test_c11_stream_flush(_c11StreamPtr);
            NativeLibrary.HandleExceptionOrReturnValue(ret);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var ret = NativeLibraryTests.test_c11_stream_read(_c11StreamPtr, buffer, buffer.Length);
            return NativeLibrary.HandleExceptionOrReturnValue(ret);
        }


        public override long Seek(long offset, SeekOrigin origin)
        {
            var ret = NativeLibraryTests.test_c11_stream_seek(_c11StreamPtr, offset,
                ManagedC11Stream.ToSeekDirs(origin));
            return NativeLibrary.HandleExceptionOrReturnValue(ret);
        }

        public override void SetLength(long value)
        {
            var ret = NativeLibraryTests.test_c11_stream_trunc(_c11StreamPtr, value);
            NativeLibrary.HandleExceptionOrReturnValue(ret);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            var ret = NativeLibraryTests.test_c11_stream_write(_c11StreamPtr, buffer, buffer.Length);
            NativeLibrary.HandleExceptionOrReturnValue(ret);
        }

        public override bool CanRead { get; }

        public override bool CanSeek { get; }


        public override bool CanWrite { get; }

        private long GetLength()
        {
            var current = NativeLibraryTests.test_c11_stream_seek(_c11StreamPtr, 0, SeekDirs.Current);
            var size = NativeLibraryTests.test_c11_stream_seek(_c11StreamPtr, 0, SeekDirs.End);
            NativeLibraryTests.test_c11_stream_seek(_c11StreamPtr, current, SeekDirs.Begin);
            return size;
        }

        public override long Length => GetLength();

        public override long Position
        {
            get => NativeLibraryTests.test_c11_stream_seek(_c11StreamPtr, 0, SeekDirs.Current);
            set => NativeLibraryTests.test_c11_stream_seek(_c11StreamPtr, value, SeekDirs.Begin);
        }
    }

    public sealed class Cxx11FileStream : Stream
    {
        private const FileShare DefaultShare = FileShare.Read;
        private readonly IntPtr _cxx11StreamPtr;

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
            var mode = ToFopenMode(fileMode, fileAccess, fileShare);

            var ret = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? NativeLibraryTests.cxx11_file_stream_create2(out _cxx11StreamPtr, filename, mode)
                : NativeLibraryTests.cxx11_file_stream_create1(out _cxx11StreamPtr, filename, mode);
            if (ret < 0)
            {
                throw new IOException($"Cannot open file: {filename} with mode: {mode}");
            }

            // how is a good time to compute CanRead/CanWrite/CanSeek:
            (CanRead, CanWrite, CanSeek) = ComputeCapabilities(fileMode, fileAccess, fileShare);
        }

        private static (bool canRead, bool canWrite, bool canSeek) ComputeCapabilities(FileMode mode, FileAccess access,
            FileShare share)
        {
            var canRead = access == FileAccess.Read || access == FileAccess.ReadWrite;
            var canWrite = access == FileAccess.Write || access == FileAccess.ReadWrite;
            // C11FileStream is always seekable unless wrapping a non-seekable handle (?)
            var canSeek = true;
            return (canRead, canWrite, canSeek);
        }

        public Cxx11FileStream(string filename, FileMode fileMode, FileAccess fileAccess) : this(filename, fileMode,
            fileAccess, DefaultShare)
        {
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Dispose managed resources here
                var ret = NativeLibraryTests.cxx11_file_stream_destroy(_cxx11StreamPtr);
                if (ret < 0)
                {
                    throw new IOException("Cannot destroy file");
                }
            }

            // Dispose unmanaged resources here (if any)
            base.Dispose(disposing);
        }

        public override void Flush()
        {
            var ret = NativeLibraryTests.test_c11_stream_flush(_cxx11StreamPtr);
            NativeLibrary.HandleExceptionOrReturnValue(ret);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var ret = NativeLibraryTests.test_c11_stream_read(_cxx11StreamPtr, buffer, buffer.Length);
            return NativeLibrary.HandleExceptionOrReturnValue(ret);
        }


        public override long Seek(long offset, SeekOrigin origin)
        {
            var ret = NativeLibraryTests.test_c11_stream_seek(_cxx11StreamPtr, offset,
                ManagedC11Stream.ToSeekDirs(origin));
            return NativeLibrary.HandleExceptionOrReturnValue(ret);
        }

        public override void SetLength(long value)
        {
            var ret = NativeLibraryTests.test_c11_stream_trunc(_cxx11StreamPtr, value);
            NativeLibrary.HandleExceptionOrReturnValue(ret);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            var ret = NativeLibraryTests.test_c11_stream_write(_cxx11StreamPtr, buffer, buffer.Length);
            NativeLibrary.HandleExceptionOrReturnValue(ret);
        }

        public override bool CanRead { get; }

        public override bool CanSeek { get; }


        public override bool CanWrite { get; }

        private long GetLength()
        {
            var current = NativeLibraryTests.test_c11_stream_seek(_cxx11StreamPtr, 0, SeekDirs.Current);
            var size = NativeLibraryTests.test_c11_stream_seek(_cxx11StreamPtr, 0, SeekDirs.End);
            NativeLibraryTests.test_c11_stream_seek(_cxx11StreamPtr, current, SeekDirs.Begin);
            return size;
        }

        public override long Length => GetLength();

        public override long Position
        {
            get => NativeLibraryTests.test_c11_stream_seek(_cxx11StreamPtr, 0, SeekDirs.Current);
            set => NativeLibraryTests.test_c11_stream_seek(_cxx11StreamPtr, value, SeekDirs.Begin);
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

        [Fact]
        public void Toto()
        {
            var data = Encoding.UTF8.GetBytes(TestText);
            string path = "example.txt";
            File.WriteAllText(path, TestText);
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                // Use fs to read from the file
                SafeFileHandle sfh = fs.SafeFileHandle;
                IntPtr handle = sfh.DangerousGetHandle();
                var h = new C11StreamHandle2(handle);
                var h2 = h.DangerousGetHandle();
                var buffer = new byte[512];
                var ret = NativeLibraryTests.test_c11_stream_read(h2, buffer, buffer.Length);
                Assert.Equal(data.Length, ret);
#if NET6_0_OR_GREATER
                Assert.Equal(data, buffer[..ret]);
#else
                Assert.Equal(data, buffer.Take(ret).ToArray());
#endif
            }
        }
    }
}
