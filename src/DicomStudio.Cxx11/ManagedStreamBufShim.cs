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

        // the managed streambuf handle (aka std::streambuf*)
        private readonly ManagedStreamBuf _managedStreamBuf;

        /// <summary>
        /// Buffered stream wrapper.
        /// </summary>
        /// <param name="stream"></param>
        public ManagedStreamBufShim(Stream? stream)
        {
            _innerStream = stream ?? throw new ArgumentNullException(nameof(stream));
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
                return _innerStream.Read(buffer, 0, count);
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
                _innerStream.Write(buffer, 0, count);
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
                var ret = _innerStream.Seek(offset, (SeekOrigin) origin);
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
                _innerStream.Flush();
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
            var actualCount = _managedStreamBuf.ReadInto(tmp);
            Array.Copy(tmp, 0, buffer, offset, actualCount);
            return actualCount;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            var tmp = new byte[count];
            Array.Copy(buffer, offset, tmp, 0, count);
            var ret = _managedStreamBuf.WriteInto(tmp);
            if (ret != count)
            {
                throw new IOException($"Failed to write all bytes. Expected {count}, but wrote {ret}.");
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return NativeLibrary.cxx11_managed_streambuf_seek(_managedStreamBuf, offset, origin);
        }

        public override void SetLength(long value)
        {
            _innerStream.SetLength(value);
        }

        public override long Length => NativeLibrary.cxx11_managed_streambuf_get_length(_managedStreamBuf);

        public override bool CanRead => _innerStream.CanRead;
        public override bool CanSeek => _innerStream.CanSeek;
        public override bool CanWrite => _innerStream.CanWrite;

        public override long Position
        {
            get => NativeLibrary.cxx11_managed_streambuf_get_position(_managedStreamBuf);
            set
            {
                var ret = NativeLibrary.cxx11_managed_streambuf_set_position(_managedStreamBuf, value);
                if (ret < 0)
                {
                    throw new NotSupportedException("Failed to set position in the stream buffer.");
                }
            }
        }
    }
}
