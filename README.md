# Welcome to EasyHook - The reinvention of Windows API Hooking

[![Join the chat at https://gitter.im/EasyHook/EasyHook](https://badges.gitter.im/EasyHook/EasyHook.svg)](https://gitter.im/EasyHook/EasyHook?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

This project supports extending (hooking) unmanaged code (APIs) with pure managed ones, from within a fully managed environment on 32- or 64-bit Windows XP SP2, Windows Vista x64, Windows Server 2008 x64, Windows 7, Windows 8.1, and Windows 10. 

EasyHook currently supports injecting assemblies built for .NET Framework 3.5 and 4.0 and can also inject native DLLs.

## Bug reports or questions
Reporting bugs is the only way to get them fixed and help other users of the library!

Report issues at: https://github.com/EasyHook/EasyHook/issues

## More information

For more information head to the EasyHook site https://easyhook.github.io

## License
    Copyright (c) 2009 Christoph Husse & Copyright (c) 2012 Justin Stenning

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:
    
    The above copyright notice and this permission notice shall be included in
    all copies or substantial portions of the Software.
    
    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.  IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
    THE SOFTWARE.

## External libraries
EasyHook includes the UDIS86 library Copyright (c) 2002-2012, Vivek Thampi <vivek.mt@gmail.com>. See .\DriverShared\Disassembler\udis86-LICENSE.txt for license details. Minor modifications have been made for it to compile with EasyHook.

More information about UDIS86 can be found at https://github.com/vmt/udis86
