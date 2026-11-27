using System.IO;
using System.IO.Compression;
using System.Text;
using Xunit;

namespace Cxx11BindingsSharp.Tests
{
    public class ManagedC11StreamZipTests
    {
        [Fact]
        public void NativeWrite_ToZipEntry_RoundTrips()
        {
            const string text = "Hello from native C11: \u4f60\u597d \U0001F30D";
            var data = Encoding.UTF8.GetBytes(text);

            using (var storage = new MemoryStream())
            {
                using (var archive = new ZipArchive(storage, ZipArchiveMode.Create, leaveOpen: true))
                {
                    var entry = archive.CreateEntry("message.txt", CompressionLevel.Optimal);
                    using (var stream = new ManagedC11Stream(entry.Open()))
                    {
                        Assert.Equal(data.Length, NativeLibraryTests.test_c11_stream_write(stream, data, data.Length));
                    }
                }

                storage.Position = 0;
                using (var header = new BinaryReader(storage, Encoding.UTF8, leaveOpen: true))
                {
                    Assert.Equal(0x04034b50u, header.ReadUInt32());
                    header.ReadUInt16();
                    header.ReadUInt16();
                    Assert.Equal(8, header.ReadUInt16());
                }

                storage.Position = 0;
                using (var archive = new ZipArchive(storage, ZipArchiveMode.Read, leaveOpen: true))
                {
                    var entry = archive.GetEntry("message.txt");
                    Assert.NotNull(entry);
                    using (var reader = new StreamReader(entry.Open(), Encoding.UTF8))
                    {
                        Assert.Equal(text, reader.ReadToEnd());
                    }
                }
            }
        }
    }
}
