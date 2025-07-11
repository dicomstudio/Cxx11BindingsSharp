using System;
using System.IO;
using Microsoft.Win32.SafeHandles;

namespace DicomStudio.Cxx11
{
    /// <summary>
    /// SafeHandle for a C++ streambuf.
    /// </summary>
    public sealed class Cxx11FileBufHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        /// <summary>
        /// default constructor
        /// </summary>
        public Cxx11FileBufHandle() : base(ownsHandle: true)
        {
        }

        /// <summary>
        /// release the handle
        /// </summary>
        /// <returns>true</returns>
        protected override bool ReleaseHandle()
        {
            NativeLibrary.cxx11_filebuf_delete(handle);
            return true;
        }
    }

    /// <summary>
    /// Concreate Stream implementation using c++ std::fiebuf.
    /// </summary>
    internal sealed class Cxx11FileBufShim : Stream, IDisposable
    {
        private const int DefaultBufferSize = 4096;
        private const FileShare DefaultShare = FileShare.Read;
        private const bool DefaultIsAsync = false;

        private readonly Cxx11FileBufHandle _handle;

        public Cxx11FileBufShim(string path, FileMode mode)
            : this(path, mode, mode == FileMode.Append ? FileAccess.Write : FileAccess.ReadWrite, DefaultShare,
                DefaultBufferSize, DefaultIsAsync)
        {
        }

        public Cxx11FileBufShim(string path, FileMode mode, FileAccess access)
            : this(path, mode, access, DefaultShare, DefaultBufferSize, DefaultIsAsync)
        {
        }

        public Cxx11FileBufShim(string path, FileMode mode, FileAccess access, FileShare share)
            : this(path, mode, access, share, DefaultBufferSize, DefaultIsAsync)
        {
        }

        public Cxx11FileBufShim(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize)
            : this(path, mode, access, share, bufferSize, DefaultIsAsync)
        {
        }

        public Cxx11FileBufShim(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize,
            bool useAsync)
            : this(path, mode, access, share, bufferSize, useAsync ? FileOptions.Asynchronous : FileOptions.None)
        {
        }

        public Cxx11FileBufShim(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize,
            FileOptions options)
            : this(path, mode, access, share, bufferSize, options, 0)
        {
        }

        private Cxx11FileBufShim(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize,
            FileOptions options, long preallocationSize)
        {
#if WINDOWS
            _handle = NativeLibrary.cxx11_filebuf_create2(path, mode, access, share);
#elif LINUX
            _handle = NativeLibrary.cxx11_filebuf_create1(path, mode, access, share);
#else
#error This platform is not supported
#endif
            if (!_handle.IsInvalid) return;
            if (mode == FileMode.Open && access == FileAccess.Read)
                throw new FileNotFoundException("not found");
            throw new ArgumentException("Invalid parameters");
        }

        public static Cxx11FileBufShim OpenRead(string path)
        {
            return new Cxx11FileBufShim(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        public static Cxx11FileBufShim OpenWrite(string path)
        {
            return new Cxx11FileBufShim(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        }

        /// <summary>
        /// Dispose implementation
        /// </summary>
        public new void Dispose()
        {
            _handle.Dispose();
        }

        /// <inheritdoc />
        public override void Flush()
        {
            var ret = NativeLibrary.cxx11_filebuf_flush(_handle);
            if (ret < 0)
            {
                throw new IOException("Failed to flush the stream buffer.");
            }
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            var ret = NativeLibrary.cxx11_filebuf_read(_handle, buffer, buffer.Length, offset, count);
            if (ret < 0)
            {
                throw new IOException("io");
            }

            return ret;
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin)
        {
            return NativeLibrary.cxx11_filebuf_seek(_handle, offset, origin);
        }

        /// <inheritdoc />
        public override void SetLength(long value)
        {
            var ret = NativeLibrary.cxx11_filebuf_set_length(_handle, value);
            if (ret < 0)
            {
                throw new NotSupportedException("Failed setLength");
            }
        }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            var ret = NativeLibrary.cxx11_filebuf_write(_handle, buffer, buffer.Length, offset, count);
            if (ret < 0)
            {
                throw new IOException("io");
            }
        }

        /// <inheritdoc />
        public override bool CanRead => NativeLibrary.cxx11_filebuf_can_read(_handle) == 0;

        /// <inheritdoc />
        public override bool CanSeek => NativeLibrary.cxx11_filebuf_can_seek(_handle) == 0;

        /// <inheritdoc />
        public override bool CanWrite => NativeLibrary.cxx11_filebuf_can_write(_handle) == 0;

        /// <inheritdoc />
        public override long Length
        {
            get
            {
                var ret = NativeLibrary.cxx11_filebuf_get_length(_handle);
                if (ret < 0)
                {
                    throw new NotSupportedException("not");
                }

                return ret;
            }
        }

        /// <inheritdoc />
        public override long Position
        {
            get
            {
                var ret = NativeLibrary.cxx11_filebuf_get_position(_handle);
                if (ret < 0)
                {
                    throw new IOException("io");
                }

                return ret;
            }
            set
            {
                var ret = NativeLibrary.cxx11_filebuf_set_position(_handle, value);
                if (ret < 0)
                {
                    throw new NotSupportedException("Failed to set position");
                }
            }
        }
    }
}
