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


UCHAR* GetTrampolinePtr();
ULONG GetTrampolineSize();

LOCAL_HOOK_INFO             GlobalHookListHead;
LOCAL_HOOK_INFO             GlobalRemovalListHead;
RTL_SPIN_LOCK               GlobalHookLock;
ULONG                       GlobalSlotList[MAX_HOOK_COUNT];
static LONG                 UniqueIDCounter = 0x10000000;

void LhCriticalInitialize()
{
/*
Description:
    
    Fail safe initialization of global hooking structures...
*/
    RtlZeroMemory(&GlobalHookListHead, sizeof(GlobalHookListHead));
    RtlZeroMemory(&GlobalRemovalListHead, sizeof(GlobalRemovalListHead));

    RtlInitializeLock(&GlobalHookLock);
}





EASYHOOK_BOOL_INTERNAL LhIsValidHandle(
            TRACED_HOOK_HANDLE InTracedHandle,
            PLOCAL_HOOK_INFO* OutHandle)
{
/*
Description:

    A handle is considered to be valid, if the whole structure
    points to valid memory AND the signature is valid AND the
    hook is installed!

*/
    if(!IsValidPointer(InTracedHandle, sizeof(HOOK_TRACE_INFO)))
        return FALSE;

    if(!IsValidPointer(InTracedHandle->Link, sizeof(LOCAL_HOOK_INFO)))
        return FALSE;

    if(InTracedHandle->Link->Signature != LOCAL_HOOK_SIGNATURE)
        return FALSE;

    if(!IsValidPointer(InTracedHandle->Link, InTracedHandle->Link->NativeSize))
        return FALSE;

    if(InTracedHandle->Link->HookProc == NULL)
        return FALSE;

    if(OutHandle != NULL)
        *OutHandle = InTracedHandle->Link;

    return TRUE;
}





