using System;
using Xunit;

namespace Cxx11BindingsSharp.Tests
{
    public class C11StreamHandleTests
    {
        [Fact]
        public void NullFunction_ShouldReturnNotSupportedException()
        {
            var c11Stream = new C11StreamHandle(null, null, null, null, null);
            var hresult = (new NotSupportedException()).HResult;
            var buffer = new byte[4096];
            var ret = NativeLibraryTests.c11_stream_read(c11Stream, buffer, buffer.Length);
            Assert.True(ret < 0);
            Assert.Equal(hresult, ret);
            ret = NativeLibraryTests.c11_stream_write(c11Stream, buffer, buffer.Length);
            Assert.Equal(hresult, ret);
            var ret2 = NativeLibraryTests.c11_stream_seek(c11Stream, 0, SeekDirs.Begin);
            Assert.True(ret2 < 0, $"Expected negative return value, got {ret2}");
            Assert.Equal(hresult, ret2);
            ret = NativeLibraryTests.c11_stream_flush(c11Stream);
            Assert.Equal(hresult, ret);
            ret = NativeLibraryTests.c11_stream_trunc(c11Stream, 4096);
            Assert.Equal(hresult, ret);
        }
    }
}
