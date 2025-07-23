using System;
using System.IO;
using System.Text;
using Xunit;

namespace Cxx11BindingsSharp.Tests
{
    [Collection("NonParallelTests")]
    public class ManagedStreamBufShimFileTests
    {
        private const string TestText = "The quick brown fox jumps over the lazy dog";
        private const string FileName = "ManagedStreamBufShimFileTestsFile.txt";

        private void CreateTestFile(string? content = null)
        {
            File.WriteAllText(FileName, content ?? TestText, Encoding.ASCII);
        }

        [Fact]
        public void Read_ShouldSucceed()
        {
            CreateTestFile();
            using (var sfs = new FileStream(FileName, FileMode.Open, FileAccess.Read))
            {
                var wrap = new ManagedStreamBufShim(sfs);
                var buffer = new byte[512];
                var ret = wrap.Read(buffer, 0, buffer.Length);
                Assert.Equal(TestText.Length, ret);
                var data = Encoding.UTF8.GetBytes(TestText);
                Assert.Equal(data, buffer[..ret]);
            }
        }

        [Fact]
        public void Read_ShouldSucceed2()
        {
            CreateTestFile();
            using (var sfs = new FileStream(FileName, FileMode.Open, FileAccess.Read))
            {
                var wrap = new ManagedStreamBufShim(sfs);
                var buffer = new byte[512];
                var ret = wrap.Read(buffer, 0, buffer.Length);
                Assert.Equal(TestText.Length, ret);
                var data = Encoding.UTF8.GetBytes(TestText);
                Assert.Equal(data, buffer[..ret]);
            }
        }

        [Fact]
        public void CanReadWriteSeek_ReflectsUnderlyingStream()
        {
            CreateTestFile();
            using (var sfs = new FileStream(FileName, FileMode.Open, FileAccess.Read))
            {
                var wrap = new ManagedStreamBufShim(sfs);
                Assert.True(wrap.CanRead);
                Assert.False(wrap.CanWrite);
                Assert.True(wrap.CanSeek);
            }
        }

        [Fact]
        public void Length_ReturnsUnderlyingFileLength()
        {
            const string path = "dest.txt";
            File.Delete(path);
            var fs = File.OpenWrite(path);
            var wrap = new ManagedStreamBufShim(fs);
            var data = Encoding.UTF8.GetBytes("abcd");
            wrap.Write(data, 0, data.Length);
            wrap.Flush();
            var length = new System.IO.FileInfo(path).Length;
            Assert.Equal(data.Length, length);
        }

        [Fact]
        public void Ctor_FileName_ReadMode()
        {
            CreateTestFile();
            using (var sfs = new FileStream(FileName, FileMode.Open, FileAccess.Read))
            {
                var wrap = new ManagedStreamBufShim(sfs);
                Assert.True(wrap.CanRead);
                Assert.False(wrap.CanWrite);
                Assert.True(wrap.CanSeek);
            }
        }

        [Fact]
        public void Ctor_FileName_WriteMode()
        {
            using (var sfs = new FileStream(FileName, FileMode.Create, FileAccess.Write))
            {
                var wrap = new ManagedStreamBufShim(sfs);
                Assert.False(wrap.CanRead);
                Assert.True(wrap.CanWrite);
                Assert.True(wrap.CanSeek);
            }
        }