EASYHOOK_NT_INTERNAL LhAllocateHook(
            void* InEntryPoint,
            void* InHookProc,
            void* InCallback,
            LOCAL_HOOK_INFO** OutHook,
            ULONG* RelocSize)
{
/*
Description:

    For internal use only, this method allocates a hook for the given 
    entry point, preparing the redirection of all calls to the given 
    hooking method. Upon completion the original entry point remains
    unchanged.
    
    Originally located within LhInstallHook, this code has been split 
    out to improve testing.

Parameters:

    - InEntryPoint

        An entry point to hook. Not all entry points are hookable. In such
        a case STATUS_NOT_SUPPORTED will be returned.

    - InHookProc

        The method that should be called instead of the given entry point.
        Please note that calling convention, parameter count and return value
        must EXACTLY match the original entry point!

    - InCallback

        An uninterpreted callback later available through
        LhBarrierGetCallback().

    - OutHook

        OutHook will point to a newly allocated Hook, with completed trampoline
        code including relocated entry point. The original entry point is still 
        unchanged at this point.

    - RelocSize

        Will contain the size of the entry point relocation instructions.

Returns:

    STATUS_NO_MEMORY
    
        Unable to allocate memory around the target entry point.
    
    STATUS_NOT_SUPPORTED
    
        The target entry point contains unsupported instructions.
    
    STATUS_INSUFFICIENT_RESOURCES
    
        The limit of MAX_HOOK_COUNT simultaneous hooks was reached.
    
*/

    ULONG           			EntrySize;
    LOCAL_HOOK_INFO*            Hook = NULL;
    LONGLONG          			RelAddr;
    UCHAR*                      MemoryPtr;
    LONG                        NtStatus = STATUS_INTERNAL_ERROR;

#if X64_DRIVER
	UCHAR			            Jumper_x64[12] = {0x48, 0xb8, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xe0};
#endif

#ifndef _M_X64
    ULONG                       Index;
    UCHAR*			            Ptr;
#endif

    // validate parameters
    if(!IsValidPointer(InEntryPoint, 1))
        THROW(STATUS_INVALID_PARAMETER_1, L"Invalid entry point.");

    if(!IsValidPointer(InHookProc, 1))
        THROW(STATUS_INVALID_PARAMETER_2, L"Invalid hook procedure.");

    // allocate memory for hook, for 64-bit this will be located within a 32-bit relative jump of entry point
	if((*OutHook = (LOCAL_HOOK_INFO*)LhAllocateMemory(InEntryPoint)) == NULL)
        THROW(STATUS_NO_MEMORY, L"Failed to allocate memory.");
    Hook = *OutHook;

    FORCE(RtlProtectMemory(Hook, 4096, PAGE_EXECUTE_READWRITE));

    MemoryPtr = (UCHAR*)(Hook + 1);

    // determine entry point size
#ifdef X64_DRIVER
	FORCE(EntrySize = LhRoundToNextInstruction(InEntryPoint, 12));
#else
    FORCE(EntrySize = LhRoundToNextInstruction(InEntryPoint, 5));
#endif

    // create and initialize hook handle
    Hook->NativeSize = sizeof(LOCAL_HOOK_INFO);
#if !_M_X64
    __pragma(warning(push))
    __pragma(warning(disable:4305))
#endif
    Hook->RandomValue = (void*)0x69FAB738962376EF;
#if !_M_X64
    __pragma(warning(pop))
#endif
        Hook->HookProc = (UCHAR*)InHookProc;
    Hook->TargetProc = (UCHAR*)InEntryPoint;
    Hook->EntrySize = EntrySize;	
    Hook->IsExecutedPtr = (int*)((UCHAR*)Hook + 2048);
    Hook->Callback = InCallback;
    *Hook->IsExecutedPtr = 0;

    /*
	    The following will be called by the trampoline before the user defined handler is invoked.
	    It will setup a proper environment for the hook handler which includes the "fiber deadlock barrier"
	    and user specific callback.
    */
    Hook->HookIntro = (PVOID)LhBarrierIntro;
    Hook->HookOutro = (PVOID)LhBarrierOutro;

    // copy trampoline
    Hook->Trampoline = MemoryPtr; 
    MemoryPtr += GetTrampolineSize();

    Hook->NativeSize += GetTrampolineSize();

    RtlCopyMemory(Hook->Trampoline, GetTrampolinePtr(), GetTrampolineSize());

    /*
	    Relocate entry point (the same for both archs)
	    Has to be written directly into the target buffer, because to
	    relocate RIP-relative addressing we need to know where the
	    instruction will go to...
    */
    *RelocSize = 0;
    Hook->OldProc = MemoryPtr; 

    FORCE(LhRelocateEntryPoint(Hook->TargetProc, EntrySize, Hook->OldProc, RelocSize));

    MemoryPtr += *RelocSize + 12;
    Hook->NativeSize += *RelocSize + 12;

    // add jumper to relocated entry point that will proceed execution in original method
#ifdef X64_DRIVER

	// absolute jumper
	RelAddr = Hook->TargetProc + Hook->EntrySize;

	RtlCopyMemory(Hook->OldProc + RelocSize, Jumper_x64, 12);
	RtlCopyMemory(Hook->OldProc + RelocSize + 2, &RelAddr, 8);

#else

	// relative jumper
    RelAddr = (LONGLONG)(Hook->TargetProc + Hook->EntrySize) - ((LONGLONG)Hook->OldProc + *RelocSize + 5);

	if(RelAddr != (LONG)RelAddr)
		THROW(STATUS_NOT_SUPPORTED, L"The given entry point is out of reach.");

    Hook->OldProc[*RelocSize] = 0xE9;

    RtlCopyMemory(Hook->OldProc + *RelocSize + 1, &RelAddr, 4);

#endif

    // backup original entry point
    Hook->TargetBackup = *((ULONGLONG*)Hook->TargetProc); 

#ifdef X64_DRIVER
	Hook->TargetBackup_x64 = *((ULONGLONG*)(Hook->TargetProc + 8)); 
#endif

#ifndef _M_X64

    /*
	    Replace absolute placeholders with proper addresses...
    */
    Ptr = Hook->Trampoline;

    for(Index = 0; Index < GetTrampolineSize(); Index++)
    {
    #pragma warning (disable:4311) // pointer truncation
	    switch(*((ULONG*)(Ptr)))
	    {
	    /*Handle*/			case 0x1A2B3C05: *((ULONG*)Ptr) = (ULONG)Hook; break;
	    /*UnmanagedIntro*/	case 0x1A2B3C03: *((ULONG*)Ptr) = (ULONG)Hook->HookIntro; break;
	    /*OldProc*/			case 0x1A2B3C01: *((ULONG*)Ptr) = (ULONG)Hook->OldProc; break;
	    /*Ptr:NewProc*/		case 0x1A2B3C07: *((ULONG*)Ptr) = (ULONG)&Hook->HookProc; break;
	    /*NewProc*/			case 0x1A2B3C00: *((ULONG*)Ptr) = (ULONG)Hook->HookProc; break;
	    /*UnmanagedOutro*/	case 0x1A2B3C06: *((ULONG*)Ptr) = (ULONG)Hook->HookOutro; break;
	    /*IsExecuted*/		case 0x1A2B3C02: *((ULONG*)Ptr) = (ULONG)Hook->IsExecutedPtr; break;
	    /*RetAddr*/			case 0x1A2B3C04: *((ULONG*)Ptr) = (ULONG)(Hook->Trampoline + 92); break;
	    }

	    Ptr++;
    }
#endif

    RETURN(STATUS_SUCCESS);

THROW_OUTRO:
FINALLY_OUTRO:
    {
        if(!RTL_SUCCESS(NtStatus))
        {
	        if(Hook != NULL)
	            LhFreeMemory(&Hook);
        }

        return NtStatus;
    }
}






