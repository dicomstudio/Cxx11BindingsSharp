using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;

namespace Cxx11BindingsSharp.Tests
{
    [CollectionDefinition("Trace listener tests", DisableParallelization = true)]
    public class TraceListenerCollection
    {
    }

    [Collection("Trace listener tests")]
    public class TraceListenerTests
    {
        private sealed class ThrowingTraceListener : TraceListener
        {
            public bool Called { get; private set; }

            public override void Write(
#if NET6_0_OR_GREATER
                string? message
#else
                string message
#endif
            )
            {
                Called = true;
                throw new IOException("Trace listener failed");
            }

            public override void WriteLine(
#if NET6_0_OR_GREATER
                string? message
#else
                string message
#endif
            )
            {
                Called = true;
                throw new IOException("Trace listener failed");
            }
        }

        [Fact]
        public void ThrowingTraceListenerDoesNotEscapeNativeCallback()
        {
            var listener = new ThrowingTraceListener();
            Trace.Listeners.Add(listener);
            try
            {
                using (var stream = new C11StreamHandle(null, null, null,
                           self => throw new ExternalException("unmapped", unchecked((int) 0x81234567)), null))
                {
                    Assert.Equal((int) ErrorCodes.C11_E_UNMAPPED, NativeLibraryTests.test_c11_stream_flush(stream));
                    Assert.True(listener.Called);
                }
            }
            finally
            {
                Trace.Listeners.Remove(listener);
                listener.Dispose();
            }
        }
    }
}
