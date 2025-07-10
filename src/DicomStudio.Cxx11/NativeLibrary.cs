using System;
using System.Runtime.InteropServices;

namespace DicomStudio.Cxx11
{
    internal static class NativeLibrary
    {
        #region managed streambuf

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate int ReadDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate int WriteDelegate(int count);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate long SeekDelegate(long offset, int origin);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate int FlushDelegate();

        [DllImport("Cxx11Sharp", CallingConvention = CallingConvention.Cdecl)]
        private static extern StreamBufHandle cxx11_managed_streambuf_create(ReadDelegate readDelegate,
            WriteDelegate writeDelegate, FlushDelegate flushDelegate, SeekDelegate seekDelegate, [In] byte[] buffer,
            [In] int buffering);

        // synthatic sugar
        internal static StreamBufHandle managed_streambuf_create(ReadDelegate read, WriteDelegate write,
            Func<int> flushFunction, Func<long, int, long> seekFunction, byte[] buffer)
        {
            var flush = new FlushDelegate(flushFunction);
            var seek = new SeekDelegate(seekFunction);
            var ret = cxx11_managed_streambuf_create(read, write, flush, seek, buffer, buffer.Length);
            ret.Read = read;
            ret.Write = write;
            ret.Flush = flush;
            ret.Seek = seek;
            return ret;
        }

        [DllImport("Cxx11Sharp", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void cxx11_managed_streambuf_delete(IntPtr handle);

        [DllImport("Cxx11Sharp", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int cxx11_managed_streambuf_read_into(StreamBufHandle src, byte[] buffer, int count);

        [DllImport("Cxx11Sharp", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int cxx11_managed_streambuf_write_into(StreamBufHandle dst, byte[] buffer, int count);

        [DllImport("Cxx11Sharp", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int cxx11_managed_streambuf_flush(StreamBufHandle dst);

        [DllImport("Cxx11Sharp", CallingConvention = CallingConvention.Cdecl)]
        internal static extern long cxx11_managed_streambuf_test_size(StreamBufHandle src);

        #endregion
    }
}
