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
        public ManagedStreamBufStream(Stream? stream)
        {
            _stream = stream ?? throw new System.ArgumentNullException(nameof(stream));
            _managedStreamBuf = new ManagedStreamBuf(Read, Write, FlushImpl, Seek);
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

        private long Seek(long offset, int origin)
        {
            try
            {
                var ret = _stream.Seek(offset, (SeekOrigin) origin);
                return ret;
            }
            catch
            {
                return -1;
            }
        }

        private int FlushImpl()
        {
            try
            {
                _stream.Flush();
                return 0;
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

        public override void Flush()
        {
            var ret = NativeLibrary.cxx11_managed_streambuf_flush(_managedStreamBuf);
            if (ret < 0) throw new IOException("flush");
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var tmp = new byte[count];
            _managedStreamBuf.ReadInto(tmp);
            tmp.CopyTo(buffer, offset);
            return count;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            var tmp = new byte[count];
            _managedStreamBuf.WriteInto(tmp);
            tmp.CopyTo(buffer, offset);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _stream.SetLength(value);
        }


        public override bool CanRead => _stream.CanRead;
        public override bool CanSeek => _stream.CanSeek;
        public override bool CanWrite => _stream.CanWrite;
        public override long Length => _stream.Length;

        public override long Position
        {
            get => _stream.Position;
            set => _stream.Position = value;
        }
    }
}
