using System;
using System.IO;

namespace DicomStudio.Cxx11
{
    /// <summary>
    /// Dual personality (C# and C++) buffered stream wrapper. Provide a C++ std::streambuf interface for a managed Stream.
    /// Since it derives from <see cref="Stream"/>, it can be used in any place where a <see cref="Stream"/> is expected.
    /// </summary>
    public sealed class ManagedStreamBufShim : Stream
    {
        // the actual stream we are wrapping
        private readonly Stream _innerStream;

        // the managed stream handle
        private readonly C11StreamWrapper _managedStreamBuf;

        /// <summary>
        /// Buffered stream wrapper.
        /// </summary>
        /// <param name="stream"></param>
        public ManagedStreamBufShim(Stream? stream)
        {
            _innerStream = stream ?? throw new ArgumentNullException(nameof(stream));
            _managedStreamBuf = new C11StreamWrapper(FRead, FWrite, FSeek, FFlush, FTrunc);
        }

        private int FRead(byte[] buffer, uint count)
        {
            try
            {
                return _innerStream.Read(buffer, 0, (int) count);
            }
            catch (Exception ex)
            {
                return ex.HResult;
            }
        }

        private int FWrite(byte[] buffer, uint count)
        {
            try
            {
                _innerStream.Write(buffer, 0, (int) count);
                return (int) count;
            }
            catch (Exception ex)
            {
                return ex.HResult;
            }
        }

        private long FSeek(long off, int dir)
        {
            try
            {
                var ret = _innerStream.Seek(off, (SeekOrigin) dir);
                return ret;
            }
            catch (Exception ex)
            {
                return ex.HResult;
            }
        }

        private int FFlush()
        {
            try
            {
                _innerStream.Flush();
                return 0;
            }
            catch (Exception ex)
            {
                return ex.HResult;
            }
        }

        private int FTrunc(ulong length)
        {
            try
            {
                _innerStream.SetLength((long) length);
                return 0;
            }
            catch (Exception ex)
            {
                return ex.HResult;
            }
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

        public static implicit operator C11StreamWrapper(ManagedStreamBufShim managedStreamBufShim)
        {
            return managedStreamBufShim._managedStreamBuf;
        }

        public override void Flush()
        {
            _innerStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _innerStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _innerStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _innerStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _innerStream.Write(buffer, offset, count);
        }

        public override bool CanRead => _innerStream.CanRead;
        public override bool CanSeek => _innerStream.CanSeek;
        public override bool CanWrite => _innerStream.CanWrite;
        public override long Length => _innerStream.Length;

        public override long Position
        {
            get => _innerStream.Position;
            set => _innerStream.Position = value;
        }
    }
}
