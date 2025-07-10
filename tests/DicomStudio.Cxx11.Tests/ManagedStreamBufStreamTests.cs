using System;
using System.IO;
using System.Text;
using Xunit;

namespace DicomStudio.Cxx11.Tests
{
    public class ManagedStreamBufStreamTests
    {
        [Fact]
        public void Constructor_NullStream_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ManagedStreamBufStream(null));
        }

        [Fact]
        public void CanReadWriteSeek_ReflectsUnderlyingStream()
        {
            var ms = new MemoryStream();
            var wrap = new ManagedStreamBufStream(ms);
            Assert.True(wrap.CanRead);
            Assert.True(wrap.CanWrite);
            Assert.True(wrap.CanSeek);
        }

        [Fact]
        public void Position_SetAndGet_Works()
        {
            var ms = new MemoryStream(new byte[100]);
            var wrap = new ManagedStreamBufStream(ms);
            wrap.Position = 10;
            Assert.Equal(10, wrap.Position);
        }

        [Fact]
        public void Length_ReturnsUnderlyingLength()
        {
            var ms = new MemoryStream(new byte[42]);
            var wrap = new ManagedStreamBufStream(ms);
            Assert.Equal(42, wrap.Length);
        }

        [Fact]
        public void SetLength_ChangesStreamLength()
        {
            var ms = new MemoryStream();
            var wrap = new ManagedStreamBufStream(ms);
            wrap.SetLength(123);
            Assert.Equal(123, ms.Length);
        }

        [Fact]
        public void Flush_DelegatesToStream()
        {
            var ms = new MemoryStream();
            var wrap = new ManagedStreamBufStream(ms);
            wrap.Flush();
            // No exception means success
        }

        [Fact]
        public void Read_Write_Seek_WorkCorrectly()
        {
            var ms = new MemoryStream();
            var wrap = new ManagedStreamBufStream(ms);
            var data = Encoding.UTF8.GetBytes("abc");
            wrap.Write(data, 0, data.Length);
            wrap.Position = 0;
            var buffer = new byte[3];
            int read = wrap.Read(buffer, 0, 3);
            Assert.Equal(3, read);
            Assert.Equal(data, buffer);
        }

        [Fact]
        public void Length_ReturnsUnderlyingLength2()
        {
            var ms = new MemoryStream(new byte[42]);
            var wrap = new ManagedStreamBufStream(ms);
            var ret = NativeLibrary.cxx11_managed_streambuf_test_size(wrap);
            Assert.Equal(42, ret);
        }
        [Fact]
        public void Length_ReturnsUnderlyingLength4()
        {
            var fs = File.OpenWrite("dest2.txt");
            var wrap = new ManagedStreamBufStream(fs);
            var data = Encoding.UTF8.GetBytes("abcd");
            var ret = NativeLibrary.cxx11_managed_streambuf_write_into(wrap, data, data.Length);
            wrap.Flush();
            Assert.Equal(data.Length, ret);
        }

    }
}
