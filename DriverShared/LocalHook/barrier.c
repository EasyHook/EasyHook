// EasyHook (File: EasyHookDll\barrier.c)
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

/*
I am using a >>well known<< library to check for OS loader lock ;-)
*/
#ifndef DRIVER
	#include "Aux_ulib.h"
#endif


typedef struct _RUNTIME_INFO_
{
	// "true" if the current thread is within the related hook handler
	BOOL            IsExecuting;
	// the hook this information entry belongs to... This allows a per thread and hook storage!
	DWORD           HLSIdent;
	// the return address of the current thread's hook handler...
	void*           RetAddress;
    // the address of the return address of the current thread's hook handler...
	void**          AddrOfRetAddr;
}RUNTIME_INFO;

typedef struct _THREAD_RUNTIME_INFO_
{
	RUNTIME_INFO*		Entries;
	RUNTIME_INFO*		Current;
	void*				Callback;
	BOOL				IsProtected;
}THREAD_RUNTIME_INFO, *LPTHREAD_RUNTIME_INFO;

typedef struct _THREAD_LOCAL_STORAGE_
{
    THREAD_RUNTIME_INFO		Entries[MAX_THREAD_COUNT];
    DWORD					IdList[MAX_THREAD_COUNT];
    RTL_SPIN_LOCK			ThreadSafe;
}THREAD_LOCAL_STORAGE;

typedef struct _BARRIER_UNIT_
{
	HOOK_ACL				GlobalACL;
	BOOL					IsInitialized;
	THREAD_LOCAL_STORAGE	TLS;
}BARRIER_UNIT;


static BARRIER_UNIT         Unit;

HOOK_ACL* LhBarrierGetAcl(){ return &Unit.GlobalACL; }



BOOL TlsAddCurrentThread(THREAD_LOCAL_STORAGE* InTls)
{
/*
Description:

    Tries to reserve a THREAD_RUNTIME_INFO entry for the calling thread.
    On success it may call TlsGetCurrentValue() to query a pointer to
    its private entry.

    This is a replacement for the Windows Thread Local Storage which seems
    to cause trouble when using it in Explorer.EXE for example.

    No parameter validation (for performance reasons).

    This method will raise an assertion if the thread was already added
    to the storage!

Parameters:
    - InTls

        The thread local storage to allocate from.

Returns:

    TRUE on success, FALSE otherwise.
*/
#ifndef DRIVER
	ULONG		CurrentId = (ULONG)GetCurrentThreadId();
#else
	ULONG		CurrentId = (ULONG)PsGetCurrentThreadId();
#endif
	LONG		Index = -1;
    LONG		i;

    RtlAcquireLock(&InTls->ThreadSafe);

    // select Index AND check whether thread is already registered.
	for(i = 0; i < MAX_THREAD_COUNT; i++)
	{
		if((InTls->IdList[i] == 0) && (Index == -1))
			Index = i;
		
		ASSERT(InTls->IdList[i] != CurrentId,L"barrier.c - InTls->IdList[i] != CurrentId");
	}

	if(Index == -1)
	{
		RtlReleaseLock(&InTls->ThreadSafe);

		return FALSE;
	}

	InTls->IdList[Index] = CurrentId;
	RtlZeroMemory(&InTls->Entries[Index], sizeof(THREAD_RUNTIME_INFO));
	
	RtlReleaseLock(&InTls->ThreadSafe);

	return TRUE;
}






