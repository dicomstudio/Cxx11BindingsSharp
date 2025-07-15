using System.IO;

namespace DicomStudio.Cxx11
{
    /// <summary>
    /// Dual personality buffered stream wrapper. Provide a C++ streambuf interface for a managed Stream.
    /// Since it derives from <see cref="Stream"/>, it can be used in any place where a <see cref="Stream"/> is expected.
    /// </summary>
    public sealed class ManagedStreamBufShim : Stream
    {
        // the actual stream we are wrapping
        private readonly Stream _actualStream;
        // the managed streambuf handle (aka std::streambuf*)
        private readonly ManagedStreamBuf _managedStreamBuf;

        /// <summary>
        /// Buffered stream wrapper.
        /// </summary>
        /// <param name="stream"></param>
        public ManagedStreamBufShim(Stream? stream)
        {
            _actualStream = stream ?? throw new System.ArgumentNullException(nameof(stream));
            _managedStreamBuf = new ManagedStreamBuf(Read, Write, FlushImpl, Seek);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Dispose managed resources here
                _innerStream.Dispose();
                _managedStreamBuf.Dispose();
            }
            // Dispose unmanaged resources here (if any)
            base.Dispose(disposing);
        }

        private int Read(byte[] buffer, int count)
        {
            try
            {
                return _actualStream.Read(buffer, 0, count);
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
                _actualStream.Write(buffer, 0, count);
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
                var ret = _actualStream.Seek(offset, (SeekOrigin) origin);
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
                _actualStream.Flush();
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
        /// <param name="managedStreamBufShim"></param>
        /// <returns></returns>
        public static implicit operator StreamBufHandle(ManagedStreamBufShim managedStreamBufShim)
        {
            return managedStreamBufShim._managedStreamBuf;
        }

        public override void Flush()
        {
            _managedStreamBuf.Flush();
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
            return _actualStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _actualStream.SetLength(value);
        }

        public override bool CanRead => _actualStream.CanRead;
        public override bool CanSeek => _actualStream.CanSeek;
        public override bool CanWrite => _actualStream.CanWrite;
        public override long Length => _actualStream.Length;

        public override long Position
        {
            get => _actualStream.Position;
            set => _actualStream.Position = value;
        }
    }
}