        [Fact]
        public void Ctor_FileName_ReadWriteMode()
        {
            using (var sfs = new FileStream(FileName, FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                var wrap = new ManagedStreamBufShim(sfs);
                Assert.True(wrap.CanRead);
                Assert.True(wrap.CanWrite);
                Assert.True(wrap.CanSeek);
            }
        }

        [Fact]
        public void Read_AtEndOfFile_ShouldReturnZero()
        {
            CreateTestFile();
            using (var sfs = File.OpenRead(FileName))
            {
                var wrap = new ManagedStreamBufShim(sfs);
                wrap.Seek(0, SeekOrigin.End);
                var buffer = new byte[10];
                int read = wrap.Read(buffer, 0, buffer.Length);
                Assert.Equal(0, read);
            }
        }

        [Fact]
        public void Write_ThenReadBack_ShouldMatch()
        {
            var bytes = Encoding.ASCII.GetBytes(TestText);
            using (var sfs = File.OpenWrite("sfs_rw.txt"))
            {
                var wrap = new ManagedStreamBufShim(sfs);
                wrap.Write(bytes, 0, bytes.Length);
                wrap.Flush();
            }

            using (var sfs = File.OpenRead("sfs_rw.txt"))
            {
                var wrap = new ManagedStreamBufShim(sfs);
                var buffer = new byte[bytes.Length];
                int read = wrap.Read(buffer, 0, buffer.Length);
                Assert.Equal(bytes.Length, read);
                Assert.Equal(bytes, buffer);
            }
        }

        [Fact]
        public void ZeroLengthFile_ShouldHaveZeroLength()
        {
            CreateTestFile(string.Empty);
            using (var sfs = File.OpenRead(FileName))
            {
                var wrap = new ManagedStreamBufShim(sfs);
                Assert.Equal(0, wrap.Length);
            }
        }

        [Fact]
        public void Seek_BeyondEnd_ShouldNotThrow()
        {
            CreateTestFile();
            using (var sfs = File.OpenRead(FileName))
            {
                var wrap = new ManagedStreamBufShim(sfs);
                long beyond = wrap.Length + 100;
                long pos = wrap.Seek(beyond, SeekOrigin.Begin);
                Assert.Equal(beyond, pos);
            }
        }

        [Fact]
        public void Read_AfterDispose_ShouldThrow()
        {
            CreateTestFile();
            var sfs = File.OpenRead(FileName);
            var wrap = new ManagedStreamBufShim(sfs);
            wrap.Dispose();
            Assert.Throws<ObjectDisposedException>(() => wrap.ReadByte());
        }

        [Fact]
        public void CanRead_ShouldBeTrue_ForReadStream()
        {
            CreateTestFile();
            using (var sfs = File.OpenRead(FileName))
            {
                var wrap = new ManagedStreamBufShim(sfs);
                Assert.True(wrap.CanRead);
                Assert.False(wrap.CanWrite);
                Assert.True(wrap.CanSeek);
            }
        }

        [Fact]
        public void CanWrite_ShouldBeTrue_ForWriteStream()
        {
            using (var sfs = File.OpenWrite("sfs_canwrite.txt"))
            {
                var wrap = new ManagedStreamBufShim(sfs);
                Assert.False(wrap.CanRead);
                Assert.True(wrap.CanWrite);
                Assert.True(wrap.CanSeek);
            }
        }

        [Fact]
        public void CanSeek_ShouldBeTrue_ForAllStreams()
        {
            CreateTestFile();
            using (var sfs = File.OpenRead(FileName))
            {
                var wrap = new ManagedStreamBufShim(sfs);
                Assert.True(wrap.CanSeek);
            }

            using (var sfs = File.OpenWrite("sfs_canseek.txt"))
            {
                var wrap = new ManagedStreamBufShim(sfs);
                Assert.True(wrap.CanSeek);
            }
        }

        [Fact]
        public void SetLength_ShouldTruncateOrExtendFile()
        {
            CreateTestFile();
            var originalBytes = File.ReadAllBytes(FileName);

            // Truncate
            using (var sfs = File.OpenWrite(FileName))
            {
                var wrap = new ManagedStreamBufShim(sfs);
                wrap.SetLength(10);
                Assert.Equal(10, wrap.Length);
            }

            var truncated = File.ReadAllBytes(FileName);
            Assert.Equal(10, truncated.Length);
            Assert.Equal(originalBytes[..10], truncated);

            // Extend
            using (var sfs = File.OpenWrite(FileName))
            {
                var wrap = new ManagedStreamBufShim(sfs);
                wrap.SetLength(20);
                Assert.Equal(20, wrap.Length);
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
            using (var sfs = File.OpenRead(FileName))
            {
                var wrap = new ManagedStreamBufShim(sfs);
                // Seek to position 4 ('q' in "The quick...")
                long pos = wrap.Seek(4, SeekOrigin.Begin);
                Assert.Equal(4, pos);

                int b = wrap.ReadByte();
                Assert.Equal((int) 'q', b);
            }
        }

        [Fact]
        public void UnicodeFileName_ReadWrite_ShouldSucceed()
        {
            string unicodeFileName = "测试_файл_🚀.txt";
            string testContent = "Unicode filename test content";

            // Write
            using (var sfs = File.OpenWrite(unicodeFileName))
            {
                var wrap = new ManagedStreamBufShim(sfs);
                var bytes = Encoding.UTF8.GetBytes(testContent);
                wrap.Write(bytes, 0, bytes.Length);
                wrap.Flush();
            }

            Assert.True(File.Exists(unicodeFileName));

            // Read
            using (var sfs = File.OpenRead(unicodeFileName))
            {
                var wrap = new ManagedStreamBufShim(sfs);
                var buffer = new byte[testContent.Length * 2];
                int read = wrap.Read(buffer, 0, buffer.Length);
                string readText = Encoding.UTF8.GetString(buffer, 0, read);
                Assert.Equal(testContent, readText);
            }

            // Cleanup
            File.Delete(unicodeFileName);
        }
    }
}
