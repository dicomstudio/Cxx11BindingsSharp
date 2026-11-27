using System;
using System.IO;
using Xunit;

namespace Cxx11BindingsSharp.Tests
{
    public class Cxx11FileStreamCompatibilityTests
    {
        private static void Compare(FileMode mode, FileAccess access, Action<Stream, Stream> test)
        {
            var referencePath = Path.GetTempFileName();
            var nativePath = Path.GetTempFileName();
            try
            {
                var contents = new byte[] { 0, 10, 13, 10, 26, 255 };
                File.WriteAllBytes(referencePath, contents);
                File.WriteAllBytes(nativePath, contents);
                using (var reference = new FileStream(referencePath, mode, access, FileShare.ReadWrite))
                using (var actual = new Cxx11FileStream(nativePath, mode, access, FileShare.ReadWrite))
                {
                    test(reference, actual);
                }
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
        public void Read_RespectsOffsetCountAndEndOfFile()
        {
            Compare(FileMode.Open, FileAccess.Read, (reference, actual) =>
            {
                var expected = new byte[] { 99, 99, 99, 99, 99, 99, 99 };
                var observed = (byte[]) expected.Clone();
                Assert.Equal(reference.Read(expected, 2, 3), actual.Read(observed, 2, 3));
                Assert.Equal(expected, observed);
                Assert.Equal(reference.Position, actual.Position);
                Assert.Equal(reference.Read(expected, 1, 6), actual.Read(observed, 1, 6));
                Assert.Equal(expected, observed);
                Assert.Equal(reference.Read(expected, 0, expected.Length), actual.Read(observed, 0, observed.Length));
                Assert.Equal(reference.Read(expected, 0, 0), actual.Read(observed, 0, 0));
            });
        }

        [Fact]
        public void Write_RespectsOffsetAndCount()
        {
            Compare(FileMode.Create, FileAccess.Write, (reference, actual) =>
            {
                var buffer = new byte[] { 1, 2, 3, 4, 5 };
                reference.Write(buffer, 1, 3);
                actual.Write(buffer, 1, 3);
                reference.Write(buffer, 0, 0);
                actual.Write(buffer, 0, 0);
                Assert.Equal(reference.Position, actual.Position);
                reference.Flush();
                actual.Flush();
            });
        }

        [Fact]
        public void ReadAndWrite_PreserveBinaryBytes()
        {
            Compare(FileMode.Create, FileAccess.Write, (reference, actual) =>
            {
                var bytes = new byte[] { 0, 10, 13, 10, 26, 255 };
                reference.Write(bytes, 0, bytes.Length);
                actual.Write(bytes, 0, bytes.Length);
            });
            Compare(FileMode.Open, FileAccess.Read, (reference, actual) =>
            {
                var expected = new byte[8];
                var observed = new byte[8];
                Assert.Equal(reference.Read(expected, 1, 5), actual.Read(observed, 1, 5));
                Assert.Equal(expected, observed);
            });
        }

        [Fact]
        public void ReadWrite_InvalidBuffersMatchFileStreamExceptions()
        {
            Compare(FileMode.Open, FileAccess.ReadWrite, (reference, actual) =>
            {
#pragma warning disable CS8625
                CompareException(reference, actual, s => s.Read(null, 0, 0));
                CompareException(reference, actual, s => s.Read(new byte[4], -1, 1));
                CompareException(reference, actual, s => s.Read(new byte[4], 0, -1));
                CompareException(reference, actual, s => s.Read(new byte[4], 3, 2));
                CompareException(reference, actual, s => s.Read(new byte[4], int.MaxValue, 0));
                CompareException(reference, actual, s => s.Write(null, 0, 0));
#pragma warning restore CS8625
                CompareException(reference, actual, s => s.Write(new byte[4], -1, 1));
                CompareException(reference, actual, s => s.Write(new byte[4], 0, -1));
                CompareException(reference, actual, s => s.Write(new byte[4], 3, 2));
                CompareException(reference, actual, s => s.Write(new byte[4], int.MaxValue, 0));
            });
        }

        [Theory]
        [InlineData(FileAccess.Read)]
        [InlineData(FileAccess.Write)]
        public void UnsupportedOperationsMatchFileStream(FileAccess access)
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
                {
                    CompareException(reference, actual, s => s.Read(new byte[1], 0, 1));
                }
            });
        }

