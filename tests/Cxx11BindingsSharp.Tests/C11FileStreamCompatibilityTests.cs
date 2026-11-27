using System;
using System.IO;
using Xunit;

namespace Cxx11BindingsSharp.Tests
{
    public class C11FileStreamCompatibilityTests
    {
        private static void Compare(FileMode mode, FileAccess access, Action<Stream, Stream> test)
        {
            var referencePath = Path.GetTempFileName();
            var nativePath = Path.GetTempFileName();
            try
            {
                var data = new byte[] { 0, 10, 13, 10, 26, 255 };
                File.WriteAllBytes(referencePath, data);
                File.WriteAllBytes(nativePath, data);
                using (var reference = new FileStream(referencePath, mode, access, FileShare.ReadWrite))
                using (var actual = new C11FileStream(nativePath, mode, access, FileShare.ReadWrite))
                    test(reference, actual);
                Assert.Equal(File.ReadAllBytes(referencePath), File.ReadAllBytes(nativePath));
            }
            finally
            {
                File.Delete(referencePath);
                File.Delete(nativePath);
            }
        }

        private static void CompareException(Stream reference, Stream actual, Action<Stream> action)
        {
            var expected = Record.Exception(() => action(reference));
            var observed = Record.Exception(() => action(actual));
            Assert.NotNull(expected);
            Assert.NotNull(observed);
            Assert.Equal(expected.GetType(), observed.GetType());
            if (expected is ArgumentException expectedArgument && observed is ArgumentException observedArgument)
                Assert.Equal(expectedArgument.ParamName, observedArgument.ParamName);
        }

        [Fact]
        public void BinaryReadWriteAndSeekingMatchFileStream()
        {
            Compare(FileMode.Open, FileAccess.ReadWrite, (reference, actual) =>
            {
                var expected = new byte[8];
                var observed = new byte[8];
                Assert.Equal(reference.Read(expected, 1, 4), actual.Read(observed, 1, 4));
                Assert.Equal(expected, observed);
                Assert.Equal(reference.Position, actual.Position);
                Assert.Equal(reference.Length, actual.Length);
                Assert.Equal(reference.Position, actual.Position);
                var bytes = new byte[] { 1, 10, 13, 26, 255, 2 };
                reference.Write(bytes, 1, 4);
                actual.Write(bytes, 1, 4);
                Assert.Equal(reference.Seek(0, SeekOrigin.Begin), actual.Seek(0, SeekOrigin.Begin));
                Assert.Equal(reference.Read(expected, 0, expected.Length), actual.Read(observed, 0, observed.Length));
                Assert.Equal(expected, observed);
                Assert.Equal(reference.Read(expected, 0, 0), actual.Read(observed, 0, 0));
                reference.Flush();
                actual.Flush();
            });
        }

        [Fact]
        public void InvalidArgumentsMatchFileStream()
        {
            Compare(FileMode.Open, FileAccess.ReadWrite, (reference, actual) =>
            {
#pragma warning disable CS8625
                CompareException(reference, actual, s => s.Read(null, 0, 0));
                CompareException(reference, actual, s => s.Write(null, 0, 0));
#pragma warning restore CS8625
                CompareException(reference, actual, s => s.Read(new byte[4], -1, 1));
                CompareException(reference, actual, s => s.Read(new byte[4], 0, -1));
                CompareException(reference, actual, s => s.Read(new byte[4], 3, 2));
                CompareException(reference, actual, s => s.Write(new byte[4], -1, 1));
                CompareException(reference, actual, s => s.Write(new byte[4], 0, -1));
                CompareException(reference, actual, s => s.Write(new byte[4], 3, 2));
                CompareException(reference, actual, s => s.Seek(-1, SeekOrigin.Begin));
                CompareException(reference, actual, s => s.Seek(0, (SeekOrigin) 42));
                CompareException(reference, actual, s => s.Position = -1);
                CompareException(reference, actual, s => s.SetLength(-1));
            });
        }

