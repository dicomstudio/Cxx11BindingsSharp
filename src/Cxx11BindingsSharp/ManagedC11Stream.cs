using System;
using System.IO;
using System.Threading;

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
        private readonly C11StreamHandle _c11Stream;
        private readonly bool _leaveOpen;
        private int _disposed;

        /// <summary>
        /// Wraps a stream and closes it when the wrapper is disposed.
        /// </summary>
        /// <param name="stream">The stream to wrap.</param>
        public ManagedC11Stream(
#if NET6_0_OR_GREATER
            Stream? stream
#else
            Stream stream
#endif
        ) : this(stream, false)
        {
        }

        /// <summary>
        /// Wraps a stream for managed and native access.
        /// </summary>
        /// <param name="stream">The stream to wrap.</param>
        /// <param name="leaveOpen">Whether to leave the underlying stream open when the wrapper is disposed.</param>
        public ManagedC11Stream(
#if NET6_0_OR_GREATER
            Stream? stream,
#else
            Stream stream,
#endif
            bool leaveOpen
        )
        {
            _innerStream = stream ?? throw new ArgumentNullException(nameof(stream));
            _leaveOpen = leaveOpen;
            // FIXME: should buffering depend on the inner stream's buffering?
            _c11Stream = new C11StreamHandle(FRead, FWrite, FSeek, FFlush, FTrunc);
        }

        private int FRead(IntPtr self, byte[] buffer, int count)
        {
            try
            {
                // may return a short-read
                return _innerStream.Read(buffer, 0, count);
            }
            catch (Exception ex)
            {
                return NativeLibrary.ErrorCodeMap.ToErrorCode(ex);
            }
        }

        private int FWrite(IntPtr self, byte[] buffer, int count)
        {
            try
            {
                _innerStream.Write(buffer, 0, count);
                return count;
            }
            catch (Exception ex)
            {
                return NativeLibrary.ErrorCodeMap.ToErrorCode(ex);
            }
        }

        public static SeekOrigin ToSeekOrigin(SeekDirs seekDir)
        {
#if NET6_0_OR_GREATER
            return seekDir switch
            {
                SeekDirs.Begin => SeekOrigin.Begin,
                SeekDirs.Current => SeekOrigin.Current,
                SeekDirs.End => SeekOrigin.End,
                _ => throw new ArgumentOutOfRangeException(nameof(seekDir), seekDir, "Invalid SeekDir value")
            };
#else
            switch (seekDir)
            {
                case SeekDirs.Begin:
                    return SeekOrigin.Begin;
                case SeekDirs.Current:
                    return SeekOrigin.Current;
                case SeekDirs.End:
                    return SeekOrigin.End;
                default:
                    throw new ArgumentOutOfRangeException(nameof(seekDir), seekDir, "Invalid SeekDir value");
            }
#endif
        }

        public static SeekDirs ToSeekDirs(SeekOrigin origin)
        {
#if NET6_0_OR_GREATER
            return origin switch
            {
                SeekOrigin.Begin => SeekDirs.Begin,
                SeekOrigin.Current => SeekDirs.Current,
                SeekOrigin.End => SeekDirs.End,
                _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, "Invalid SeekOrigin value")
            };
#else
            switch (origin)
            {
                case SeekOrigin.Begin:
                    return SeekDirs.Begin;
                case SeekOrigin.Current:
                    return SeekDirs.Current;
                case SeekOrigin.End:
                    return SeekDirs.End;
                default:
                    throw new ArgumentOutOfRangeException(nameof(origin), origin, "Invalid SeekOrigin value");
            }
#endif
        }

        private long FSeek(IntPtr self, long offset, int seekDir)
        {
            try
            {
                var seekOrigin = ToSeekOrigin((SeekDirs) seekDir);
                var ret = _innerStream.Seek(offset, seekOrigin);
                return ret;
            }
            catch (Exception ex)
            {
                return NativeLibrary.ErrorCodeMap.ToErrorCode(ex);
            }
        }

        private int FFlush(IntPtr self)
        {
            try
            {
                _innerStream.Flush();
                return 0;
            }
            catch (Exception ex)
            {
                return NativeLibrary.ErrorCodeMap.ToErrorCode(ex);
            }
        }

        private long FTrunc(IntPtr self, long length)
        {
            try
            {
                _innerStream.SetLength(length);
                return length;
            }
            catch (Exception ex)
            {
                return NativeLibrary.ErrorCodeMap.ToErrorCode(ex);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                try
                {
                    if (!_leaveOpen) _innerStream.Dispose();
                }
                finally
                {
                    _c11Stream.Dispose();
                    base.Dispose(disposing);
                }
                return;
            }

            base.Dispose(disposing);
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(ManagedC11Stream));
        }

        public static implicit operator C11StreamHandle(ManagedC11Stream managedC11Stream)
        {
            return managedC11Stream._c11Stream;
        }

        public override void Flush()
        {
            ThrowIfDisposed();
            _innerStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ThrowIfDisposed();
            return _innerStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            ThrowIfDisposed();
            return _innerStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            ThrowIfDisposed();
            _innerStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            ThrowIfDisposed();
            _innerStream.Write(buffer, offset, count);
        }

        public override bool CanRead => Volatile.Read(ref _disposed) == 0 && _innerStream.CanRead;
        public override bool CanSeek => Volatile.Read(ref _disposed) == 0 && _innerStream.CanSeek;
        public override bool CanWrite => Volatile.Read(ref _disposed) == 0 && _innerStream.CanWrite;
        public override long Length
        {
            get
            {
                ThrowIfDisposed();
                return _innerStream.Length;
            }
        }

        public override long Position
        {
            get
            {
                ThrowIfDisposed();
                return _innerStream.Position;
            }
            set
            {
                ThrowIfDisposed();
                _innerStream.Position = value;
            }
        }
    }
}
