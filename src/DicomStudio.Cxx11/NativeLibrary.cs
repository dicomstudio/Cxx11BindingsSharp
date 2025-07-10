using System;
using System.Runtime.InteropServices;

namespace DicomStudio.Cxx11
{
    internal static class NativeLibrary
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        internal delegate int ReadDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        internal delegate int WriteDelegate(int count);

        [DllImport("Cxx11Sharp", CallingConvention = CallingConvention.Cdecl)]
        private static extern StreamBufHandle cxx11_managed_streambuf_create(IntPtr fillBuffer, IntPtr flushBuffer,
            [In] byte[] buffer, [In] int buffering);

        internal static StreamBufHandle managed_streambuf_create(ReadDelegate read, WriteDelegate write, byte[] buffer)
        {
            var fillPtr = Marshal.GetFunctionPointerForDelegate(read);
            var flushPtr = Marshal.GetFunctionPointerForDelegate(write);
            var ret = cxx11_managed_streambuf_create(fillPtr, flushPtr, buffer, buffer.Length);
            ret.Read = read;
            ret.Write = write;
            return ret;
        }

        [DllImport("Cxx11Sharp", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void cxx11_managed_streambuf_delete(IntPtr handle);

        [DllImport("Cxx11Sharp", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int cxx11_managed_streambuf_read_into(StreamBufHandle src, byte[] buffer, int count);

        [DllImport("Cxx11Sharp", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int cxx11_managed_streambuf_write_into(StreamBufHandle dst, byte[] buffer, int count);
    }
}
