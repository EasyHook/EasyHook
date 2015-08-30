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

static PWCHAR           LastError = L"";
static ULONG            LastErrorCode = 0;

EASYHOOK_NT_EXPORT RtlGetLastError()
{
    return LastErrorCode;
}

PWCHAR RtlGetLastErrorString()
{
    return LastError;
}

PWCHAR RtlGetLastErrorStringCopy()
{
    // https://easyhook.codeplex.com/workitem/24958
    ULONG len = (ULONG)(wcslen(LastError)+1)*sizeof(TCHAR);
    PWCHAR pBuffer = (PWCHAR) CoTaskMemAlloc(len);
    CopyMemory(pBuffer, LastError, len);

    return pBuffer;
}

WCHAR* RtlErrorCodeToString(LONG InCode)
{
    switch(InCode)
    {
        case STATUS_SUCCESS: return L"STATUS_SUCCESS";
        case STATUS_NOT_SUPPORTED: return L"STATUS_NOT_SUPPORTED";
        case STATUS_INTERNAL_ERROR: return L"STATUS_INTERNAL_ERROR";
        case STATUS_PROCEDURE_NOT_FOUND: return L"STATUS_PROCEDURE_NOT_FOUND";
        case STATUS_NOINTERFACE: return L"STATUS_NOINTERFACE";
        case STATUS_INFO_LENGTH_MISMATCH: return L"STATUS_INFO_LENGTH_MISMATCH";
        case STATUS_BUFFER_TOO_SMALL: return L"STATUS_BUFFER_TOO_SMALL";
        case STATUS_INVALID_PARAMETER: return L"STATUS_INVALID_PARAMETER";
        case STATUS_INSUFFICIENT_RESOURCES: return L"STATUS_INSUFFICIENT_RESOURCES";
        case STATUS_UNHANDLED_EXCEPTION: return L"STATUS_UNHANDLED_EXCEPTION";
        case STATUS_NOT_FOUND: return L"STATUS_NOT_FOUND";
        case STATUS_NOT_IMPLEMENTED: return L"STATUS_NOT_IMPLEMENTED";
        case STATUS_ACCESS_DENIED: return L"STATUS_ACCESS_DENIED";
        case STATUS_ALREADY_REGISTERED: return L"STATUS_ALREADY_REGISTERED";
        case STATUS_WOW_ASSERTION: return L"STATUS_WOW_ASSERTION";
        case STATUS_BUFFER_OVERFLOW: return L"STATUS_BUFFER_OVERFLOW";
        case STATUS_DLL_INIT_FAILED: return L"STATUS_DLL_INIT_FAILED";
        case STATUS_INVALID_PARAMETER_1: return L"STATUS_INVALID_PARAMETER_1";
        case STATUS_INVALID_PARAMETER_2: return L"STATUS_INVALID_PARAMETER_2";
        case STATUS_INVALID_PARAMETER_3: return L"STATUS_INVALID_PARAMETER_3";
        case STATUS_INVALID_PARAMETER_4: return L"STATUS_INVALID_PARAMETER_4";
        case STATUS_INVALID_PARAMETER_5: return L"STATUS_INVALID_PARAMETER_5";
        case STATUS_INVALID_PARAMETER_6: return L"STATUS_INVALID_PARAMETER_6";
        case STATUS_INVALID_PARAMETER_7: return L"STATUS_INVALID_PARAMETER_7";
        case STATUS_INVALID_PARAMETER_8: return L"STATUS_INVALID_PARAMETER_8"; 
        default: return L"UNKNOWN";
    }
}

void RtlSetLastError(LONG InCode, NTSTATUS InNtStatus, WCHAR* InMessage)
{
    LastErrorCode = InCode;

    if(InMessage == NULL)
        LastError = L"";
    else
    {
#if _DEBUG
        // output to debugger
        if (lstrlenW(InMessage) > 0)
        {
            WCHAR msg[1024] = { 0 };
            LPVOID lpMsgBuf;

            if (InNtStatus == STATUS_SUCCESS) 
            {
                FormatMessage(
                    FORMAT_MESSAGE_ALLOCATE_BUFFER | 
                    FORMAT_MESSAGE_FROM_SYSTEM |
                    FORMAT_MESSAGE_IGNORE_INSERTS,
                    NULL,
                    InCode,
                    MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT),
                    (LPTSTR) &lpMsgBuf,
                    0, NULL );
                _snwprintf_s(msg, 1024, _TRUNCATE, L"%s (%s)", InMessage, lpMsgBuf);
            }
            else 
            {
                _snwprintf_s(msg, 1024, _TRUNCATE, L"%s (%s)", InMessage, RtlErrorCodeToString(InNtStatus));
            }
            DEBUGMSG(msg);

            LocalFree(lpMsgBuf);
        }
#endif
        LastError = (PWCHAR)InMessage;
    }
}

#ifndef DRIVER
	void RtlAssert(BOOL InAssert,LPCWSTR lpMessageText)
	{
		if(InAssert)
			return;

	#ifdef _DEBUG
		DebugBreak();
	#endif

			FatalAppExitW(0, lpMessageText);
		
	}
#endif