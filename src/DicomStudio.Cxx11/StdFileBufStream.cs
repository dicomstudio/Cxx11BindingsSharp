using System;
using System.IO;
using Microsoft.Win32.SafeHandles;

namespace DicomStudio.Cxx11
{
    /// <summary>
    /// SafeHandle for a C++ streambuf.
    /// </summary>
    public sealed class StdFileBufHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        /// <summary>
        /// default constructor
        /// </summary>
        public StdFileBufHandle() : base(ownsHandle: true)
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
    /// Managed streambuf for C++.
    /// </summary>
    internal sealed class StdFileBufStream : Stream, IDisposable
    {
        private const int DefaultBufferSize = 4096;
        private const FileShare DefaultShare = FileShare.Read;
        private const bool DefaultIsAsync = false;

        private readonly StdFileBufHandle _handle;
        public StdFileBufStream(string path, FileMode mode)
            : this(path, mode, mode == FileMode.Append ? FileAccess.Write : FileAccess.ReadWrite, DefaultShare, DefaultBufferSize, DefaultIsAsync)
        {
        }

        public StdFileBufStream(string path, FileMode mode, FileAccess access)
            : this(path, mode, access, DefaultShare, DefaultBufferSize, DefaultIsAsync)
        {
        }

        public StdFileBufStream(string path, FileMode mode, FileAccess access, FileShare share)
            : this(path, mode, access, share, DefaultBufferSize, DefaultIsAsync)
        {
        }

        public StdFileBufStream(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize)
            : this(path, mode, access, share, bufferSize, DefaultIsAsync)
        {
        }

        public StdFileBufStream(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize, bool useAsync)
            : this(path, mode, access, share, bufferSize, useAsync ? FileOptions.Asynchronous : FileOptions.None)
        {
        }

        public StdFileBufStream(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize, FileOptions options)
            : this(path, mode, access, share, bufferSize, options, 0)
        {
        }
        private StdFileBufStream(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize, FileOptions options, long preallocationSize)
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

        public static StdFileBufStream OpenRead(string path)
        {
            return new StdFileBufStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        public static StdFileBufStream OpenWrite(string path)
        {
            return new StdFileBufStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
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
            if (ret == -1)
            {
                throw new IOException("Failed to flush the stream buffer.");
            }
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            return NativeLibrary.cxx11_filebuf_read(_handle, buffer, buffer.Length, offset, count);
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin)
        {
            return NativeLibrary.cxx11_filebuf_seek(_handle, offset, origin);
        }

        /// <inheritdoc />
        public override void SetLength(long value)
        {
            int ret = NativeLibrary.cxx11_filebuf_set_length(_handle, value);
            if( ret == -1)
            {
                throw new IOException("Failed setLength");
            }
        }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            NativeLibrary.cxx11_filebuf_write(_handle, buffer, buffer.Length, offset, count);
        }

        /// <inheritdoc />
        public override bool CanRead
        {
            get => NativeLibrary.cxx11_filebuf_can_read(_handle) == 0;
        }

        /// <inheritdoc />
        public override bool CanSeek
        {
            get => NativeLibrary.cxx11_filebuf_can_seek(_handle) == 0;
        }

        /// <inheritdoc />
        public override bool CanWrite
        {
            get => NativeLibrary.cxx11_filebuf_can_write(_handle) == 0;
        }

        /// <inheritdoc />
        public override long Length
        {
            get => NativeLibrary.cxx11_filebuf_get_length(_handle) ;
        }

        /// <inheritdoc />
        public override long Position
        {
            get => NativeLibrary.cxx11_filebuf_get_position(_handle);
            set => NativeLibrary.cxx11_filebuf_set_position(_handle, value);
        }
    }
}
