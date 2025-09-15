# Cxx11BindingsSharp

[![License](https://img.shields.io/badge/License-MIT--Clause-blue.svg)](https://raw.githubusercontent.com/dicomstudio/Cxx11BindingsSharp/main/LICENSE)
[![.NET](https://github.com/dicomstudio/Cxx11BindingsSharp/actions/workflows/dotnet.yml/badge.svg)](https://github.com/dicomstudio/Cxx11BindingsSharp/actions/workflows/dotnet.yml)
[![NuGet](https://img.shields.io/nuget/v/Cxx11BindingsSharp.svg)](https://www.nuget.org/packages/Cxx11BindingsSharp)

## Introduction

Provide a thin layer to wrap a [`System.IO.Stream`](https://learn.microsoft.com/fr-fr/dotnet/api/system.io.stream) in C#
to a portable C++ stream ([`std::streambuf`](https://en.cppreference.com/w/cpp/io/basic_streambuf.html)).
The intermediate C layer is exposed and can be used directly in a C11 project.
The custom `std::streambuf` implementation simply uses the portable C layer to call back into the concrete C# stream implementation.

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

On the C11 side, export your function as:

```c
ACME_EXPORT int acme_convert(c11_stream* src, c11_stream* dst);
```

At this point you can either re-use one of the existing `std::streambuf` wrappers, or directly use the `c11_stream`
interface if you prefer to work with C-style APIs.

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
typedef int32_t (*read_fn)(byte* buffer, int32_t count);
typedef int32_t (*write_fn)(const byte* buffer, int32_t count);
typedef int64_t (*seek_fn)(int64_t off, int dir);
typedef int (*flush_fn)(void);
typedef int64_t (*trunc_fn)(int64_t size);
```

## See:

* https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.comtypes.istream
* https://learn.microsoft.com/en-us/dotnet/framework/interop/marshalling-classes-structures-and-unions
* https://stackoverflow.com/questions/6601355/passing-an-fstream-or-equivalent-from-c-sharp-to-c-through-cli

## References:

* https://github.com/dicomstudio/Cxx11Bindings
