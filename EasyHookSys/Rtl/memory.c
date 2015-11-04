// EasyHook (File: EasyHookSys\memory.c)
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
	KeInitializeSpinLock(&OutLock->Lock);

	OutLock->OldIrql = PASSIVE_LEVEL;
}

void RtlAcquireLock(RTL_SPIN_LOCK* InLock)
{
	KeAcquireSpinLock(&InLock->Lock, &InLock->OldIrql);
}

void RtlReleaseLock(RTL_SPIN_LOCK* InLock)
{
	KeReleaseSpinLock(&InLock->Lock, InLock->OldIrql);
}

void RtlDeleteLock(RTL_SPIN_LOCK* InLock)
{
	RtlZeroMemory(InLock, sizeof(RTL_SPIN_LOCK));
}

void RtlSleep(ULONG InTimeout)
{
	KEVENT				Event;
	LARGE_INTEGER		DueTime;

	DueTime.QuadPart = -10 * 1000 * InTimeout;

	KeInitializeEvent(&Event, NotificationEvent, FALSE);

    KeWaitForSingleObject(&Event, Executive, KernelMode, FALSE, &DueTime);
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
    void*       Result = ExAllocatePoolWithTag(NonPagedPool, InSize, 'HOOK');

    if(InZeroMemory && (Result != NULL))
        RtlZeroMemory(Result, InSize);

    return Result;
}

LONG RtlProtectMemory(void* InPointer, ULONG InSize, ULONG InNewProtection)
{
    return STATUS_SUCCESS;
}

void RtlFreeMemory(void* InPointer)
{
	ExFreePool(InPointer);
}

BOOL RtlIsValidPointer(PVOID InPtr, ULONG InSize)
{
    if((InPtr == NULL) || (InPtr == (PVOID)~0))
        return FALSE;

    return TRUE;
}

#if X64_DRIVER
// Write Protection Off
KIRQL RtlWPOff()
{
	// prevent rescheduling 
	KIRQL irql = KeRaiseIrqlToDpcLevel();
	// disable memory protection (disable WP bit of CR0)   
	UINT64 cr0 = __readcr0();
	cr0 &= ~0x10000;
	__writecr0(cr0);
	// disable interrupts
	_disable();
	return irql;
}
//Write Protection On
void RtlWPOn(KIRQL irql)
{
	// re-enable memory protection (enable WP bit of CR0)   
	UINT64 cr0 = __readcr0();
	cr0 |= 0x10000;
	// enable interrupts
	_enable();
	__writecr0(cr0);
	// lower irql again
	KeLowerIrql(irql);
}
#endif