using System.IO;
using System.Text;
using Xunit;

namespace DicomStudio.Cxx11.Tests
{
    public class ManagedStreamBufStreamTests
    {
        [Fact]
        public void ReadIntoTest()
        {
            var bytes = Encoding.ASCII.GetBytes("BufferedStreamWrapperTests");
            var src = new MemoryStream();
            src.Write(bytes);
            src.Position = 0;
            var dst = new MemoryStream();
            var srcWrap = new ManagedStreamBufStream(src);
            var bytes2 = new byte[bytes.Length];
            srcWrap.ReadInto(bytes2);

            Assert.Equal(bytes, bytes2);
        }

        [Fact]
        public void WriteIntoTest()
        {
            var bytes = Encoding.ASCII.GetBytes("BufferedStreamWrapperTests");
            var src = new MemoryStream();
            var srcWrap = new ManagedStreamBufStream(src);
            srcWrap.WriteInto(bytes);

            Assert.Equal(bytes, src.ToArray());
        }

        [Fact]
        public void FileStreamTemplate()
        {
            var bytes = Encoding.ASCII.GetBytes("BufferedStreamWrapperTests");
            const string destination = "FileUnitTest.txt";
            // using (FileStream createdFile = File.Create("c:/Temp/testfile.txt", 4096, FileOptions.WriteThrough ))
            using (var fStream = File.OpenWrite(destination))
            {
                fStream.Write(bytes);
                fStream.Flush();
            }

            var src = new FileStream(destination, FileMode.Open);
            const int nBytesToRead = 4;
            var buffer = new byte[nBytesToRead];
            var nBytes = src.Read(buffer);
            src.Flush();
            Assert.Equal(nBytesToRead, nBytes);
            Assert.Equal(nBytesToRead, src.Position);
            Assert.Equal(bytes[..nBytesToRead], buffer);
        }
    }
}