        [Theory]
        [InlineData(FileAccess.Read)]
        [InlineData(FileAccess.Write)]
        public void AccessRestrictionsMatchFileStream(FileAccess access)
        {
            Compare(FileMode.Open, access, (reference, actual) =>
            {
                Assert.Equal(reference.CanRead, actual.CanRead);
                Assert.Equal(reference.CanWrite, actual.CanWrite);
                Assert.Equal(reference.CanSeek, actual.CanSeek);
                if (access == FileAccess.Read)
                {
                    CompareException(reference, actual, s => s.Write(new byte[1], 0, 1));
                    CompareException(reference, actual, s => s.SetLength(0));
                }
                else
                    CompareException(reference, actual, s => s.Read(new byte[1], 0, 1));
                reference.Flush();
                actual.Flush();
            });
        }

        [Fact]
        public void SetLengthPreservesBufferedWritesAndMatchesPosition()
        {
            Compare(FileMode.Open, FileAccess.ReadWrite, (reference, actual) =>
            {
                reference.Position = 4;
                actual.Position = 4;
                reference.WriteByte(42);
                actual.WriteByte(42);
                reference.SetLength(3);
                actual.SetLength(3);
                Assert.Equal(reference.Length, actual.Length);
                Assert.Equal(reference.Position, actual.Position);
                reference.SetLength(8);
                actual.SetLength(8);
                Assert.Equal(reference.Length, actual.Length);
                Assert.Equal(reference.Position, actual.Position);
                reference.WriteByte(99);
                actual.WriteByte(99);
            });
        }

        [Fact]
        public void DisposedStreamMatchesFileStream()
        {
            Compare(FileMode.Open, FileAccess.ReadWrite, (reference, actual) =>
            {
                reference.Dispose();
                actual.Dispose();
                reference.Dispose();
                actual.Dispose();
                Assert.Equal(reference.CanRead, actual.CanRead);
                Assert.Equal(reference.CanWrite, actual.CanWrite);
                Assert.Equal(reference.CanSeek, actual.CanSeek);
                CompareException(reference, actual, s => s.Read(new byte[1], 0, 1));
                CompareException(reference, actual, s => s.Write(new byte[1], 0, 1));
                CompareException(reference, actual, s => s.Flush());
                CompareException(reference, actual, s => s.Seek(0, SeekOrigin.Begin));
                CompareException(reference, actual, s => { var length = s.Length; });
                CompareException(reference, actual, s => { var position = s.Position; });
                CompareException(reference, actual, s => s.Position = 0);
                CompareException(reference, actual, s => s.SetLength(0));
            });
        }

        [Fact]
        public void CreateNewDoesNotTruncateExistingFile()
        {
            var path = Path.GetTempFileName();
            try
            {
                var bytes = new byte[] { 1, 2, 3 };
                File.WriteAllBytes(path, bytes);
                Assert.Throws<NotSupportedException>(() => new C11FileStream(path, FileMode.CreateNew, FileAccess.Write));
                Assert.Equal(bytes, File.ReadAllBytes(path));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Theory]
        [InlineData(FileMode.Create, FileAccess.Read)]
        [InlineData(FileMode.Truncate, FileAccess.Read)]
        [InlineData(FileMode.Append, FileAccess.Read)]
        [InlineData(FileMode.Append, FileAccess.ReadWrite)]
        public void InvalidModeAndAccessDoNotModifyFile(FileMode mode, FileAccess access)
        {
            var path = Path.GetTempFileName();
            try
            {
                var bytes = new byte[] { 1, 2, 3 };
                File.WriteAllBytes(path, bytes);
                var expected = Record.Exception(() => new FileStream(path, mode, access).Dispose());
                var observed = Record.Exception(() => new C11FileStream(path, mode, access).Dispose());
                Assert.NotNull(expected);
                Assert.NotNull(observed);
                Assert.Equal(expected.GetType(), observed.GetType());
                Assert.Equal(bytes, File.ReadAllBytes(path));
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
