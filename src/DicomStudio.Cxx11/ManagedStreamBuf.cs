using System;
using System.IO;
using Microsoft.Win32.SafeHandles;

namespace DicomStudio.Cxx11
{
    /// <summary>
    /// SafeHandle for a C++ streambuf.
    /// </summary>
    public sealed class StreamBufHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        /// <summary>
        /// default constructor
        /// </summary>
        public StreamBufHandle() : base(ownsHandle: true)
        {
        }

        // It is important to store a reference to the delegates to prevent them from being garbage collected before _handle is released.
        // https://stackoverflow.com/questions/49466509/delegate-on-instance-method-passed-by-p-invoke
        internal NativeLibrary.ReadDelegate Read { get; set; }
        internal NativeLibrary.WriteDelegate Write { get; set; }
        internal NativeLibrary.FlushDelegate Flush { get; set; }
        internal NativeLibrary.SeekDelegate Seek { get; set; }

        /// <summary>
        /// release the handle
        /// </summary>
        /// <returns>true</returns>
        protected override bool ReleaseHandle()
        {
            NativeLibrary.cxx11_managed_streambuf_delete(handle);
            return true;
        }
    }

    /// <summary>
    /// Managed streambuf for C++.
    /// </summary>
    internal sealed class ManagedStreamBuf : IDisposable
    {
        // System.IO.Stream expects an input byte[] to read into,
        // since we do not want to go the `unsafe` road, create one here:
        // this also define the buffering (aka `setvbuf`) implicitly:
        // https://stackoverflow.com/questions/1862982/c-sharp-filestream-optimal-buffer-size-for-writing-large-files
        // System.IO.Stream uses this as default:
        // private const int DefaultCopyBufferSize = 81920;

        private readonly Func<byte[], int, int> _readFunction;
        private readonly Func<byte[], int, int> _writeFunction;
        private readonly byte[] _buffer;
        private readonly StreamBufHandle _handle;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="readFunction">function to fill buffer</param>
        /// <param name="writeFunction">function to flush buffer</param>
        /// <param name="flushFunction">function to flush buffer</param>
        /// <param name="seekFunction">function to flush buffer</param>
        /// <param name="buffering">size of buffering</param>
        public ManagedStreamBuf(Func<byte[], int, int> readFunction, Func<byte[], int, int> writeFunction,
            Func<int> flushFunction, Func<long, int, long> seekFunction,
            int buffering = 4096 /*FileStream.DefaultBufferSize*/)
        {
            _readFunction = readFunction;
            _writeFunction = writeFunction;
            _buffer = new byte[buffering];
            _handle = NativeLibrary.managed_streambuf_create(Read, Write, flushFunction, seekFunction, _buffer);

            if (_handle.IsInvalid)
                throw new ArgumentException("Invalid parameters");
        }

        private int Read()
        {
            if (_handle.IsClosed) return -1;
            return _readFunction(_buffer, _buffer.Length);
        }

        private int Write(int count)
        {
            // fwrite(0) is always valid even on read-only stream
            if (count == 0) return 0;
            if (_handle.IsClosed) return -1;
            return _writeFunction(_buffer, count);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="buffer"></param>
        /// <exception cref="IOException"></exception>
        public int ReadInto(byte[] buffer)
        {
            var ret = NativeLibrary.cxx11_managed_streambuf_read_into(_handle, buffer, buffer.Length);
            if (ret < 0)
            {
                throw new NotSupportedException("Failed to write to the stream buffer.");
            }

            return ret;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="buffer"></param>
        /// <exception cref="IOException"></exception>
        public int WriteInto(byte[] buffer)
        {
            var ret = NativeLibrary.cxx11_managed_streambuf_write_into(_handle, buffer, buffer.Length);
            if (ret < 0)
            {
                throw new NotSupportedException("Failed to write to the stream buffer.");
            }

            return ret;
        }

        /// <summary>
        /// Implicit conversion to StreamBufHandle.
        /// </summary>
        /// <param name="managedStreamBuf"></param>
        /// <returns></returns>
        public static implicit operator StreamBufHandle(ManagedStreamBuf managedStreamBuf)
        {
            return managedStreamBuf._handle;
        }

        /// <summary>
        /// Dispose implementation
        /// </summary>
        public void Dispose()
        {
            _handle.Dispose();
        }

        public void Flush()
        {
            var ret = NativeLibrary.cxx11_managed_streambuf_flush(_handle);
            if (ret < 0) throw new IOException("flush");
        }
    }
}
