using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Xunit;

namespace Cxx11BindingsSharp.Tests
{
    public static class NativeLibraryTests
    {
        #region c11 stream

        private const string LibraryName = "Cxx11BindingsTests";
        private const CallingConvention CallConv = CallingConvention.Cdecl;

        [DllImport(LibraryName, CallingConvention = CallConv)]
        internal static extern int c11_stream_read(C11StreamBufHandle handle, [Out] byte[] buffer, int length);

        [DllImport(LibraryName, CallingConvention = CallConv)]
        internal static extern int c11_stream_write(C11StreamBufHandle handle, [In] byte[] buffer, int length);

        [DllImport(LibraryName, CallingConvention = CallConv)]
        internal static extern long c11_stream_seek(C11StreamBufHandle handle, long offset, SeekOrigin dir);

        [DllImport(LibraryName, CallingConvention = CallConv)]
        internal static extern int c11_stream_flush(C11StreamBufHandle handle);

        [DllImport(LibraryName, CallingConvention = CallConv)]
        internal static extern int c11_stream_trunc(C11StreamBufHandle handle, long size);

        #endregion
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
            var ret = NativeLibraryTests.c11_stream_read(managedC11Stream, buffer, buffer.Length);
            Assert.Equal(shortRead, ret);
            Assert.Equal(buffer, data[..ret]);
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
            var ret = NativeLibraryTests.c11_stream_read(managedC11Stream, buffer, buffer.Length);
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
            var ret = NativeLibraryTests.c11_stream_read(managedC11Stream, buffer, buffer.Length);
            Assert.Equal(data.Length, ret);
            Assert.Equal(buffer[..ret], data);
        }

        [Fact]
        public void ExactWrite_ShouldSucceed()
        {
            var ms = new MemoryStream();
            var data = Encoding.UTF8.GetBytes(TestText);

            var managedC11Stream = new ManagedC11Stream(ms);
            var ret = NativeLibraryTests.c11_stream_write(managedC11Stream, data, data.Length);
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
                var ret = NativeLibraryTests.c11_stream_trunc(managedC11Stream, 10);
                Assert.Equal(10, ret);
                Assert.Equal(10, ms.Length);
            }

            // Extend
            {
                var managedC11Stream = new ManagedC11Stream(ms);
                var ret = NativeLibraryTests.c11_stream_trunc(managedC11Stream, 20);
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
            var ret = NativeLibraryTests.c11_stream_write(managedC11Stream, data, data.Length);
            Assert.Equal(data.Length, ret);
            Assert.Equal(data.Length, managedC11Stream.Position);
            var ret2 = NativeLibraryTests.c11_stream_seek(managedC11Stream, 0, SeekOrigin.Begin);
            Assert.Equal(0, ret2);
            Assert.Equal(0, managedC11Stream.Position);
            var buffer = new byte[data.Length];
            var ret3 = NativeLibraryTests.c11_stream_read(managedC11Stream, buffer, buffer.Length);
            Assert.Equal(data.Length, ret3);
            Assert.Equal(data, buffer);
        }

        [Fact]
        public void Length_ReturnsUnderlyingFileLength()
        {
            var tempFilePath = Path.GetTempFileName();
            try
            {
                using var tempFileStream =
                    new FileStream(tempFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                var managedC11Stream = new ManagedC11Stream(tempFileStream);
                var data = Encoding.UTF8.GetBytes(TestText);
                var ret1 = NativeLibraryTests.c11_stream_write(managedC11Stream, data, data.Length);
                Assert.Equal(data.Length, ret1);
                var length1 = new FileInfo(tempFilePath).Length;
                Assert.Equal(0, length1);
                // native flush:
                var ret2 = NativeLibraryTests.c11_stream_flush(managedC11Stream);
                Assert.Equal(0, ret2);
                var length2 = new FileInfo(tempFilePath).Length;
                Assert.Equal(data.Length, length2);
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
                using var tempFileStream =
                    new FileStream(tempFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                var managedC11Stream = new ManagedC11Stream(tempFileStream);
                var data = Encoding.UTF8.GetBytes(TestText);
                var ret1 = NativeLibraryTests.c11_stream_write(managedC11Stream, data, data.Length);
                Assert.Equal(data.Length, ret1);
                var length1 = new FileInfo(tempFilePath).Length;
                Assert.Equal(0, length1);
                // c# flush:
                managedC11Stream.Flush();
                // no exception
                var length2 = new FileInfo(tempFilePath).Length;
                Assert.Equal(data.Length, length2);
            }
            finally
            {
                File.Delete(tempFilePath);
            }
        }
    }
}
