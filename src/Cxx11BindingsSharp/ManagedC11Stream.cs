using System;
using System.IO;

namespace Cxx11BindingsSharp
{
    /// <summary>
    /// Dual personality (C# and C++) stream wrapper.
    /// Intention is to provide a C11 or C++11 std::streambuf interface of a managed Stream to a native library with minimal overhead.
    /// Since it derives from <see cref="Stream"/>, it can be used in any place where a <see cref="Stream"/> is expected.
    /// Intended use is for library author to expose a regular `System.IO.Stream` interface, but internally use a ManagedC11Stream
    /// as an implementation detail.
    /// </summary>
    public sealed class ManagedC11Stream : Stream
    {
        // the actual stream we are wrapping
        private readonly Stream _innerStream;

        // the managed stream handle
        private readonly C11StreamBufHandle _c11StreamBuf;

        /// <summary>
        /// Buffered stream wrapper.
        /// </summary>
        /// <param name="stream"></param>
        public ManagedC11Stream(Stream? stream)
        {
            _innerStream = stream ?? throw new ArgumentNullException(nameof(stream));
            // FIXME: should buffering depend on the inner stream's buffering?
            _c11StreamBuf = new C11StreamBufHandle(FRead, FWrite, FSeek, FFlush, FTrunc);
        }

        private int FRead(byte[] buffer, int count)
        {
            try
            {
                // may return a short-read
                return _innerStream.Read(buffer, 0, count);
            }
            catch (Exception ex)
            {
                return ex.HResult;
            }
        }

        private int FWrite(byte[] buffer, int count)
        {
            try
            {
                _innerStream.Write(buffer, 0, count);
                return count;
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

        private long FTrunc(long length)
        {
            try
            {
                _innerStream.SetLength(length);
                return length;
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
                _c11StreamBuf.Dispose();
            }

            // Dispose unmanaged resources here (if any)
            base.Dispose(disposing);
        }

        public static implicit operator C11StreamBufHandle(ManagedC11Stream managedC11Stream)
        {
            return managedC11Stream._c11StreamBuf;
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
