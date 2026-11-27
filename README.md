# Cxx11BindingsSharp

[![License](https://img.shields.io/badge/License-MIT--Clause-blue.svg)](https://raw.githubusercontent.com/dicomstudio/Cxx11BindingsSharp/main/LICENSE)
[![.NET](https://github.com/dicomstudio/Cxx11BindingsSharp/actions/workflows/dotnet.yml/badge.svg)](https://github.com/dicomstudio/Cxx11BindingsSharp/actions/workflows/dotnet.yml)
[![NuGet](https://img.shields.io/nuget/v/Cxx11BindingsSharp.svg)](https://www.nuget.org/packages/Cxx11BindingsSharp)

## Introduction

P/Invoke provides a set of marshaling capabilities for C-style APIs, but it does not support C++ Stream/APIs directly.

This library provides a thin layer to wrap a [
`System.IO.Stream`](https://learn.microsoft.com/fr-fr/dotnet/api/system.io.stream) in C#
to a portable C++ stream ([`std::streambuf`](https://en.cppreference.com/w/cpp/io/basic_streambuf.html)).
The intermediate C layer is exposed and can be used directly in a C11 project.
The custom `std::streambuf` implementation simply uses the portable C layer to call back into the concrete C# stream
implementation.

This is somewhat related to [
`System.Runtime.InteropServices.ComTypes.IStream`](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.comtypes.istream)
on windows.

## Typical usage in your library

Installation is:

```sh
dotnet add package Cxx11BindingsSharp
```

In your C# library, you can now write something like:

```cs
using Cxx11BindingsSharp;
public void AcmeConvert(System.IO.Stream src, System.IO.Stream dst)
{
  var srcWrap = new ManagedC11Stream(src);
  var dstWrap = new ManagedC11Stream(dst);
  if (NativeService.acme_convert(srcWrap, dstWrap)<0)
  {
    throw new ArgumentException($"Cannot convert");
  }
}
```

This will expose a C# Stream-like interface to your C11 library.

The only requirement is the following P/Invoke declaration in your C# code:

```cs
[DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl)]
internal static extern int acme_convert(C11StreamHandle src, C11StreamHandle dst);
```

Pass `C11StreamHandle` directly to P/Invoke rather than converting it to
`IntPtr`: the marshaller keeps the handle and callbacks alive until the call
returns and rejects a disposed handle. The implicit `IntPtr` conversions on
`C11StreamHandle` and `ManagedC11Stream` are no longer available; change
existing `IntPtr` P/Invoke parameters to `C11StreamHandle`.

If native code retains a pointer beyond the call, the owner must retain the
managed handle and use `DangerousAddRef` before obtaining the pointer with
`DangerousGetHandle`. Release that reference with `DangerousRelease` only after
native code has stopped using the pointer and all callbacks have completed.
Disposing a `ManagedC11Stream` also disposes its underlying stream, so it must
remain undisposed while native code uses it.

On the C11 side, export your function as:

```c
ACME_EXPORT int acme_convert(c11_stream* src, c11_stream* dst);
```

At this point you can either re-use one of the existing `std::streambuf` wrappers, or directly use the `c11_stream`
interface if you prefer to work with C-style APIs.

The C11 callbacks and example P/Invoke use the C calling convention. Managed
callback exceptions must not escape into native code: mapped HRESULTs are
translated to C11 error codes; unmapped HRESULTs (including OS-specific I/O
errors) are traced and reported as `C11_E_UNMAPPED`. C++ callers receive
`cxx11::callback_exception` for the extended error codes and can inspect
`code()` to distinguish them. Exception messages and OS error details are not
preserved by the C11 ABI.

## Running the C# tests on Windows

Initialize the `native` submodule and install CMake and Visual Studio C++ build
tools. The test project uses a Visual Studio CMake generator to build
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

The stream abstraction is simply defined with a set of five function pointers:

```c
struct c11_stream {
  read_fn read;
  write_fn write;
  seek_fn seek;
  flush_fn flush;
  trunc_fn trunc;
};
```

Where function declarations are:

```c
typedef int32_t (*read_fn)(struct c11_stream* self, byte* buffer, int32_t count);
typedef int32_t (*write_fn)(struct c11_stream* self, const byte* buffer, int32_t count);
typedef int64_t (*seek_fn)(struct c11_stream* self, int64_t off, int dir);
typedef int (*flush_fn)(struct c11_stream* self);
typedef int64_t (*trunc_fn)(struct c11_stream* self, int64_t size);
```

## See:

* https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.comtypes.istream
* https://learn.microsoft.com/en-us/dotnet/framework/interop/marshalling-classes-structures-and-unions
* https://stackoverflow.com/questions/6601355/passing-an-fstream-or-equivalent-from-c-sharp-to-c-through-cli

## References:

* https://github.com/dicomstudio/Cxx11Bindings
* https://www.nuget.org/packages/Cxx11BindingsSharp/
