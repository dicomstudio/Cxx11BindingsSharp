using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Xunit;

namespace Cxx11BindingsSharp.Tests
{
    public class C11FileStreamTests
    {
        private const string TestText = "The quick brown fox jumps over the lazy dog";

        private static string CreateTestFile(string? fileName = null, string? content = null)
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
                using var c11FileStream = new C11FileStream(filename, FileMode.Open, FileAccess.Read);
                var buffer = new byte[512];
                var ret = c11FileStream.Read(buffer, 0, buffer.Length);
                Assert.Equal(TestText.Length, ret);
                var data = Encoding.UTF8.GetBytes(TestText);
                Assert.Equal(data, buffer[..ret]);
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
                    using var fs = new C11FileStream(filename, FileMode.Create, FileAccess.Write);
                    var bytes = Encoding.UTF8.GetBytes(TestText);
                    fs.Write(bytes, 0, bytes.Length);
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

                using var fs = new C11FileStream(filename, FileMode.Open, FileAccess.Read);
                fs.Seek(2, SeekOrigin.Begin); // Seek to the third character ('c')
                int value = fs.ReadByte();
                Assert.Equal((byte) 'c', value);
            }
            finally
            {
                File.Delete(filename);
            }
        }

        [Fact]
        public void Read_ShouldThrowNotSupported()
        {
            const string filename = "NotSupported.txt";
            const string content = "test data";
            try
            {
                using (var fs = new C11FileStream(filename, FileMode.Create, FileAccess.Write))
                {
                    var bytes = Encoding.UTF8.GetBytes(content);
                    fs.Write(bytes, 0, bytes.Length);
                    fs.Flush();

                    fs.Seek(0, SeekOrigin.Begin);
                    var buffer = new byte[bytes.Length];
                    Assert.Throws<NotSupportedException>(() => fs.Read(buffer, 0, buffer.Length));
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

                using (var writer = new C11FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                {
                    writer.Write(data, 0, data.Length);

                    // Act: flush without closing
                    writer.Flush();

                    // Assert: open a new stream and verify data is there
                    using (var reader =
                           new C11FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
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
        public void SetLength_ShouldChangeFileSize()
        {
            const string filename = "SetLength_ShouldChangeFileSize.txt";
            try
            {
                using (var fs = new C11FileStream(filename, FileMode.Create, FileAccess.Write))
                {
                    fs.SetLength(100);
                    Assert.Equal(100, fs.Length);

                    fs.SetLength(50);
                    Assert.Equal(50, fs.Length);
                }

                var fileInfo = new FileInfo(filename);
                Assert.Equal(50, fileInfo.Length);
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
                using (var fs = new C11FileStream(unicodeFilename, FileMode.Create, FileAccess.Write))
                {
                    var bytes = Encoding.UTF8.GetBytes(testContent);
                    fs.Write(bytes, 0, bytes.Length);
                }

                Assert.True(File.Exists(unicodeFilename));

                // Read from the Unicode file
                using (var fs = new C11FileStream(unicodeFilename, FileMode.Open, FileAccess.Read))
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
