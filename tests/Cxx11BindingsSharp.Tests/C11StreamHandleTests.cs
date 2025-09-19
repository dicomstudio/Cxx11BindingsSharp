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
            var ret = NativeLibraryTests.test_c11_stream_read(c11Stream, buffer, buffer.Length);
            Assert.True(ret < 0);
            Assert.Equal(hresult, NativeLibrary.ErrorCodeMap.ToHResult(ret));
            ret = NativeLibraryTests.test_c11_stream_write(c11Stream, buffer, buffer.Length);
            Assert.Equal(hresult, NativeLibrary.ErrorCodeMap.ToHResult(ret));
            var ret2 = NativeLibraryTests.test_c11_stream_seek(c11Stream, 0, SeekDirs.Begin);
            Assert.True(ret2 < 0, $"Expected negative return value, got {ret2}");
            Assert.Equal(hresult, NativeLibrary.ErrorCodeMap.ToHResult((int)ret2));
            ret = NativeLibraryTests.test_c11_stream_flush(c11Stream);
            Assert.Equal(hresult, NativeLibrary.ErrorCodeMap.ToHResult(ret));
            ret = NativeLibraryTests.test_c11_stream_trunc(c11Stream, 4096);
            Assert.Equal(hresult, NativeLibrary.ErrorCodeMap.ToHResult(ret));
        }
    }
}
