// EasyHook (File: EasyHookDll\memory.c)
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

void RtlInitializeLock(RTL_SPIN_LOCK* OutLock)
{
    RtlZeroMemory(OutLock, sizeof(RTL_SPIN_LOCK));

    InitializeCriticalSection(&OutLock->Lock);
}

void RtlAcquireLock(RTL_SPIN_LOCK* InLock)
{
    EnterCriticalSection(&InLock->Lock);

    ASSERT(!InLock->IsOwned,L"memory.c - !InLock->IsOwned");

    InLock->IsOwned = TRUE;
}

void RtlReleaseLock(RTL_SPIN_LOCK* InLock)
{
    ASSERT(InLock->IsOwned,L"memory.c - InLock->IsOwned");

    InLock->IsOwned = FALSE;

    LeaveCriticalSection(&InLock->Lock);
}

void RtlDeleteLock(RTL_SPIN_LOCK* InLock)
{
    ASSERT(!InLock->IsOwned,L"memory.c - InLock->IsOwned");

    DeleteCriticalSection(&InLock->Lock);
}

void RtlSleep(ULONG InTimeout)
{
    Sleep(InTimeout);
}


void RtlCopyMemory(
            PVOID InDest,
            PVOID InSource,
            ULONG InByteCount)
{
    ULONG       Index;
    UCHAR*      Dest = (UCHAR*)InDest;
    UCHAR*      Src = (UCHAR*)InSource;

    for(Index = 0; Index < InByteCount; Index++)
    {
        *Dest = *Src;

        Dest++;
        Src++;
    }
}

BOOL RtlMoveMemory(
            PVOID InDest,
            PVOID InSource,
            ULONG InByteCount)
{
    PVOID       Buffer = RtlAllocateMemory(FALSE, InByteCount);

    if(Buffer == NULL)
        return FALSE;

    RtlCopyMemory(Buffer, InSource, InByteCount);
    RtlCopyMemory(InDest, Buffer, InByteCount);

    RtlFreeMemory(Buffer);
    return TRUE;
}

#ifndef _DEBUG
    #pragma optimize ("", off) // suppress _memset
#endif
void RtlZeroMemory(
            PVOID InTarget,
            ULONG InByteCount)
{
    ULONG           Index;
    UCHAR*          Target = (UCHAR*)InTarget;

    for(Index = 0; Index < InByteCount; Index++)
    {
        *Target = 0;

        Target++;
    }
}
#ifndef _DEBUG
    #pragma optimize ("", on) 
#endif


void* RtlAllocateMemory(BOOL InZeroMemory, ULONG InSize)
{
    void*       Result = 
#ifdef _DEBUG
        malloc(InSize);
#else
        HeapAlloc(hEasyHookHeap, 0, InSize);
#endif

    if(InZeroMemory && (Result != NULL))
        RtlZeroMemory(Result, InSize);

    return Result;
}

LONG RtlProtectMemory(void* InPointer, ULONG InSize, ULONG InNewProtection)
{
    DWORD       OldProtect;
    NTSTATUS            NtStatus;

    if(!VirtualProtect(InPointer, InSize, InNewProtection, &OldProtect))
        THROW(STATUS_INVALID_PARAMETER, L"Unable to make memory executable.")
    else
        RETURN;

THROW_OUTRO:
FINALLY_OUTRO:
    return NtStatus;
}

void RtlFreeMemory(void* InPointer)
{
	ASSERT(InPointer != NULL,L"InPointer != NULL");

#ifdef _DEBUG
    free(InPointer);
#else
    HeapFree(hEasyHookHeap, 0, InPointer);
#endif
}

LONG RtlInterlockedIncrement(LONG* RefValue)
{
    return InterlockedIncrement(RefValue);
}

BOOL RtlIsValidPointer(PVOID InPtr, ULONG InSize)
{
    if((InPtr == NULL) || (InPtr == (PVOID)~0))
        return FALSE;

    ASSERT(!IsBadReadPtr(InPtr, InSize),L"memory.c - !IsBadReadPtr(InPtr, InSize)");

    return TRUE;
}