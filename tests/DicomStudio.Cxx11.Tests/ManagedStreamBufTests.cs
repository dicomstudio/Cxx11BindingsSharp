using System;
using System.IO;
using Xunit;

namespace DicomStudio.Cxx11.Tests
{
    public class ManagedStreamBufTests
    {
        private static int ReadEmpty(byte[] buffer, int count)
        {
            return 0;
        }

        private static int WriteEmpty(byte[] buffer, int count)
        {
            return 0;
        }

        private static int ReadError(byte[] buffer, int count)
        {
            return -1;
        }

        private static int WriteError(byte[] buffer, int count)
        {
            return -1;
        }

    }
}
