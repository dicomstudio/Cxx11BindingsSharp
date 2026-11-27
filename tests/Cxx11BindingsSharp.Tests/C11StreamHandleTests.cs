using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;

namespace Cxx11BindingsSharp.Tests
{
    public class C11StreamHandleTests
    {
        [DllImport("Cxx11BindingsTests", EntryPoint = "test_c11_stream_read", CallingConvention = CallingConvention.Cdecl)]
        private static extern int ReadRaw(C11StreamHandle stream, IntPtr buffer, int count);

        [DllImport("Cxx11BindingsTests", EntryPoint = "test_c11_stream_write", CallingConvention = CallingConvention.Cdecl)]
        private static extern int WriteRaw(C11StreamHandle stream, IntPtr buffer, int count);

        [Fact]
        public void ManagedStructMatchesNativePointerLayout()
        {
            Assert.Equal(5 * IntPtr.Size, Marshal.SizeOf(typeof(C11Stream)));
            Assert.Equal(0 * IntPtr.Size, Marshal.OffsetOf(typeof(C11Stream), "read").ToInt32());
            Assert.Equal(1 * IntPtr.Size, Marshal.OffsetOf(typeof(C11Stream), "write").ToInt32());
            Assert.Equal(2 * IntPtr.Size, Marshal.OffsetOf(typeof(C11Stream), "seek").ToInt32());
            Assert.Equal(3 * IntPtr.Size, Marshal.OffsetOf(typeof(C11Stream), "flush").ToInt32());
            Assert.Equal(4 * IntPtr.Size, Marshal.OffsetOf(typeof(C11Stream), "trunc").ToInt32());
        }

        [Fact]
        public void NullNativeBuffersReturnPointerError()
        {
            using (var stream = new C11StreamHandle((self, buffer, count) => 0,
                       (self, buffer, count) => 0, null, null, null))
            {
                Assert.Equal((int) ErrorCodes.C11_E_POINTER, ReadRaw(stream, IntPtr.Zero, 1));
                Assert.Equal((int) ErrorCodes.C11_E_POINTER, WriteRaw(stream, IntPtr.Zero, 1));
                Assert.Equal(0, ReadRaw(stream, IntPtr.Zero, 0));
                Assert.Equal(0, WriteRaw(stream, IntPtr.Zero, 0));
            }
        }

        [Fact]
        public void ThrownCallbackExceptionsAreContainedAtNativeBoundary()
        {
            using (var stream = new C11StreamHandle(
                       (self, buffer, count) => throw new InvalidOperationException("read failure"),
                       (self, buffer, count) => throw new IOException(),
                       (self, offset, origin) => throw new ArgumentOutOfRangeException(),
                       self => throw new NotSupportedException(),
                       (self, size) => throw new ObjectDisposedException("stream")))
            {
                var buffer = new byte[1];
                Assert.Equal((int) ErrorCodes.C11_E_INVALIDOPERATION, NativeLibraryTests.test_c11_stream_read(stream, buffer, 1));
                Assert.Equal((int) ErrorCodes.C11_E_IO, NativeLibraryTests.test_c11_stream_write(stream, buffer, 1));
                Assert.Equal((long) ErrorCodes.C11_E_ARGUMENTOUTOFRANGE, NativeLibraryTests.test_c11_stream_seek(stream, 0, SeekDirs.Begin));
                Assert.Equal((int) ErrorCodes.C11_E_NOTSUPPORTED, NativeLibraryTests.test_c11_stream_flush(stream));
                Assert.Equal((long) ErrorCodes.C11_E_OBJECTDISPOSED, NativeLibraryTests.test_c11_stream_trunc(stream, 1));
            }
        }