BOOL TlsGetCurrentValue(
            THREAD_LOCAL_STORAGE* InTls,                
            THREAD_RUNTIME_INFO** OutValue)
{
/*
Description:

    Queries the THREAD_RUNTIME_INFO for the calling thread.
    The caller shall previously be added to the storage by
    using TlsAddCurrentThread().

Parameters:

    - InTls

        The storage where the caller is registered.

    - OutValue

        Is filled with a pointer to the caller's private storage entry.

Returns:

    FALSE if the caller was not registered in the storage, TRUE otherwise.
*/
#ifndef DRIVER
	ULONG		CurrentId = (ULONG)GetCurrentThreadId();
#else
	ULONG		CurrentId = (ULONG)PsGetCurrentThreadId();
#endif
    LONG        Index;

	for(Index = 0; Index < MAX_THREAD_COUNT; Index++)
	{
		if(InTls->IdList[Index] == CurrentId)
		{
			*OutValue = &InTls->Entries[Index];

			return TRUE;
		}
	}

	return FALSE;
}





void TlsRemoveCurrentThread(THREAD_LOCAL_STORAGE* InTls)
{
/*
Description:

    Removes the caller from the local storage. If the caller
    is already removed, the method will do nothing.

Parameters:

    - InTls

        The storage from which the caller should be removed.
*/
#ifndef DRIVER
	ULONG		    CurrentId = (ULONG)GetCurrentThreadId();
#else
	ULONG		    CurrentId = (ULONG)PsGetCurrentThreadId();
#endif
    ULONG           Index;

    RtlAcquireLock(&InTls->ThreadSafe);

	for(Index = 0; Index < MAX_THREAD_COUNT; Index++)
	{
		if(InTls->IdList[Index] == CurrentId)
		{
			InTls->IdList[Index] = 0;

			RtlZeroMemory(&InTls->Entries[Index], sizeof(THREAD_RUNTIME_INFO));
		}
	}

	RtlReleaseLock(&InTls->ThreadSafe);
}





BOOL IsLoaderLock()
{
/*
Returns:

    TRUE if the current thread hols the OS loader lock, or the library was not initialized
    properly. In both cases a hook handler should not be executed!

    FALSE if it is safe to execute the hook handler.

*/
#ifndef DRIVER
	BOOL     IsLoaderLock = FALSE;

	return (!AuxUlibIsDLLSynchronizationHeld(&IsLoaderLock) || IsLoaderLock || !Unit.IsInitialized);
#else
	return FALSE;
#endif
}




BOOL AcquireSelfProtection()
{
/*
Description:

    To provide more convenience for writing the TDB, this self protection
    will disable ALL hooks for the current thread until ReleaseSelfProtection() 
    is called. This allows one to call any API during TDB initialization
    without being intercepted...

Returns:

    TRUE if the caller's runtime info has been locked down.

    FALSE if the caller's runtime info already has been locked down
    or is not available. The hook handler should not be executed in
    this case!

*/
	LPTHREAD_RUNTIME_INFO		Runtime = NULL;

	if(!TlsGetCurrentValue(&Unit.TLS, &Runtime) || Runtime->IsProtected)
		return FALSE;

	Runtime->IsProtected = TRUE;

	return TRUE;
}





void ReleaseSelfProtection()
{
/*
Description:

    Exists the TDB self protection. Refer to AcquireSelfProtection() for more
    information.

    An assertion is raised if the caller has not owned the self protection.
*/
	LPTHREAD_RUNTIME_INFO		Runtime = NULL;

	ASSERT(TlsGetCurrentValue(&Unit.TLS, &Runtime) && Runtime->IsProtected,L"barrier.c - TlsGetCurrentValue(&Unit.TLS, &Runtime) && Runtime->IsProtected");

	Runtime->IsProtected = FALSE;
}






BOOL ACLContains(
	HOOK_ACL* InACL,
	ULONG InCheckID)
{
/*
Returns:

    TRUE if the given ACL contains the given ID, FALSE otherwise.
*/
    ULONG           Index;

	for(Index = 0; Index < InACL->Count; Index++)
	{
		if(InACL->Entries[Index] == InCheckID)
			return TRUE;
	}

	return FALSE;
}




#ifndef DRIVER
BOOL IsThreadIntercepted(
	HOOK_ACL* LocalACL, 
	ULONG InThreadID)
