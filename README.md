# Cxx11BindingsSharp

Provide a thin layer to wrap a `System.IO.Stream` in C# to a portable C++ stream (`std::streambuf`).
The intermediate C layer is exposed and can be used directly in a C11 project.
The custom streambuf implementation simply uses the portable C layer to call back into the C# stream.

This is somewhat related to `System.Runtime.InteropServices.ComTypes.IStream` on windows.

See:

* http://msdn.microsoft.com/en-us/library/system.runtime.interopservices.comtypes.istream.aspx
* https://stackoverflow.com/questions/6601355/passing-an-fstream-or-equivalent-from-c-sharp-to-c-through-cli
* https://stackoverflow.com/questions/6361314/pinvoke-and-istream