            public static IEnumerable<object[]> MappedExceptions()
            {
                yield return new object[] { new NullReferenceException(), ErrorCodes.C11_E_POINTER };
                yield return new object[] { new NotSupportedException(), ErrorCodes.C11_E_NOTSUPPORTED };
                yield return new object[] { new ArgumentException(), ErrorCodes.C11_E_INVALIDARG };
                yield return new object[] { new ArgumentOutOfRangeException(), ErrorCodes.C11_E_ARGUMENTOUTOFRANGE };
                yield return new object[] { new ObjectDisposedException("stream"), ErrorCodes.C11_E_OBJECTDISPOSED };
                yield return new object[] { new IOException(), ErrorCodes.C11_E_IO };
                yield return new object[] { new InvalidOperationException(), ErrorCodes.C11_E_INVALIDOPERATION };
                yield return new object[] { new UnauthorizedAccessException(), ErrorCodes.C11_E_UNAUTHORIZED };
                yield return new object[] { new OutOfMemoryException(), ErrorCodes.C11_E_OUTOFMEMORY };
                yield return new object[] { new TimeoutException(), ErrorCodes.C11_E_TIMEOUT };
                yield return new object[] { new OperationCanceledException(), ErrorCodes.C11_E_CANCELED };
                yield return new object[] { new FileNotFoundException(), ErrorCodes.C11_E_FILENOTFOUND };
                yield return new object[] { new DirectoryNotFoundException(), ErrorCodes.C11_E_DIRECTORYNOTFOUND };
                yield return new object[] { new PathTooLongException(), ErrorCodes.C11_E_PATHTOOLONG };
                yield return new object[] { new EndOfStreamException(), ErrorCodes.C11_E_ENDOFSTREAM };
                yield return new object[] { new ArgumentNullException(), ErrorCodes.C11_E_ARGUMENTNULL };
                yield return new object[] { new NotImplementedException(), ErrorCodes.C11_E_NOTIMPLEMENTED };
            }

            [Theory]
            [MemberData(nameof(MappedExceptions))]
            public void ExceptionMappingRoundTripsThroughNativeCallback(Exception exception, object codeValue)
            {
                var code = (ErrorCodes) codeValue;
                Assert.Equal((int) code, NativeLibrary.ErrorCodeMap.ToErrorCode(exception));
                Assert.Equal(exception.HResult, NativeLibrary.ErrorCodeMap.ToHResult((int) code));
                var roundTrip = Record.Exception(() => NativeLibrary.HandleExceptionOrReturnValue((int) code));
                Assert.NotNull(roundTrip);
                Assert.Equal(exception.GetType(), roundTrip.GetType());
                using (var stream = new C11StreamHandle(null, null, null, self => throw exception, null))
                    Assert.Equal((int) code, NativeLibraryTests.test_c11_stream_flush(stream));
            }

            [Fact]
            public void UnmappedHResultIsDistinctFromIoError()
            {
                const int hresult = unchecked((int) 0x81234567);
                Assert.Equal((int) ErrorCodes.C11_E_UNMAPPED, NativeLibrary.ErrorCodeMap.ToErrorCode(hresult));
                using (var stream = new C11StreamHandle(null, null, null,
                           self => throw new ExternalException("unmapped", hresult), null))
                    Assert.Equal((int) ErrorCodes.C11_E_UNMAPPED, NativeLibraryTests.test_c11_stream_flush(stream));
            }
        [Fact]
        public void TruncateReturns64BitLength()
        {
            const long length = (long) int.MaxValue + 123;
            using (var stream = new C11StreamHandle(null, null, null, null, (self, size) => size))
                Assert.Equal(length, NativeLibraryTests.test_c11_stream_trunc(stream, length));
        }

        [Fact]
        public void NullFunction_ShouldReturnNotSupportedException()
        {
            using (var c11Stream = new C11StreamHandle(null, null, null, null, null))
            {
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
                ret2 = NativeLibraryTests.test_c11_stream_trunc(c11Stream, 4096);
                Assert.Equal(hresult, NativeLibrary.ErrorCodeMap.ToHResult((int)ret2));
            }
        }