#else
BOOL IsProcessIntercepted(
	HOOK_ACL* LocalACL, 
	ULONG InProcessID)
#endif
{
/*
Description:

    Please refer to LhIsThreadIntercepted() for more information.

Returns:

    TRUE if the given thread is intercepted by the global AND local ACL,
    FALSE otherwise.
*/
	ULONG				CheckID;

#ifndef DRIVER
	if(InThreadID == 0)
		CheckID = GetCurrentThreadId();
	else
		CheckID = InThreadID;
#else
	if(InProcessID == 0)
		CheckID = (ULONG)PsGetCurrentProcessId();
	else
		CheckID = InProcessID;
#endif

	if(ACLContains(&Unit.GlobalACL, CheckID))
	{
		if(ACLContains(LocalACL, CheckID))
		{
			if(LocalACL->IsExclusive)
				return FALSE;
		}
		else
		{
			if(!LocalACL->IsExclusive)
				return FALSE;
		}

		return !Unit.GlobalACL.IsExclusive;
	}
	else
	{
		if(ACLContains(LocalACL, CheckID))
		{
			if(LocalACL->IsExclusive)
				return FALSE;
		}
		else
		{
			if(!LocalACL->IsExclusive)
				return FALSE;
		}

		return Unit.GlobalACL.IsExclusive;
	}
}





#ifndef DRIVER
EASYHOOK_NT_EXPORT LhIsThreadIntercepted(
	TRACED_HOOK_HANDLE InHook,
	ULONG InThreadID,
    BOOL* OutResult)
#else
EASYHOOK_NT_EXPORT LhIsProcessIntercepted(
	TRACED_HOOK_HANDLE InHook,
	ULONG InProcessID,
    BOOL* OutResult)
#endif
{
/*
Description:

    This method will negotiate whether a given thread passes
    the ACLs and would invoke the related hook handler. Refer
    to the source code of Is[Thread/Process]Intercepted() for more information
    about the implementation.

*/
    NTSTATUS            NtStatus;
    PLOCAL_HOOK_INFO    Handle;

    if(!LhIsValidHandle(InHook, &Handle))
        THROW(STATUS_INVALID_PARAMETER_1, L"The given hook handle is invalid or already disposed.");

    if(!IsValidPointer(OutResult, sizeof(BOOL)))
        THROW(STATUS_INVALID_PARAMETER_3, L"Invalid pointer for result storage.");

#ifndef DRIVER
    *OutResult = IsThreadIntercepted(&Handle->LocalACL, InThreadID);
#else
	*OutResult = IsProcessIntercepted(&Handle->LocalACL, InProcessID);
#endif

    RETURN;

THROW_OUTRO:
FINALLY_OUTRO:
    return NtStatus;
}





EASYHOOK_NT_EXPORT LhBarrierGetCallback(PVOID* OutValue)
{
/*
Description:

    Is expected to be called inside a hook handler. Otherwise it
    will fail with STATUS_NOT_SUPPORTED. The method retrieves
    the callback initially passed to the related LhInstallHook()
    call.

*/
    NTSTATUS            NtStatus;
	LPTHREAD_RUNTIME_INFO       Runtime;

    if(!IsValidPointer(OutValue, sizeof(PVOID)))
        THROW(STATUS_INVALID_PARAMETER, L"Invalid result storage specified.");

	if(!TlsGetCurrentValue(&Unit.TLS, &Runtime))
        THROW(STATUS_NOT_SUPPORTED, L"The caller is not inside a hook handler.");

	if(Runtime->Current != NULL)
		*OutValue = Runtime->Callback;
	else
		THROW(STATUS_NOT_SUPPORTED, L"The caller is not inside a hook handler.");

    RETURN;

THROW_OUTRO:
FINALLY_OUTRO:
    return NtStatus;
}