EASYHOOK_NT_EXPORT LhInstallHook(
            void* InEntryPoint,
            void* InHookProc,
            void* InCallback,
            TRACED_HOOK_HANDLE OutHandle)
{
/*
Description:

    Installs a hook at the given entry point, redirecting all
    calls to the given hooking method. The returned handle will
    either be released on library unloading or explicitly through
    LhUninstallHook() or LhUninstallAllHooks().

Parameters:

    - InEntryPoint

        An entry point to hook. Not all entry points are hookable. In such
        a case STATUS_NOT_SUPPORTED will be returned.

    - InHookProc

        The method that should be called instead of the given entry point.
        Please note that calling convention, parameter count and return value
        shall match EXACTLY!

    - InCallback

        An uninterpreted callback later available through
        LhBarrierGetCallback().

    - OutPHandle

        The memory portion supplied by *OutHandle is expected to be preallocated
        by the caller. This structure is then filled by the method on success and
        must stay valid for hook-life time. Only if you explicitly call one of
        the hook uninstallation APIs, you can safely release the handle memory.

Returns:

    STATUS_NO_MEMORY
    
        Unable to allocate memory around the target entry point.
    
    STATUS_NOT_SUPPORTED
    
        The target entry point contains unsupported instructions.
    
    STATUS_INSUFFICIENT_RESOURCES
    
        The limit of MAX_HOOK_COUNT simultaneous hooks was reached.
    
*/
    LOCAL_HOOK_INFO*			Hook = NULL;
    ULONG                       Index;
    LONGLONG          			RelAddr;
    ULONG           			RelocSize;
    UCHAR			            Jumper[12] = {0xE9, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00};
    ULONGLONG                   AtomicCache;
    BOOL                        Exists;
    LONG                        NtStatus = STATUS_INTERNAL_ERROR;

#if X64_DRIVER
	UCHAR			            Jumper_x64[12] = {0x48, 0xb8, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xe0};
	ULONGLONG					AtomicCache_x64;
#endif

    // validate parameters
    if(!IsValidPointer(InEntryPoint, 1))
        THROW(STATUS_INVALID_PARAMETER_1, L"Invalid entry point.");

    if(!IsValidPointer(InHookProc, 1))
        THROW(STATUS_INVALID_PARAMETER_2, L"Invalid hook procedure.");

    if(!IsValidPointer(OutHandle, sizeof(HOOK_TRACE_INFO)))
        THROW(STATUS_INVALID_PARAMETER_4, L"The hook handle storage is expected to be allocated by the caller.");

    if(OutHandle->Link != NULL)
        THROW(STATUS_INVALID_PARAMETER_4, L"The given trace handle seems to already be associated with a hook.");

    // allocate hook and prepare trampoline / hook stub
    FORCE(LhAllocateHook(InEntryPoint, InHookProc, InCallback, &Hook, &RelocSize));
    
	// Prepare jumper from entry point to hook stub...
#if X64_DRIVER

	// absolute jumper
	RelAddr = (ULONGLONG)Hook->Trampoline;

	RtlCopyMemory(Jumper, Jumper_x64, 12);
	RtlCopyMemory(Jumper + 2, &RelAddr, 8);

#else

	// relative jumper
    RelAddr = (LONGLONG)Hook->Trampoline - ((LONGLONG)Hook->TargetProc + 5);

	if(RelAddr != (LONG)RelAddr)
		THROW(STATUS_NOT_SUPPORTED, L"The given entry point is out of reach.");

    RtlCopyMemory(Jumper + 1, &RelAddr, 4);

    FORCE(RtlProtectMemory(Hook->TargetProc, Hook->EntrySize, PAGE_EXECUTE_READWRITE));
#endif

    // register in global HLS list
    RtlAcquireLock(&GlobalHookLock);
    {
		Hook->HLSIdent = UniqueIDCounter++;

		Exists = FALSE;

        for(Index = 0; Index < MAX_HOOK_COUNT; Index++)
        {
	        if(GlobalSlotList[Index] == 0)
	        {
		        GlobalSlotList[Index] = Hook->HLSIdent;

		        Hook->HLSIndex = Index;

		        Exists = TRUE;

		        break;
	        }
        }
    }
    RtlReleaseLock(&GlobalHookLock);

	// ATTENTION: This must be the last THROW!!!!
    if(!Exists)
	    THROW(STATUS_INSUFFICIENT_RESOURCES, L"Not more than MAX_HOOK_COUNT hooks are supported simultaneously.");

    // from now on the unrecoverable code section starts...
#ifdef X64_DRIVER

	AtomicCache = *((ULONGLONG*)(Hook->TargetProc + 8));
    {
		RtlCopyMemory(&AtomicCache_x64, Jumper, 8);
	    RtlCopyMemory(&AtomicCache, Jumper + 8, 4);

		// backup entry point for later comparsion
	    Hook->HookCopy = AtomicCache_x64;
    }
	*((ULONGLONG*)(Hook->TargetProc + 0)) = AtomicCache_x64;
    *((ULONGLONG*)(Hook->TargetProc + 8)) = AtomicCache;

#else

    AtomicCache = *((ULONGLONG*)Hook->TargetProc);
    {
	    RtlCopyMemory(&AtomicCache, Jumper, 5);

	    // backup entry point for later comparsion
	    Hook->HookCopy = AtomicCache;
    }
    *((ULONGLONG*)Hook->TargetProc) = AtomicCache;

#endif

    /*
        Add hook to global list and return handle...
    */
    RtlAcquireLock(&GlobalHookLock);
    {
        Hook->Next = GlobalHookListHead.Next;
        GlobalHookListHead.Next = Hook;
    }
    RtlReleaseLock(&GlobalHookLock);

    Hook->Signature = LOCAL_HOOK_SIGNATURE;
    Hook->Tracking = OutHandle;
    OutHandle->Link = Hook;

    RETURN(STATUS_SUCCESS);

THROW_OUTRO:
FINALLY_OUTRO:
    {
        if(!RTL_SUCCESS(NtStatus))
        {
	        if(Hook != NULL)
	            LhFreeMemory(&Hook);
        }

        return NtStatus;
    }
}