        [Fact]
        public void NullNativeStreamReturnsPointerError()
        {
            var data = new byte[1];
            Assert.Equal((int) ErrorCodes.C11_E_POINTER, NativeLibraryTests.test_c11_stream_read(IntPtr.Zero, data, 1));
            Assert.Equal((int) ErrorCodes.C11_E_POINTER, NativeLibraryTests.test_c11_stream_write(IntPtr.Zero, data, 1));
            Assert.Equal((long) ErrorCodes.C11_E_POINTER, NativeLibraryTests.test_c11_stream_seek(IntPtr.Zero, 0, SeekDirs.Begin));
            Assert.Equal((int) ErrorCodes.C11_E_POINTER, NativeLibraryTests.test_c11_stream_flush(IntPtr.Zero));
            Assert.Equal((long) ErrorCodes.C11_E_POINTER, NativeLibraryTests.test_c11_stream_trunc(IntPtr.Zero, 0));
        }

        [Fact]
        public void DisposingHandleIsIdempotent()
        {
            var stream = new C11StreamHandle(null, null, null, null, null);
            Assert.False(stream.IsInvalid);
            Assert.False(stream.IsClosed);
            stream.Dispose();
            stream.Dispose();
            Assert.True(stream.IsClosed);
        }

        [Fact]
        public void DisposedHandleCannotBePassedToNativeCode()
        {
            var stream = new C11StreamHandle(null, null, null, null, null);
            stream.Dispose();
            Assert.Throws<ObjectDisposedException>(() => NativeLibraryTests.test_c11_stream_flush(stream));
        }

        [Fact]
        public void DisposedManagedStreamCannotBePassedToNativeCode()
        {
            var stream = new ManagedC11Stream(new MemoryStream());
            stream.Dispose();
            Assert.Throws<ObjectDisposedException>(() => NativeLibraryTests.test_c11_stream_flush(stream));
        }

        [Fact]
        public void NativeCallKeepsHandleAliveWhenDisposedDuringCallback()
        {
            var holder = new C11StreamHandle[1];
            var nestedResult = -1;
            var flushes = 0;
            holder[0] = new C11StreamHandle((self, buffer, count) =>
            {
                holder[0].Dispose();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                nestedResult = NativeLibraryTests.test_c11_stream_flush(self);
                buffer[0] = 42;
                return 1;
            }, null, null, self => { flushes++; return 0; }, null);

            var destination = new byte[1];
            Assert.Equal(1, NativeLibraryTests.test_c11_stream_read(holder[0], destination, 1));
            Assert.Equal(0, nestedResult);
            Assert.Equal(1, flushes);
            Assert.Equal(42, destination[0]);
            Assert.True(holder[0].IsClosed);
        }

        [Fact]
        public void ReadCallback_PreservesUnwrittenBytesOnShortReadAndEof()
        {
            var calls = 0;
            using (var stream = new C11StreamHandle((self, buffer, count) =>
                   {
                       Assert.NotEqual(IntPtr.Zero, self);
                       Assert.Equal(5, count);
                       if (calls++ > 0) return 0;
                       buffer[0] = 10;
                       buffer[1] = 20;
                       return 2;
                   }, null, null, null, null, 1))
            {
                var destination = new byte[] { 9, 9, 9, 9, 9 };
                Assert.Equal(2, NativeLibraryTests.test_c11_stream_read(stream, destination, 5));
                Assert.Equal(new byte[] { 10, 20, 9, 9, 9 }, destination);
                Assert.Equal(0, NativeLibraryTests.test_c11_stream_read(stream, destination, 5));
                Assert.Equal(new byte[] { 10, 20, 9, 9, 9 }, destination);
            }
        }

        [Fact]
        public void ReadCallback_PreservesBufferAcrossNestedWrite()
        {
            var nestedResult = 0;
            using (var stream = new C11StreamHandle((self, buffer, count) =>
                   {
                       buffer[0] = 42;
                       nestedResult = NativeLibraryTests.test_c11_stream_write(self, new byte[] { 99 }, 1);
                       return 1;
                   }, (self, buffer, count) => count, null, null, null))
            {
                var destination = new byte[1];
                Assert.Equal(1, NativeLibraryTests.test_c11_stream_read(stream, destination, 1));
                Assert.Equal(1, nestedResult);
                Assert.Equal(42, destination[0]);
            }
        }