EASYHOOK_NT_EXPORT LhBarrierGetReturnAddress(PVOID* OutValue)
{
/*
Description:

    Is expected to be called inside a hook handler. Otherwise it
    will fail with STATUS_NOT_SUPPORTED. The method retrieves
    the return address of the hook handler. This is usually the
    instruction behind the "CALL" which invoked the hook.

    The calling module determination is based on this method.

*/
    NTSTATUS            NtStatus;
	LPTHREAD_RUNTIME_INFO       Runtime;

    if(!IsValidPointer(OutValue, sizeof(PVOID)))
        THROW(STATUS_INVALID_PARAMETER, L"Invalid result storage specified.");

	if(!TlsGetCurrentValue(&Unit.TLS, &Runtime))
        THROW(STATUS_NOT_SUPPORTED, L"The caller is not inside a hook handler.");

	if(Runtime->Current != NULL)
		*OutValue = Runtime->Current->RetAddress;
	else
		THROW(STATUS_NOT_SUPPORTED, L"The caller is not inside a hook handler.");

    RETURN;

THROW_OUTRO:
FINALLY_OUTRO:
    return NtStatus;
}





EASYHOOK_NT_EXPORT LhBarrierGetAddressOfReturnAddress(PVOID** OutValue)
{
/*
Description:

    Is expected to be called inside a hook handler. Otherwise it
    will fail with STATUS_NOT_SUPPORTED. The method retrieves
    the address of the return address of the hook handler. 
*/
	LPTHREAD_RUNTIME_INFO       Runtime;
    NTSTATUS                    NtStatus;

    if(OutValue == NULL)
        THROW(STATUS_INVALID_PARAMETER, L"Invalid storage specified.");

	if(!TlsGetCurrentValue(&Unit.TLS, &Runtime))
        THROW(STATUS_NOT_SUPPORTED, L"The caller is not inside a hook handler.");

	if(Runtime->Current != NULL)
        *OutValue = Runtime->Current->AddrOfRetAddr;
	else
		THROW(STATUS_NOT_SUPPORTED, L"The caller is not inside a hook handler.");

    RETURN;

THROW_OUTRO:
FINALLY_OUTRO:
    return NtStatus;
}





EASYHOOK_NT_EXPORT LhBarrierBeginStackTrace(PVOID* OutBackup)
{
/*
Description:

    Is expected to be called inside a hook handler. Otherwise it
    will fail with STATUS_NOT_SUPPORTED. 
    Temporarily restores the call stack to allow stack traces.

    You have to pass the stored backup pointer to 
    LhBarrierEndStackTrace() BEFORE leaving the handler, otherwise
    the application will be left in an unstable state!
*/
    NTSTATUS                    NtStatus;
    LPTHREAD_RUNTIME_INFO       Runtime;

    if(OutBackup == NULL)
        THROW(STATUS_INVALID_PARAMETER, L"The given backup storage is invalid.");

	if(!TlsGetCurrentValue(&Unit.TLS, &Runtime))
        THROW(STATUS_NOT_SUPPORTED, L"The caller is not inside a hook handler.");

	if(Runtime->Current == NULL)
		THROW(STATUS_NOT_SUPPORTED, L"The caller is not inside a hook handler.");

    *OutBackup = *Runtime->Current->AddrOfRetAddr;
    *Runtime->Current->AddrOfRetAddr = Runtime->Current->RetAddress;

    RETURN;

THROW_OUTRO:
FINALLY_OUTRO:
    return NtStatus;
}






EASYHOOK_NT_EXPORT LhBarrierEndStackTrace(PVOID InBackup)
{
/*
Description:

    Is expected to be called inside a hook handler. Otherwise it
    will fail with STATUS_NOT_SUPPORTED. 

    You have to pass the backup pointer obtained with
    LhBarrierBeginStackTrace().
*/
    NTSTATUS            NtStatus;
    PVOID*              AddrOfRetAddr;

    if(!IsValidPointer(InBackup, 1))
        THROW(STATUS_INVALID_PARAMETER, L"The given stack backup pointer is invalid.");

    FORCE(LhBarrierGetAddressOfReturnAddress(&AddrOfRetAddr));

    *AddrOfRetAddr = InBackup;

    RETURN;

THROW_OUTRO:
FINALLY_OUTRO:
    return NtStatus;
}






