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
#ifndef _DRIVERSHARED_H_
#define _DRIVERSHARED_H_

#ifdef __cplusplus
extern "C"{
#endif

#pragma warning (disable:4054) // function/data conversion
#pragma warning (disable:4055) // data/function conversion
#pragma warning (disable:4200) // zero-sized array in struct/union
#pragma warning (disable:4204) // non-constant init
#pragma warning (disable:4100) // unreferenced parameter


#include "rtl.h"

#define EASYHOOK_NT_INTERNAL            EXTERN_C NTSTATUS __stdcall
#define EASYHOOK_BOOL_INTERNAL          EXTERN_C BOOL __stdcall

#define EASYHOOK_INJECT_MANAGED     0x00000001

typedef struct _NOTIFICATION_REQUEST_
{
	ULONG				MaxCount;
	ULONG				Count;
	ULONG				Entries[0];
}NOTIFICATION_REQUEST, *PNOTIFICATION_REQUEST;

typedef struct _HOOK_ACL_
{
	ULONG                   Count;
	BOOL                    IsExclusive;
	ULONG                   Entries[MAX_ACE_COUNT];
}HOOK_ACL;

#define LOCAL_HOOK_SIGNATURE            ((ULONG)0x6A910BE2)

typedef struct _LOCAL_HOOK_INFO_
{
    PLOCAL_HOOK_INFO        Next;
    ULONG					NativeSize;
	UCHAR*					TargetProc;
	ULONGLONG				TargetBackup;
	ULONGLONG				TargetBackup_x64;
	ULONGLONG				HookCopy;
	ULONG					EntrySize;
	UCHAR*					Trampoline;
    ULONG					HLSIndex;
	ULONG					HLSIdent;
	void*					Callback;
	HOOK_ACL				LocalACL;
    ULONG                   Signature;
    TRACED_HOOK_HANDLE      Tracking;

	void*					RandomValue; // fixed
	void*					HookIntro; // fixed
	UCHAR*					OldProc; // fixed
	UCHAR*					HookProc; // fixed
	void*					HookOutro; // fixed
	int*					IsExecutedPtr; // fixed
}LOCAL_HOOK_INFO, *PLOCAL_HOOK_INFO;


extern LOCAL_HOOK_INFO          GlobalHookListHead;
extern LOCAL_HOOK_INFO          GlobalRemovalListHead;
extern RTL_SPIN_LOCK            GlobalHookLock;
extern ULONG                    GlobalSlotList[];

EASYHOOK_BOOL_INTERNAL LhIsValidHandle(
            TRACED_HOOK_HANDLE InTracedHandle,
            PLOCAL_HOOK_INFO* OutHandle);

void LhCriticalInitialize();

void LhModuleInfoFinalize();

void LhCriticalFinalize();

void* LhAllocateMemory(void* InEntryPoint);

void LhFreeMemory(PLOCAL_HOOK_INFO* RefHandle);

HOOK_ACL* LhBarrierGetAcl();

ULONGLONG LhBarrierIntro(LOCAL_HOOK_INFO* InHandle, void* InRetAddr, void** InAddrOfRetAddr);

void* __stdcall LhBarrierOutro(LOCAL_HOOK_INFO* InHandle, void** InAddrOfRetAddr);

EASYHOOK_NT_INTERNAL LhAllocateHook(
            void* InEntryPoint,
            void* InHookProc,
            void* InCallback,
            LOCAL_HOOK_INFO** Hook,
            ULONG* RelocSize);

EASYHOOK_NT_INTERNAL LhDisassembleInstruction(
            void* InPtr, 
            ULONG* length, 
            PSTR buf, 
            LONG buffSize, 
            ULONG64 *nextInstr);

EASYHOOK_NT_INTERNAL LhRelocateRIPRelativeInstruction(
	        ULONGLONG InOffset,
	        ULONGLONG InTargetOffset,
            BOOL* OutWasRelocated);

EASYHOOK_NT_INTERNAL LhRelocateEntryPoint(
				UCHAR* InEntryPoint,
				ULONG InEPSize,
				UCHAR* Buffer,
				ULONG* OutRelocSize);

EASYHOOK_NT_INTERNAL LhRoundToNextInstruction(
			void* InCodePtr,
			ULONG InCodeSize);

EASYHOOK_NT_INTERNAL LhGetInstructionLength(void* InPtr);

/*
    Helper API
*/
EASYHOOK_NT_INTERNAL DbgCriticalInitialize();

EASYHOOK_NT_INTERNAL DbgCriticalFinalize();

#ifdef __cplusplus
}
#endif

#endif