# Cxx11BindingsSharp

[![License](https://img.shields.io/badge/License-MIT-blue.svg)](https://raw.githubusercontent.com/dicomstudio/Cxx11BindingsSharp/main/LICENSE)
[![.NET](https://github.com/dicomstudio/Cxx11BindingsSharp/actions/workflows/dotnet.yml/badge.svg)](https://github.com/dicomstudio/Cxx11BindingsSharp/actions/workflows/dotnet.yml)
[![NuGet](https://img.shields.io/nuget/v/Cxx11BindingsSharp.svg)](https://www.nuget.org/packages/Cxx11BindingsSharp)

## Introduction

P/Invoke supports C-style APIs but does not directly marshal C++ stream APIs.
Cxx11BindingsSharp wraps a .NET
[`System.IO.Stream`](https://learn.microsoft.com/en-us/dotnet/api/system.io.stream)
in a portable C interface. Native C code can use that interface directly, while
C++ code can use an adapter based on
[`std::streambuf`](https://en.cppreference.com/w/cpp/io/basic_streambuf.html).
Operations call back into the wrapped .NET stream.

The interface serves a similar purpose to
[`System.Runtime.InteropServices.ComTypes.IStream`](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.comtypes.istream)
on Windows.

## Usage

Install the package:

```sh
dotnet add package Cxx11BindingsSharp
```

For example, wrap the input and output streams when calling a native function:

```cs
using System;
using System.IO;
using Cxx11BindingsSharp;

public void AcmeConvert(Stream src, Stream dst)
{
  using var srcWrap = new ManagedC11Stream(src, leaveOpen: true);
  using var dstWrap = new ManagedC11Stream(dst, leaveOpen: true);
  if (NativeService.acme_convert(srcWrap, dstWrap) < 0)
  {
    throw new IOException("Conversion failed.");
  }
}
```

Declare the native function using `C11StreamHandle` parameters:

```cs
[DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl)]
internal static extern int acme_convert(C11StreamHandle src, C11StreamHandle dst);
```

`ManagedC11Stream` converts implicitly to `C11StreamHandle`. Pass the handle
directly to P/Invoke rather than converting it to `IntPtr`: the marshaller
keeps the handle and callbacks alive until the call returns and rejects a
disposed handle. The implicit `IntPtr` conversions on `C11StreamHandle` and
`ManagedC11Stream` are no longer available; change existing `IntPtr` P/Invoke
parameters to `C11StreamHandle`.

If native code retains a pointer beyond the call, the owner must retain the
managed handle and use `DangerousAddRef` before obtaining the pointer with
`DangerousGetHandle`. Release that reference with `DangerousRelease` only after
native code has stopped using the pointer and all callbacks have completed;
keep the managed handle reachable for that entire period.
Dispose each `ManagedC11Stream` after native code has stopped using it. By
default, disposing the wrapper also disposes the underlying stream; pass
`leaveOpen: true` to retain ownership of the underlying stream.

On the C side, include `cxx11bindings/stream.h` and export your function
using your library's export macro:

```c
ACME_EXPORT int acme_convert(struct c11_stream* src, struct c11_stream* dst);
```

Native code can use the `c11_stream` interface directly or use a C++
`std::streambuf` adapter from `cxx11bindings/streambuf.hxx`.

The callbacks and example P/Invoke use the C calling convention. The interface
is synchronous, may block, and does not guarantee thread safety. Native code
must not use a callback's buffer after the callback returns.

Managed callback exceptions are translated to C11 error codes. Unmapped
HRESULTs, including OS-specific I/O errors, are traced on a best-effort basis
and reported as `C11_E_UNMAPPED`, even if a trace listener throws. The C++ `cxx11::c_stream` adapter translates
negative error codes to `cxx11::stream_exception`; `code()` classifies the
error and `raw_code()` preserves its numeric value. Exception messages and
OS error details are not preserved by the C interface. On .NET 6 and later,
callback buffers are cleared before being returned to the shared array pool.

## Publishing

The `dotnet.yml` workflow publishes `v*` tags to GitHub Packages and nuget.org
after the Linux and Windows jobs succeed. For nuget.org, configure a trusted
publishing policy for `dicomstudio/Cxx11BindingsSharp` and `dotnet.yml`, and
set the GitHub Actions secret `NUGET_USER` to the policy owner's NuGet.org
username. The workflow exchanges a GitHub OIDC token for a short-lived NuGet
API key; it does not require a long-lived `NUGET_API_KEY` secret.

## Running the C# tests on Windows

Initialize the `native` submodule and install the .NET SDK, CMake, and Visual
Studio C++ build tools. The test project uses a Visual Studio CMake generator to build
`Cxx11BindingsTests.dll` and copies it to the test output automatically. From
the repository root, for example:

```powershell
git submodule update --init --recursive
dotnet test .\tests\Cxx11BindingsSharp.Tests\Cxx11BindingsSharp.Tests.csproj --framework net8.0 -p:Platform=x64
```

Use `-p:Platform=x86` for an x86 build. AnyCPU uses the x64 native library.
To reuse a separately built native test library instead, pass its absolute path
as `-p:NativeTestLibraryPath=<path>`. The library must match the test process
architecture. This override also works for the Linux test library.

To collect C# coverage, add `--collect "Code Coverage"` to the test command.
Visual Studio Enterprise can open the resulting `.coverage` file in
`tests\Cxx11BindingsSharp.Tests\TestResults`.

## Implementation details

The stream interface consists of five function pointers, defined in
`cxx11bindings/stream.h`:

```c
struct c11_stream {
  read_fn read;
  write_fn write;
  seek_fn seek;
  flush_fn flush;
  trunc_fn trunc;
};
```

Their signatures are:

```c
typedef int32_t (*read_fn)(struct c11_stream* self, byte* buffer, int32_t count);
typedef int32_t (*write_fn)(struct c11_stream* self, const byte* buffer, int32_t count);
typedef int64_t (*seek_fn)(struct c11_stream* self, int64_t off, int dir);
typedef int (*flush_fn)(struct c11_stream* self);
typedef int64_t (*trunc_fn)(struct c11_stream* self, int64_t size);
```

Read and write return the number of bytes transferred, or a negative error
code; short transfers are permitted. Seek and truncate return a position or
length on success, and flush returns zero on success. All three return a
negative error code on failure. See the native header for the complete
definitions and error codes.

## See also

* [Native Cxx11Bindings library](https://github.com/dicomstudio/Cxx11Bindings)
* [Cxx11BindingsSharp on NuGet](https://www.nuget.org/packages/Cxx11BindingsSharp/)
* [.NET marshalling of classes, structures, and unions](https://learn.microsoft.com/en-us/dotnet/framework/interop/marshalling-classes-structures-and-unions)
* [Passing a C++ stream from C#](https://stackoverflow.com/questions/6601355/passing-an-fstream-or-equivalent-from-c-sharp-to-c-through-cli)