void LhBarrierThreadDetach()
{
/*
Description:

    Will be called on thread termination and cleans up the TLS.
*/
	LPTHREAD_RUNTIME_INFO		Info;

	if(TlsGetCurrentValue(&Unit.TLS, &Info))
	{
		if(Info->Entries != NULL)
			RtlFreeMemory(Info->Entries);

		Info->Entries = NULL;
	}

	TlsRemoveCurrentThread(&Unit.TLS);
}




#ifdef DRIVER
void OnThreadDetach(
			IN HANDLE  ProcessId,
			IN HANDLE  ThreadId,
			IN BOOLEAN  Create)
{
	if(Create)
		return;

	LhBarrierThreadDetach();
}
#endif





NTSTATUS LhBarrierProcessAttach()
{
/*
Description:

    Will be called on DLL load and initializes all barrier structures.
*/
	RtlZeroMemory(&Unit, sizeof(Unit));

	// globally accept all threads...
	Unit.GlobalACL.IsExclusive = TRUE;

	// allocate private heap
    RtlInitializeLock(&Unit.TLS.ThreadSafe);

#ifndef DRIVER

    Unit.IsInitialized = AuxUlibInitialize()?TRUE:FALSE;

	return STATUS_SUCCESS;

#else

	// we also have to emulate a thread detach event...
	Unit.IsInitialized = TRUE;

	return PsSetCreateThreadNotifyRoutine(OnThreadDetach);

#endif
}

void LhBarrierProcessDetach()
{
/*
Description:

    Will be called on DLL unload.
*/
	ULONG			Index;

#ifdef DRIVER
	PsRemoveCreateThreadNotifyRoutine(OnThreadDetach);
#endif

	RtlDeleteLock(&Unit.TLS.ThreadSafe);

	// release thread specific resources
	for(Index = 0; Index < MAX_THREAD_COUNT; Index++)
	{
		if(Unit.TLS.Entries[Index].Entries != NULL)
			RtlFreeMemory(Unit.TLS.Entries[Index].Entries);
	}

	RtlZeroMemory(&Unit, sizeof(Unit));
}