        [Fact]
        public void WriteCallback_PreservesBufferAcrossNestedRead()
        {
            var nestedResult = 0;
            byte observed = 0;
            using (var stream = new C11StreamHandle((self, buffer, count) =>
                   {
                       buffer[0] = 99;
                       return 1;
                   }, (self, buffer, count) =>
                   {
                       nestedResult = NativeLibraryTests.test_c11_stream_read(self, new byte[1], 1);
                       observed = buffer[0];
                       return 1;
                   }, null, null, null))
            {
                Assert.Equal(1, NativeLibraryTests.test_c11_stream_write(stream, new byte[] { 42 }, 1));
                Assert.Equal(1, nestedResult);
                Assert.Equal(42, observed);
            }
        }

        [Fact]
        public void ReadCallback_NegativeResultDoesNotModifyDestination()
        {
            using (var stream = new C11StreamHandle((self, buffer, count) =>
                   {
                       buffer[0] = 42;
                       return (int) ErrorCodes.C11_E_IO;
                   }, null, null, null, null))
            {
                var destination = new byte[] { 1, 2, 3 };
                Assert.Equal((int) ErrorCodes.C11_E_IO,
                    NativeLibraryTests.test_c11_stream_read(stream, destination, destination.Length));
                Assert.Equal(new byte[] { 1, 2, 3 }, destination);
            }
        }

        [Fact]
        public void WriteCallback_ReceivesRequestedBytesAndReturnsPartialProgress()
        {
            using (var stream = new C11StreamHandle(null, (self, buffer, count) =>
                   {
                       Assert.NotEqual(IntPtr.Zero, self);
                       Assert.Equal(3, count);
                       Assert.Equal(new byte[] { 3, 5, 7 }, new[] { buffer[0], buffer[1], buffer[2] });
                       return 2;
                   }, null, null, null, 1))
            {
                Assert.Equal(2, NativeLibraryTests.test_c11_stream_write(stream, new byte[] { 3, 5, 7, 9 }, 3));
            }
        }

#if NET6_0_OR_GREATER
        [Theory]
        [InlineData(true, false)]
        [InlineData(true, true)]
        [InlineData(false, false)]
        [InlineData(false, true)]
        public void BufferCallbacks_ClearEntireRentedArray(bool isRead, bool throws)
        {
            byte[]? callbackBuffer = null;
            using (var stream = new C11StreamHandle((self, buffer, count) =>
                   {
                       callbackBuffer = buffer;
                       Array.Fill(buffer, (byte) 42);
                       if (throws) throw new IOException();
                       return count;
                   }, (self, buffer, count) =>
                   {
                       callbackBuffer = buffer;
                       Array.Fill(buffer, (byte) 42);
                       if (throws) throw new IOException();
                       return count;
                   }, null, null, null))
            {
                var data = new byte[] { 1 };
                var result = isRead
                    ? NativeLibraryTests.test_c11_stream_read(stream, data, data.Length)
                    : NativeLibraryTests.test_c11_stream_write(stream, data, data.Length);

                Assert.Equal(throws ? (int) ErrorCodes.C11_E_IO : 1, result);
                Assert.NotNull(callbackBuffer);
                Assert.All(callbackBuffer, value => Assert.Equal(0, value));
            }
        }
#endif

