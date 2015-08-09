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