ULONGLONG LhBarrierIntro(LOCAL_HOOK_INFO* InHandle, void* InRetAddr, void** InAddrOfRetAddr)
{
/*
Description:

    Will be called from assembler code and enters the 
    thread deadlock barrier.
*/
    LPTHREAD_RUNTIME_INFO		Info;
    RUNTIME_INFO*		        Runtime;
	BOOL						Exists;

	#ifdef _M_X64
		InHandle -= 1;
	#endif

	// are we in OS loader lock?
	if(IsLoaderLock())
	{
		/*
			Execution of managed code or even any other code within any loader lock
			may lead into unpredictable application behavior and therefore we just
			execute without intercepting the call...
		*/

		/*  !!Note that the assembler code does not invoke LhBarrierOutro() in this case!! */

		return FALSE;
	}

	// open pointer table
	Exists = TlsGetCurrentValue(&Unit.TLS, &Info);

	if(!Exists)
	{
		if(!TlsAddCurrentThread(&Unit.TLS))
			return FALSE;
	}

	/*
		To minimize APIs that can't be hooked, we are now entering the self protection.
		This will allow anybody to hook any APIs except those required to setup
		self protection.

		Self protection prevents any further hook interception for the current fiber,
		while setting up the "Thread Deadlock Barrier"...
	*/
	if(!AcquireSelfProtection())
	{
		/*  !!Note that the assembler code does not invoke LhBarrierOutro() in this case!! */

		return FALSE;
	}

	ASSERT(InHandle->HLSIndex < MAX_HOOK_COUNT,L"barrier.c - InHandle->HLSIndex < MAX_HOOK_COUNT");

	if(!Exists)
	{
		TlsGetCurrentValue(&Unit.TLS, &Info);

		Info->Entries = (RUNTIME_INFO*)RtlAllocateMemory(TRUE, sizeof(RUNTIME_INFO) * MAX_HOOK_COUNT);

		if(Info->Entries == NULL)
			goto DONT_INTERCEPT;
	}

	// get hook runtime info...
	Runtime = &Info->Entries[InHandle->HLSIndex];

	if(Runtime->HLSIdent != InHandle->HLSIdent)
	{
		// just reset execution information
		Runtime->HLSIdent = InHandle->HLSIdent;
		Runtime->IsExecuting = FALSE;
	}

	// detect loops in hook execution hiearchy.
	if(Runtime->IsExecuting)
	{
		/*
			This implies that actually the handler has invoked itself. Because of
			the special HookLocalStorage, this is now also signaled if other
			hooks invoked by the related handler are calling it again.

			I call this the "Thread deadlock barrier".

			!!Note that the assembler code does not invoke LhBarrierOutro() in this case!!
		*/

		goto DONT_INTERCEPT;
	}

	Info->Callback = InHandle->Callback;
	Info->Current = Runtime;

	/*
		Now we will negotiate thread/process access based on global and local ACL...
	*/
#ifndef DRIVER
	Runtime->IsExecuting = IsThreadIntercepted(&InHandle->LocalACL, GetCurrentThreadId());
#else
	Runtime->IsExecuting = IsProcessIntercepted(&InHandle->LocalACL, (ULONG)PsGetCurrentProcessId());
#endif

	if(!Runtime->IsExecuting)
		goto DONT_INTERCEPT;

	// save some context specific information
	Runtime->RetAddress = InRetAddr;
	Runtime->AddrOfRetAddr = InAddrOfRetAddr;

	ReleaseSelfProtection();
	
	return TRUE;

DONT_INTERCEPT:
	/*  !!Note that the assembler code does not invoke UnmanagedHookOutro() in this case!! */

	if(Info != NULL)
	{
		Info->Current = NULL;
		Info->Callback = NULL;

		ReleaseSelfProtection();
	}

	return FALSE;
}





void* __stdcall LhBarrierOutro(LOCAL_HOOK_INFO* InHandle, void** InAddrOfRetAddr)
{
/*
Description:
    
    Will just reset the "thread deadlock barrier" for the current hook handler and provides
	some important integrity checks. 

	The hook handle is just passed through, because the assembler code has no chance to
	save it in any efficient manner at this point of execution...
*/
    RUNTIME_INFO*			Runtime;
    LPTHREAD_RUNTIME_INFO	Info;

	#ifdef _M_X64
		InHandle -= 1;
	#endif

	ASSERT(AcquireSelfProtection(),L"barrier.c - AcquireSelfProtection()");

	ASSERT(TlsGetCurrentValue(&Unit.TLS, &Info) && (Info != NULL),L"barrier.c - TlsGetCurrentValue(&Unit.TLS, &Info) && (Info != NULL)");

	Runtime = &Info->Entries[InHandle->HLSIndex];

	// leave handler context
	Info->Current = NULL;
	Info->Callback = NULL;

	ASSERT(Runtime != NULL,L"barrier.c - Runtime != NULL");

	ASSERT(Runtime->IsExecuting,L"barrier.c - Runtime->IsExecuting");

	Runtime->IsExecuting = FALSE;

	ASSERT(*InAddrOfRetAddr == NULL,L"barrier.c - *InAddrOfRetAddr == NULL");

	*InAddrOfRetAddr = Runtime->RetAddress;

	ReleaseSelfProtection();

	return InHandle;

}