using System;
using System.IO;
using Xunit;

namespace DicomStudio.Cxx11.Tests
{
    public class ManagedStreamBufTests
    {
        private static int ReadEmpty(byte[] buffer, int count)
        {
            return 0;
        }

        private static int WriteEmpty(byte[] buffer, int count)
        {
            return 0;
        }

        private static int FlushEmpty()
        {
            return 0;
        }

        private static long SeekEmpty(long offset, int origin)
        {
            return 0;
        }

        private static int ReadError(byte[] buffer, int count)
        {
            return -1;
        }

        private static int WriteError(byte[] buffer, int count)
        {
            return -1;
        }

        [Fact]
        public void Buffer0_ShouldFail()
        {
            // act & assert
            Assert.Throws<ArgumentException>(() => new ManagedStreamBuf(ReadEmpty, WriteEmpty,
                FlushEmpty, SeekEmpty, 0));
        }

        [Fact]
        public void Buffer1_ShouldFail()
        {
            // act & assert
            Assert.Throws<ArgumentException>(() => new ManagedStreamBuf(ReadEmpty, WriteEmpty,
                FlushEmpty, SeekEmpty, 1));
        }

        [Fact]
        public void ReadError_ShouldFail()
        {
            //arrange
            var sut = new ManagedStreamBuf(ReadError, WriteError, FlushEmpty, SeekEmpty);
            var buffer = new byte[42];

            // act & assert
            Assert.Throws<NotSupportedException>(() => sut.ReadInto(buffer));
        }

        [Fact]
        public void WriteError_ShouldFail()
        {
            //arrange
            var sut = new ManagedStreamBuf(ReadError, WriteError, FlushEmpty, SeekEmpty, 3);
            var buffer = new byte[42];

            // act & assert
            Assert.Throws<NotSupportedException>(() => sut.WriteInto(buffer));
        }
    }
}
