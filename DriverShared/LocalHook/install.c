// EasyHook (File: EasyHookDll\install.c)
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

// Disable warning C4276: no prototype provided; assumed no parameters
// For ASM functions
#pragma warning(disable: 4276)

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


// The maximum number of bytes that can be used for a trampoline jump
// Under most circumstance the max is 8 bytes, for X64_DRIVER this
// increases to 16 bytes to support jumping to absolute addresses
#define MAX_JMP_SIZE 16

#if X64_DRIVER
// The size of the instructions for a jump in/out of the trampoline within a 64-bit driver
#define X64_DRIVER_JMPSIZE 16
// The offset for where the address should be copied to within the jump
#define X64_DRIVER_JMPADDR_OFFSET 3
#endif

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
	ULONG                       PageSize = 0;

#if X64_DRIVER
	// This is the ASM that will perform a JMP back out of the trampoline
	// Note that the address 0x0 will be replaced with appropriate address.
	// 50                             push   rax
	// 48 b8 00 00 00 00 00 00 00 00  mov rax, 0x0
	// 48 87 04 24                    xchg   QWORD PTR[rsp], rax
	// c3                             ret
	UCHAR			            Jumper_x64[X64_DRIVER_JMPSIZE] = { 
		0x50, 
		0x48, 0xb8, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
		0x48, 0x87, 0x04, 0x24,
		0xc3
	};
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

    // allocate memory for hook, for 64-bit non-driver this will be located within a 32-bit relative jump of entry point
    if ((*OutHook = (LOCAL_HOOK_INFO*)LhAllocateMemoryEx(InEntryPoint, &PageSize)) == NULL)
        THROW(STATUS_NO_MEMORY, L"Failed to allocate memory.");
    Hook = *OutHook;

    FORCE(RtlProtectMemory(Hook, PageSize, PAGE_EXECUTE_READWRITE));

    // Set MemoryPtr to end of LOCAL_HOOK_INFO structure where we will copy the trampoline and old proc
	MemoryPtr = (UCHAR*)(Hook + 1);

    // determine entry point size
#ifdef X64_DRIVER
	FORCE(EntrySize = LhRoundToNextInstruction(InEntryPoint, X64_DRIVER_JMPSIZE));
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

		The entry point code will be copied to the end of the trampoline
		with any relative addresses adjusted (if possible). Hook->OldProc
		points to this location.
    */
    *RelocSize = 0;
    Hook->OldProc = MemoryPtr; 

    FORCE(LhRelocateEntryPoint(Hook->TargetProc, EntrySize, Hook->OldProc, RelocSize));

	// Reserve enough room to fit worst case
	MemoryPtr += *RelocSize + MAX_JMP_SIZE;
	Hook->NativeSize += *RelocSize + MAX_JMP_SIZE;

    // add jumper to end of relocated entry point that will continue execution at 
	// the next instruction within the original method.
#ifdef X64_DRIVER

	// absolute jumper
	RelAddr = (LONGLONG)(Hook->TargetProc + Hook->EntrySize);

	RtlCopyMemory(Hook->OldProc + *RelocSize, Jumper_x64, X64_DRIVER_JMPSIZE);
	// Set address to be copied into RAX
	RtlCopyMemory(Hook->OldProc + *RelocSize + X64_DRIVER_JMPADDR_OFFSET, &RelAddr, 8);

#else

	// relative jumper
    RelAddr = (LONGLONG)(Hook->TargetProc + Hook->EntrySize) - ((LONGLONG)Hook->OldProc + *RelocSize + 5);

	if(RelAddr != (LONG)RelAddr)
		THROW(STATUS_NOT_SUPPORTED, L"The given entry point is out of reach.");

    Hook->OldProc[*RelocSize] = 0xE9;

    RtlCopyMemory(Hook->OldProc + *RelocSize + 1, &RelAddr, 4);

#endif

    // backup original entry point (8 bytes)
    Hook->TargetBackup = *((ULONGLONG*)Hook->TargetProc); 

#ifdef X64_DRIVER
	// 64-bit driver requires backup of additional 8 bytes supporting up to MAX_JMP_SIZE
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
    // This will contain the ASM that will perform a JMP into the trampoline
    UCHAR			            Jumper[MAX_JMP_SIZE] = { 0xE9, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
    ULONGLONG                   AtomicCache;
    BOOL                        Exists;
    LONG                        NtStatus = STATUS_INTERNAL_ERROR;

#if X64_DRIVER
	// This is the ASM that will perform a jump INTO the trampoline for X64_DRIVER
	// Note that the address 0x0 will be replaced with appropriate address.
	// 50                             push   rax
	// 48 b8 00 00 00 00 00 00 00 00  mov rax, 0x0
	// 48 87 04 24                    xchg   QWORD PTR[rsp], rax
	// c3                             ret
	UCHAR			            Jumper_x64[X64_DRIVER_JMPSIZE] = {
		0x50,
		0x48, 0xb8, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
		0x48, 0x87, 0x04, 0x24,
		0xc3
	};
	ULONGLONG					AtomicCache_x64;
	KIRQL						CurrentIRQL = PASSIVE_LEVEL;
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

	RtlCopyMemory(Jumper, Jumper_x64, X64_DRIVER_JMPSIZE);
	// Set address to be copied into RAX
	RtlCopyMemory(Jumper + X64_DRIVER_JMPADDR_OFFSET, &RelAddr, 8);

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
	    // Copy the second part of the Jumper
		RtlCopyMemory(&AtomicCache, Jumper + 8, X64_DRIVER_JMPSIZE - 8);

		// backup entry point for later comparison
	    Hook->HookCopy = AtomicCache_x64;
    }
	CurrentIRQL = KeGetCurrentIrql();
	RtlWPOff();
	*((ULONGLONG*)(Hook->TargetProc + 0)) = AtomicCache_x64;
    *((ULONGLONG*)(Hook->TargetProc + 8)) = AtomicCache;
	RtlWPOn(CurrentIRQL);

#else

    AtomicCache = *((ULONGLONG*)Hook->TargetProc);
    {
	    RtlCopyMemory(&AtomicCache, Jumper, 5);

	    // backup entry point for later comparison
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