using System;
using System.IO;
#if !NET6_0_OR_GREATER
using System.Linq;
#endif
using System.Text;
using Xunit;

namespace Cxx11BindingsSharp.Tests
{
    public class Cxx11FileStreamTests
    {
        private const string TestText = "The quick brown fox jumps over the lazy dog";

        private static string CreateTestFile(
#if NET6_0_OR_GREATER
            string? fileName = null, string? content = null
#else
            string fileName = null, string content = null
#endif
        )
        {
            var filename = fileName ?? "C11FileStreamTests.txt";
            File.WriteAllText(filename, content ?? TestText, Encoding.ASCII);
            return filename;
        }

        [Fact]
        public void Read_ShouldSucceed()
        {
            var tempFilePath = Path.GetTempFileName();
            var filename = CreateTestFile(tempFilePath);
            try
            {
                using (var c11FileStream = new Cxx11FileStream(filename, FileMode.Open, FileAccess.Read))
                {
                    var buffer = new byte[512];
                    var ret = c11FileStream.Read(buffer, 0, buffer.Length);
                    Assert.Equal(TestText.Length, ret);
                    var data = Encoding.UTF8.GetBytes(TestText);
#if NET6_0_OR_GREATER
                    Assert.Equal(data, buffer[..ret]);
#else
                    Assert.Equal(data, buffer.Take(ret).ToArray());
#endif
                }
            }
            finally
            {
                File.Delete(filename);
            }
        }

        [Fact]
        public void Write_ShouldSucceed()
        {
            const string filename = "Write_ShouldSucceed.txt";

            try
            {
                {
                    // Open FileStream for writing
                    using (var fs = new Cxx11FileStream(filename, FileMode.Create, FileAccess.Write))
                    {
                        var bytes = Encoding.UTF8.GetBytes(TestText);
                        fs.Write(bytes, 0, bytes.Length);
                    }
                }

                var allText = File.ReadAllText(filename, Encoding.ASCII);
                Assert.Equal(TestText, allText);
            }
            finally
            {
                File.Delete(filename);
            }
        }

        [Fact]
        public void Seek_ShouldPositionStreamCorrectly()
        {
            const string filename = "Seek_ShouldPositionStreamCorrectly.txt";
            const string content = "abcdef";
            try
            {
                File.WriteAllText(filename, content, Encoding.ASCII);

                using (var fs = new Cxx11FileStream(filename, FileMode.Open, FileAccess.Read))
                {
                    fs.Seek(2, SeekOrigin.Begin); // Seek to the third character ('c')
                    var value = fs.ReadByte();
                    Assert.Equal((byte) 'c', value);
                }
            }
            finally
            {
                File.Delete(filename);
            }
        }

        [Fact]
        public void Flush_MakesDataVisibleToOtherStream()
        {
            // Arrange
            var path = Path.GetTempFileName();
            try
            {
                var data = new byte[] {10, 20, 30};

                using (var writer = new Cxx11FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                {
                    writer.Write(data, 0, data.Length);

                    // Act: flush without closing
                    writer.Flush();

                    // Assert: open a new stream and verify data is there
                    using (var reader =
                           new Cxx11FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        var buffer = new byte[data.Length];
                        var bytesRead = reader.Read(buffer, 0, buffer.Length);

                        Assert.Equal(data.Length, bytesRead);
                        Assert.Equal(data, buffer);
                    }
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void SetLength_IsNotSupportedByNativeFilebuf()
        {
            const string filename = "SetLength_ShouldChangeFileSize.txt";
            try
            {
                using (var fs = new Cxx11FileStream(filename, FileMode.Create, FileAccess.Write))
                {
                    Assert.Throws<NotSupportedException>(() => fs.SetLength(100));
                }
            }
            finally
            {
                File.Delete(filename);
            }
        }

        [Fact]
        public void ReadWrite_UnicodeFilename_ShouldSucceed()
        {
            // Use a Unicode filename
            var unicodeFilename = "测试文件🌟.txt";
            var testContent = "Unicode test content: 你好, мир, 🌍";

            try
            {
                // Write to the Unicode file
                using (var fs = new Cxx11FileStream(unicodeFilename, FileMode.Create, FileAccess.Write))
                {
                    var bytes = Encoding.UTF8.GetBytes(testContent);
                    fs.Write(bytes, 0, bytes.Length);
                }

                Assert.True(File.Exists(unicodeFilename));

                // Read from the Unicode file
                using (var fs = new Cxx11FileStream(unicodeFilename, FileMode.Open, FileAccess.Read))
                {
                    var buffer = new byte[1024];
                    var bytesRead = fs.Read(buffer, 0, buffer.Length);
                    var readContent = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Assert.Equal(testContent, readContent);
                }
            }
            finally
            {
                File.Delete(unicodeFilename);
            }
        }
    }
}
