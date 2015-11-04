// EasyHook (File: EasyHookDll\file.c)
//
// Copyright (c) 2009 Christoph Husse & Copyright (c) 2015 Justin Stenning
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
// Please visit https://easyhook.github.io for more information
// about the project and latest updates.

#include "stdafx.h"

BOOL RtlFileExists(WCHAR* InPath)
{
    HANDLE          hFile;

    if((hFile = CreateFileW(InPath, 0, FILE_SHARE_READ | FILE_SHARE_WRITE, NULL, OPEN_EXISTING, 0, NULL)) == INVALID_HANDLE_VALUE)
        return FALSE;

    CloseHandle(hFile);

    return TRUE;
}

LONG RtlGetWorkingDirectory(WCHAR* OutPath, ULONG InMaxLength)
{
    NTSTATUS            NtStatus;
    LONG            Index;

    Index = GetModuleFileName(NULL, OutPath, InMaxLength);

    if(GetLastError() == ERROR_INSUFFICIENT_BUFFER)
        THROW(STATUS_BUFFER_TOO_SMALL, L"The given buffer is too small.");

    // remove file name...
    for(Index--; Index >= 0; Index--)
    {
        if(OutPath[Index] == '\\')
        {
            OutPath[Index + 1] = 0;

            break;
        }
    }

    RETURN;

THROW_OUTRO:
FINALLY_OUTRO:
    return NtStatus;
}

LONG RtlGetCurrentModulePath(WCHAR* OutPath, ULONG InMaxLength)
{
    NTSTATUS            NtStatus;

    GetModuleFileName(hCurrentModule, OutPath, InMaxLength);

    if(GetLastError() == ERROR_INSUFFICIENT_BUFFER)
        THROW(STATUS_BUFFER_TOO_SMALL, L"The given buffer is too small.");

    RETURN;

THROW_OUTRO:
FINALLY_OUTRO:
    return NtStatus;
}
