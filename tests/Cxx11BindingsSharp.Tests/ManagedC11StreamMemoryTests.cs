using System;
using System.IO;
using System.Text;
using Xunit;

namespace Cxx11BindingsSharp.Tests
{
    public class ManagedC11StreamMemoryTests
    {
        [Fact]
        public void Constructor_NullStream_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ManagedC11Stream(null));
        }

        [Fact]
        public void CanReadWriteSeek_ReflectsUnderlyingStream()
        {
            var ms = new MemoryStream();
            var wrap = new ManagedC11Stream(ms);
            Assert.True(wrap.CanRead);
            Assert.True(wrap.CanWrite);
            Assert.True(wrap.CanSeek);
        }

        [Fact]
        public void Position_SetAndGet_Works()
        {
            var ms = new MemoryStream(new byte[100]);
            var wrap = new ManagedC11Stream(ms);
            wrap.Position = 10;
            Assert.Equal(10, wrap.Position);
        }

        [Fact]
        public void Length_ReturnsUnderlyingLength()
        {
            var ms = new MemoryStream(new byte[42]);
            var wrap = new ManagedC11Stream(ms);
            Assert.Equal(42, wrap.Length);
        }

        [Fact]
        public void SetLength_ChangesStreamLength()
        {
            var ms = new MemoryStream();
            var wrap = new ManagedC11Stream(ms);
            wrap.SetLength(123);
            Assert.Equal(123, ms.Length);
        }

        [Fact]
        public void Flush_DelegatesToStream()
        {
            var ms = new MemoryStream();
            var wrap = new ManagedC11Stream(ms);
            wrap.Flush();
            // No exception means success
        }

        [Fact]
        public void Read_Write_Seek_WorkCorrectly()
        {
            var ms = new MemoryStream();
            var wrap = new ManagedC11Stream(ms);
            var data = Encoding.UTF8.GetBytes("abc");
            wrap.Write(data, 0, data.Length);
            Assert.Equal(data.Length, wrap.Position);
            wrap.Position = 0;
            var buffer = new byte[3];
            int read = wrap.Read(buffer, 0, 3);
            Assert.Equal(3, read);
            Assert.Equal(data, buffer);
        }
    }
}
