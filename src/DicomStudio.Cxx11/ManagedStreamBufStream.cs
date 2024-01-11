using System.IO;

namespace DicomStudio.Cxx11
{
    /// <summary>
    /// Buffered stream wrapper.
    /// </summary>
    public sealed class ManagedStreamBufStream : Stream
    {
        private readonly Stream _stream;
        private readonly ManagedStreamBuf _managedStreamBuf;

        /// <summary>
        /// Buffered stream wrapper.
        /// </summary>
        /// <param name="stream"></param>
        public ManagedStreamBufStream(Stream stream)
        {
            _stream = stream;
            _managedStreamBuf = new ManagedStreamBuf(Read, Write);
        }

        private int Read(byte[] buffer, int count)
        {
            try
            {
                return _stream.Read(buffer, 0, count);
            }
            catch
            {
                // https://www.mono-project.com/docs/advanced/pinvoke/#runtime-exception-propagation
                // https://github.com/dotnet/runtime/issues/4756
                return -1;
            }
        }

        private int Write(byte[] buffer, int count)
        {
            try
            {
                _stream.Write(buffer, 0, count);
                return count;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// Return the std::streambuf* handle.
        /// </summary>
        /// <param name="managedStreamBufStream"></param>
        /// <returns></returns>
        public static implicit operator StreamBufHandle(ManagedStreamBufStream managedStreamBufStream)
        {
            return managedStreamBufStream._managedStreamBuf;
        }

        // FIXME
        public void CopyTo(ManagedStreamBufStream destination)
        {
            _managedStreamBuf.CopyTo(destination._managedStreamBuf);
        }

        public void ReadInto(byte[] buffer)
        {
            _managedStreamBuf.ReadInto(buffer);
        }

        public void WriteInto(byte[] buffer)
        {
            _managedStreamBuf.WriteInto(buffer);
        }

        public override void Flush()
        {
            throw new System.NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var tmp = new byte[count];
            this.ReadInto(tmp);
            tmp.CopyTo(buffer, offset);
            return count;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new System.NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new System.NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            var tmp = new byte[count];
            WriteInto(tmp);
            tmp.CopyTo(buffer, offset);
        }

        public override bool CanRead { get; }
        public override bool CanSeek { get; }
        public override bool CanWrite { get; }
        public override long Length { get; }
        public override long Position { get; set; }
    }
}