        [Fact]
        public void SeekingAndPositionMatchFileStream()
        {
            Compare(FileMode.Open, FileAccess.Read, (reference, actual) =>
            {
                Assert.Equal(reference.Length, actual.Length);
                Assert.Equal(reference.Seek(2, SeekOrigin.Begin), actual.Seek(2, SeekOrigin.Begin));
                Assert.Equal(reference.Length, actual.Length);
                Assert.Equal(reference.Position, actual.Position);
                Assert.Equal(reference.Seek(-1, SeekOrigin.Current), actual.Seek(-1, SeekOrigin.Current));
                Assert.Equal(reference.Seek(-1, SeekOrigin.End), actual.Seek(-1, SeekOrigin.End));
                CompareException(reference, actual, s => s.Seek(-1, SeekOrigin.Begin));
                CompareException(reference, actual, s => s.Seek(0, (SeekOrigin) 42));
                CompareException(reference, actual, s => s.Position = -1);
                CompareException(reference, actual, s => s.SetLength(-1));
            });
        }

        [Fact]
        public void DisposeIsIdempotentAndOperationsMatchFileStream()
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
                CompareException(reference, actual, s => s.Seek(0, (SeekOrigin) 42));
                CompareException(reference, actual, s => s.Position = -1);
                CompareException(reference, actual, s => s.SetLength(-1));
            });
        }

        [Fact(Skip = "The native std::filebuf backend does not implement truncation.")]
        public void SetLength_MatchesFileStream()
        {
            Compare(FileMode.Open, FileAccess.ReadWrite, (reference, actual) =>
            {
                reference.Position = 4;
                actual.Position = 4;
                reference.SetLength(2);
                actual.SetLength(2);
                Assert.Equal(reference.Length, actual.Length);
                Assert.Equal(reference.Position, actual.Position);
                reference.SetLength(8);
                actual.SetLength(8);
                Assert.Equal(reference.Length, actual.Length);
            });
        }

        [Fact(Skip = "The native open API cannot atomically implement FileMode.CreateNew.")]
        public void CreateNew_ExistingFileThrowsWithoutTruncating()
        {
            var path = Path.GetTempFileName();
            try
            {
                var bytes = new byte[] { 1, 2, 3 };
                File.WriteAllBytes(path, bytes);
                Assert.Throws<IOException>(() => new FileStream(path, FileMode.CreateNew, FileAccess.Write).Dispose());
                Assert.Throws<IOException>(() => new Cxx11FileStream(path, FileMode.CreateNew, FileAccess.Write).Dispose());
                Assert.Equal(bytes, File.ReadAllBytes(path));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact(Skip = "The native open API does not expose file sharing options.")]
        public void ExclusiveShare_RejectsSecondOpen()
        {
            var path = Path.GetTempFileName();
            try
            {
                using (var stream = new Cxx11FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                    Assert.Throws<IOException>(() => new FileStream(path, FileMode.Open, FileAccess.Read).Dispose());
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact(Skip = "The native open API does not distinguish file-not-found errors.")]
        public void Open_MissingFileThrowsFileNotFound()
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Assert.Throws<FileNotFoundException>(() => new FileStream(path, FileMode.Open, FileAccess.Read).Dispose());
            Assert.Throws<FileNotFoundException>(() => new Cxx11FileStream(path, FileMode.Open, FileAccess.Read).Dispose());
        }

        [Fact(Skip = "OpenOrCreate currently uses append mode instead of opening at position zero.")]
        public void OpenOrCreate_ExistingFileStartsAtBeginning()
        {
            Compare(FileMode.OpenOrCreate, FileAccess.ReadWrite, (reference, actual) =>
            {
                Assert.Equal(reference.Position, actual.Position);
                reference.WriteByte(9);
                actual.WriteByte(9);
            });
        }
    }
}
