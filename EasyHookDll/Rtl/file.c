/*
    EasyHook - The reinvention of Windows API hooking
 
    Copyright (C) 2009 Christoph Husse

    This library is free software; you can redistribute it and/or
    modify it under the terms of the GNU Lesser General Public
    License as published by the Free Software Foundation; either
    version 2.1 of the License, or (at your option) any later version.

    This library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
    Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public
    License along with this library; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301  USA

    Please visit http://www.codeplex.com/easyhook for more information
    about the project and latest updates.
*/
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
