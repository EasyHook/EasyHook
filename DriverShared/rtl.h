// EasyHook (File: EasyHookDll\rtl.h)
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

#ifndef _EASYHOOK_RTL_
#define _EASYHOOK_RTL_

#ifdef __cplusplus
extern "C"{
#endif

#include "stdafx.h"

#if _DEBUG
    #define DEBUGMSG(...) do { WCHAR debugMsg[1024] = { 0 }; _snwprintf_s(debugMsg, 1024, _TRUNCATE, __VA_ARGS__); OutputDebugStringW(debugMsg); } while(0)
#else
    #define DEBUGMSG(__VA_ARGS__) do { } while(0)
#endif

#ifndef DRIVER
	#define ASSERT(expr, Msg)            RtlAssert((BOOL)(expr),(LPCWSTR) Msg);
	#define THROW(code, Msg)        { NtStatus = (code); RtlSetLastError(GetLastError(), NtStatus, Msg); goto THROW_OUTRO; }
#else
#pragma warning(disable: 4005)
    #define ASSERT( exp, Msg )           ((!(exp)) ? (RtlAssert(#exp, __FILE__, __LINE__, NULL), FALSE) : TRUE)
#pragma warning(default: 4005)
#define THROW(code, Msg)        { NtStatus = (code); RtlSetLastError(NtStatus, NtStatus, Msg); goto THROW_OUTRO; }
#endif

#define RETURN                      { RtlSetLastError(STATUS_SUCCESS, STATUS_SUCCESS, L""); NtStatus = STATUS_SUCCESS; goto FINALLY_OUTRO; }
#define FORCE(expr)                 { if(!RTL_SUCCESS(NtStatus = (expr))) goto THROW_OUTRO; }
#define IsValidPointer				RtlIsValidPointer

#ifdef DRIVER

    typedef struct _RTL_SPIN_LOCK_
    {
        KSPIN_LOCK				Lock;  
        KIRQL                   OldIrql;
    }RTL_SPIN_LOCK;

#else

    typedef struct _RTL_SPIN_LOCK_
    {
        CRITICAL_SECTION        Lock;
        BOOL                 IsOwned;
    }RTL_SPIN_LOCK;

#endif

void RtlInitializeLock(RTL_SPIN_LOCK* InLock);

void RtlAcquireLock(RTL_SPIN_LOCK* InLock);

void RtlReleaseLock(RTL_SPIN_LOCK* InLock);

void RtlDeleteLock(RTL_SPIN_LOCK* InLock);

void RtlSleep(ULONG InTimeout);

void* RtlAllocateMemory(
            BOOL InZeroMemory, 
            ULONG InSize);

void RtlFreeMemory(void* InPointer);

#undef RtlCopyMemory
void RtlCopyMemory(
            PVOID InDest,
            PVOID InSource,
            ULONG InByteCount);

#undef RtlMoveMemory
BOOL RtlMoveMemory(
            PVOID InDest,
            PVOID InSource,
            ULONG InByteCount);

#undef RtlZeroMemory
void RtlZeroMemory(
            PVOID InTarget,
            ULONG InByteCount);

LONG RtlProtectMemory(
            void* InPointer, 
            ULONG InSize, 
            ULONG InNewProtection);

#ifndef DRIVER
	void RtlAssert(BOOL InAssert,LPCWSTR lpMessageText);
#endif

void RtlSetLastError(
            LONG InCode, 
            LONG InNtStatus,
            WCHAR* InMessage);

LONGLONG RtlAnsiHexToLongLong(
	const CHAR *s, 
	int len);

BOOL RtlAnsiDbgHexToLongLong(
            CHAR* InHexString,
            ULONG InMinStrLen,
            LONGLONG* OutValue);

LONG RtlAnsiSubString(
            CHAR* InString,
            ULONG InOffset,
            ULONG InCount,
            CHAR* InTarget,
            ULONG InTargetMaxLen);

LONG RtlAnsiIndexOf(
            CHAR* InString,
            CHAR InChar);

ULONG RtlAnsiLength(CHAR* InString);

ULONG RtlUnicodeLength(WCHAR* InString);

void RtlLongLongToUnicodeHex(LONGLONG InValue, WCHAR* InBuffer);

BOOL RtlIsValidPointer(PVOID InPtr, ULONG InSize);

#if X64_DRIVER
// Write Protection Off
KIRQL RtlWPOff();
//Write Protection On
void RtlWPOn(KIRQL irql);

#endif

#ifdef __cplusplus
}
#endif

#endif