        [Fact]
        public void ReadAndWriteCallbacks_RejectImpossibleCounts()
        {
            using (var stream = new C11StreamHandle((self, buffer, count) => count + 1,
                       (self, buffer, count) => count + 1, null, null, null))
            {
                var buffer = new byte[] { 1, 2 };
                Assert.Equal((int) ErrorCodes.C11_E_IO, NativeLibraryTests.test_c11_stream_read(stream, buffer, 2));
                Assert.Equal(new byte[] { 1, 2 }, buffer);
                Assert.Equal((int) ErrorCodes.C11_E_IO, NativeLibraryTests.test_c11_stream_write(stream, buffer, 2));
                Assert.Equal((int) ErrorCodes.C11_E_ARGUMENTOUTOFRANGE, NativeLibraryTests.test_c11_stream_read(stream, buffer, -1));
                Assert.Equal((int) ErrorCodes.C11_E_ARGUMENTOUTOFRANGE, NativeLibraryTests.test_c11_stream_write(stream, buffer, -1));
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void BufferCallbacks_TranslateIOException(bool isRead)
        {
            NativeLibrary.ReadDelegate read = (self, buffer, count) => throw new IOException();
            NativeLibrary.WriteDelegate write = (self, buffer, count) => throw new IOException();
            using (var stream = new C11StreamHandle(read, write, null, null, null))
            {
                var data = new byte[1];
                var result = isRead
                    ? NativeLibraryTests.test_c11_stream_read(stream, data, 1)
                    : NativeLibraryTests.test_c11_stream_write(stream, data, 1);
                Assert.Equal((int) ErrorCodes.C11_E_IO, result);
            }
        }

        [Fact]
        public void ZeroLengthReadAndWriteReachCallbacks()
        {
            var reads = 0;
            var writes = 0;
            using (var stream = new C11StreamHandle(
                       (self, buffer, count) => { reads++; Assert.Equal(0, count); return 0; },
                       (self, buffer, count) => { writes++; Assert.Equal(0, count); return 0; },
                       null, null, null))
            {
                Assert.Equal(0, NativeLibraryTests.test_c11_stream_read(stream, new byte[1], 0));
                Assert.Equal(0, NativeLibraryTests.test_c11_stream_write(stream, new byte[1], 0));
                Assert.Equal(1, reads);
                Assert.Equal(1, writes);
            }
        }

        [Fact]
        public void CallbackErrorCodesPassThroughNativeAbi()
        {
            using (var stream = new C11StreamHandle(
                       (self, buffer, count) => (int) ErrorCodes.C11_E_NOTSUPPORTED,
                       (self, buffer, count) => (int) ErrorCodes.C11_E_IO,
                       (self, offset, origin) => (int) ErrorCodes.C11_E_INVALIDARG,
                       self => (int) ErrorCodes.C11_E_OBJECTDISPOSED,
                       (self, size) => (int) ErrorCodes.C11_E_ARGUMENTOUTOFRANGE))
            {
                var data = new byte[1];
                Assert.Equal((int) ErrorCodes.C11_E_NOTSUPPORTED, NativeLibraryTests.test_c11_stream_read(stream, data, 1));
                Assert.Equal((int) ErrorCodes.C11_E_IO, NativeLibraryTests.test_c11_stream_write(stream, data, 1));
                Assert.Equal((long) ErrorCodes.C11_E_INVALIDARG, NativeLibraryTests.test_c11_stream_seek(stream, 0, SeekDirs.Current));
                Assert.Equal((int) ErrorCodes.C11_E_OBJECTDISPOSED, NativeLibraryTests.test_c11_stream_flush(stream));
                Assert.Equal((long) ErrorCodes.C11_E_ARGUMENTOUTOFRANGE, NativeLibraryTests.test_c11_stream_trunc(stream, 1));
            }
        }

        [Fact]
        public void CallbacksRemainAliveAndReceiveArgumentsAfterCollection()
        {
            var flushes = 0;
            using (var stream = new C11StreamHandle(
                       (self, buffer, count) => { buffer[0] = 42; return 1; },
                       (self, buffer, count) => count,
                       (self, offset, origin) =>
                       {
                           Assert.Equal((int) SeekDirs.End, origin);
                           return offset;
                       },
                       self => { flushes++; return 0; },
                       (self, size) => size))
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                var buffer = new byte[1];
                Assert.Equal(1, NativeLibraryTests.test_c11_stream_read(stream, buffer, 1));
                Assert.Equal(42, buffer[0]);
                Assert.Equal(1, NativeLibraryTests.test_c11_stream_write(stream, buffer, 1));
                Assert.Equal((long) int.MaxValue + 5, NativeLibraryTests.test_c11_stream_seek(stream, (long) int.MaxValue + 5, SeekDirs.End));
                Assert.Equal(0, NativeLibraryTests.test_c11_stream_flush(stream));
                Assert.Equal(1, flushes);
                Assert.Equal((long) int.MaxValue + 9, NativeLibraryTests.test_c11_stream_trunc(stream, (long) int.MaxValue + 9));
            }
        }
    }
}