/*////////////////////// GetTrampolineSize

DESCRIPTION:

	Will dynamically detect the size in bytes of the assembler code stored
	in "HookSpecifix_x##.asm".
*/
static ULONG ___TrampolineSize = 0;

#ifdef _M_X64
	EXTERN_C void __stdcall Trampoline_ASM_x64();
#else
	EXTERN_C void __stdcall Trampoline_ASM_x86();
#endif

UCHAR* GetTrampolinePtr()
{
// bypass possible Visual Studio debug jump table
#ifdef _M_X64
	UCHAR* Ptr = (UCHAR*)Trampoline_ASM_x64;
#else
	UCHAR* Ptr = (UCHAR*)Trampoline_ASM_x86;
#endif

	if(*Ptr == 0xE9)
		Ptr += *((int*)(Ptr + 1)) + 5;

#ifdef _M_X64
	return Ptr + 5 * 8;
#else
	return Ptr;
#endif
}

ULONG GetTrampolineSize()
{
    UCHAR*		Ptr = GetTrampolinePtr();
	UCHAR*		BasePtr = Ptr;
    ULONG       Signature;
    ULONG       Index;

	if(___TrampolineSize != 0)
		return ___TrampolineSize;
	
	// search for signature
	for(Index = 0; Index < 2000 /* some always large enough value*/; Index++)
	{
		Signature = *((ULONG*)Ptr);

		if(Signature == 0x12345678)	
		{
			___TrampolineSize = (ULONG)(Ptr - BasePtr);

			return ___TrampolineSize;
		}

		Ptr++;
	}

    ASSERT(FALSE,L"install.c - ULONG GetTrampolineSize()");

    return 0;
}