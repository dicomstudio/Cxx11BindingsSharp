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
            Assert.Throws<ArgumentNullException>(() => new ManagedC11Stream(null, true));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Dispose_RespectsUnderlyingStreamOwnership(bool leaveOpen)
        {
            var inner = new MemoryStream();
            var wrap = new ManagedC11Stream(inner, leaveOpen);
            C11StreamHandle handle = wrap;

            wrap.Dispose();
            wrap.Dispose();

            Assert.True(handle.IsClosed);
            Assert.Throws<ObjectDisposedException>(() => NativeLibraryTests.test_c11_stream_flush(handle));
            Assert.Equal(leaveOpen, inner.CanWrite);
            if (leaveOpen)
            {
                inner.WriteByte(42);
                Assert.Equal(1, inner.Length);
            }
            inner.Dispose();
        }

        [Fact]
        public void Dispose_DefaultClosesUnderlyingStream()
        {
            var inner = new MemoryStream();
            new ManagedC11Stream(inner).Dispose();
            Assert.False(inner.CanWrite);
        }

        [Fact]
        public void Dispose_LeaveOpenDisablesWrapperOperations()
        {
            using (var inner = new MemoryStream())
            {
                var wrap = new ManagedC11Stream(inner, true);
                wrap.Dispose();

                Assert.False(wrap.CanRead);
                Assert.False(wrap.CanWrite);
                Assert.False(wrap.CanSeek);
                Assert.Throws<ObjectDisposedException>(() => wrap.Read(new byte[1], 0, 1));
                Assert.Throws<ObjectDisposedException>(() => wrap.Write(new byte[1], 0, 1));
                Assert.Throws<ObjectDisposedException>(() => wrap.Flush());
                Assert.Throws<ObjectDisposedException>(() => wrap.Seek(0, SeekOrigin.Begin));
                Assert.Throws<ObjectDisposedException>(() => wrap.SetLength(0));
                Assert.Throws<ObjectDisposedException>(() => wrap.Length);
                Assert.Throws<ObjectDisposedException>(() => wrap.Position);
                Assert.Throws<ObjectDisposedException>(() => wrap.Position = 0);
                Assert.True(inner.CanRead);
            }
        }

        [Fact]
        public void Dispose_ClosesHandleWhenUnderlyingDisposeThrows()
        {
            var inner = new ThrowingDisposeStream();
            var wrap = new ManagedC11Stream(inner);
            C11StreamHandle handle = wrap;

            Assert.Throws<IOException>(() => wrap.Dispose());
            Assert.True(handle.IsClosed);
            Assert.False(wrap.CanRead);
            wrap.Dispose();
        }

        private sealed class ThrowingDisposeStream : MemoryStream
        {
            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                if (disposing) throw new IOException("Dispose failed");
            }
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
            var read = wrap.Read(buffer, 0, 3);
            Assert.Equal(3, read);
            Assert.Equal(data, buffer);
        }
    }
}
