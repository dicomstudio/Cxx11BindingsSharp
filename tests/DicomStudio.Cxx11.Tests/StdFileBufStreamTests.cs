using System;
using System.IO;
using System.Text;
using Xunit;

namespace DicomStudio.Cxx11.Tests
{
    public class StdFileBufStreamTests
    {
        private const string TestText = "The quick brown fox jumps over the lazy dog";
        private const string FileName = "StdFileBufStreamTestFile.txt";

        private void CreateTestFile(string? content = null)
        {
            File.WriteAllText(FileName, content ?? TestText, Encoding.ASCII);
        }

        [Fact]
        public void Ctor_FileName_ReadMode()
        {
            CreateTestFile();
            using (var sfs = new StdFileBufStream(FileName, FileMode.Open, FileAccess.Read))
            {
                Assert.True(sfs.CanRead);
                Assert.False(sfs.CanWrite);
                Assert.True(sfs.CanSeek);
            }
        }

        [Fact]
        public void Ctor_FileName_WriteMode()
        {
            using (var sfs = new StdFileBufStream(FileName, FileMode.Create, FileAccess.Write))
            {
                Assert.False(sfs.CanRead);
                Assert.True(sfs.CanWrite);
                Assert.True(sfs.CanSeek);
            }
        }

        [Fact]
        public void Ctor_FileName_ReadWriteMode()
        {
            using (var sfs = new StdFileBufStream(FileName, FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                Assert.True(sfs.CanRead);
                Assert.True(sfs.CanWrite);
                Assert.True(sfs.CanSeek);
            }
        }

        [Fact]
        public void Read_AtEndOfFile_ShouldReturnZero()
        {
            CreateTestFile();
            using (var sfs = StdFileBufStream.OpenRead(FileName))
            {
                sfs.Seek(0, SeekOrigin.End);
                var buffer = new byte[10];
                int read = sfs.Read(buffer, 0, buffer.Length);
                Assert.Equal(0, read);
            }
        }

        [Fact]
        public void Write_ThenReadBack_ShouldMatch()
        {
            var bytes = Encoding.ASCII.GetBytes(TestText);
            using (var sfs = StdFileBufStream.OpenWrite("sfs_rw.txt"))
            {
                sfs.Write(bytes, 0, bytes.Length);
                sfs.Flush();
            }

            using (var sfs = StdFileBufStream.OpenRead("sfs_rw.txt"))
            {
                var buffer = new byte[bytes.Length];
                int read = sfs.Read(buffer, 0, buffer.Length);
                Assert.Equal(bytes.Length, read);
                Assert.Equal(bytes, buffer);
            }
        }

        [Fact]
        public void ZeroLengthFile_ShouldHaveZeroLength()
        {
            CreateTestFile(string.Empty);
            using (var sfs = StdFileBufStream.OpenRead(FileName))
            {
                Assert.Equal(0, sfs.Length);
            }
        }

        [Fact]
        public void Seek_BeyondEnd_ShouldNotThrow()
        {
            CreateTestFile();
            using (var sfs = StdFileBufStream.OpenRead(FileName))
            {
                long beyond = sfs.Length + 100;
                long pos = sfs.Seek(beyond, SeekOrigin.Begin);
                Assert.Equal(beyond, pos);
            }
        }

        [Fact]
        public void Read_AfterDispose_ShouldThrow()
        {
            CreateTestFile();
            var sfs = StdFileBufStream.OpenRead(FileName);
            sfs.Dispose();
            Assert.Throws<ObjectDisposedException>(() => sfs.ReadByte());
        }

        [Fact]
        public void OpenNonExistentFile_ShouldThrow()
        {
            Assert.Throws<FileNotFoundException>(() => StdFileBufStream.OpenRead("does_not_exist.txt"));
        }

        [Fact]
        public void CanRead_ShouldBeTrue_ForReadStream()
        {
            CreateTestFile();
            using (var sfs = StdFileBufStream.OpenRead(FileName))
            {
                Assert.True(sfs.CanRead);
                Assert.False(sfs.CanWrite);
                Assert.True(sfs.CanSeek);
            }
        }

        [Fact]
        public void CanWrite_ShouldBeTrue_ForWriteStream()
        {
            using (var sfs = StdFileBufStream.OpenWrite("sfs_canwrite.txt"))
            {
                Assert.False(sfs.CanRead);
                Assert.True(sfs.CanWrite);
                Assert.True(sfs.CanSeek);
            }
        }

        [Fact]
        public void CanSeek_ShouldBeTrue_ForAllStreams()
        {
            CreateTestFile();
            using (var sfs = StdFileBufStream.OpenRead(FileName))
            {
                Assert.True(sfs.CanSeek);
            }

            using (var sfs = StdFileBufStream.OpenWrite("sfs_canseek.txt"))
            {
                Assert.True(sfs.CanSeek);
            }
        }

        [Fact(Skip = "SetLength unimplemented on windows")]
        public void SetLength_ShouldTruncateOrExtendFile()
        {
            CreateTestFile();
            var originalBytes = File.ReadAllBytes(FileName);

            // Truncate
            using (var sfs = StdFileBufStream.OpenWrite(FileName))
            {
                sfs.SetLength(10);
                Assert.Equal(10, sfs.Length);
            }

            var truncated = File.ReadAllBytes(FileName);
            Assert.Equal(10, truncated.Length);
            Assert.Equal(originalBytes[..10], truncated);

            // Extend
            using (var sfs = StdFileBufStream.OpenWrite(FileName))
            {
                sfs.SetLength(20);
                Assert.Equal(20, sfs.Length);
            }

            var extended = File.ReadAllBytes(FileName);
            Assert.Equal(20, extended.Length);
            Assert.Equal(truncated, extended[..10]);
            Assert.All(extended[10..], b => Assert.Equal(0, b));
        }

        [Fact]
        public void Seek_SetPosition_ReadsExpectedByte()
        {
            CreateTestFile();
            using (var sfs = StdFileBufStream.OpenRead(FileName))
            {
                // Seek to position 4 ('q' in "The quick...")
                long pos = sfs.Seek(4, SeekOrigin.Begin);
                Assert.Equal(4, pos);

                int b = sfs.ReadByte();
                Assert.Equal((int) 'q', b);
            }
        }

        [Fact]
        public void UnicodeFileName_ReadWrite_ShouldSucceed()
        {
            string unicodeFileName = "测试_файл_🚀.txt";
            string testContent = "Unicode filename test content";

            // Write
            using (var sfs = StdFileBufStream.OpenWrite(unicodeFileName))
            {
                var bytes = Encoding.UTF8.GetBytes(testContent);
                sfs.Write(bytes, 0, bytes.Length);
                sfs.Flush();
            }

            Assert.True(File.Exists(unicodeFileName));

            // Read
            using (var sfs = StdFileBufStream.OpenRead(unicodeFileName))
            {
                var buffer = new byte[testContent.Length * 2];
                int read = sfs.Read(buffer, 0, buffer.Length);
                string readText = Encoding.UTF8.GetString(buffer, 0, read);
                Assert.Equal(testContent, readText);
            }

            // Cleanup
            File.Delete(unicodeFileName);
        }
    }
}
