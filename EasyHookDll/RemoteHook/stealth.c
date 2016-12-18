// EasyHook (File: EasyHookDll\stealth.c)
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

BYTE* GetStealthStubPtr();

ULONG GetStealthStubSize();

typedef struct _STEALTH_CONTEXT_
{
    union
    {
        struct
        {
            /*00*/ WRAP_ULONG64(PVOID      CreateThread);			
            /*08*/ WRAP_ULONG64(PVOID      RemoteThreadStart);
            /*16*/ WRAP_ULONG64(PVOID      RemoteThreadParam);
            /*24*/ WRAP_ULONG64(PVOID      WaitForSingleObject);
            /*32*/ WRAP_ULONG64(HANDLE     hCompletionEvent);
            /*40*/ WRAP_ULONG64(PVOID      CloseHandle);
			/*48*/ union
			{
				WRAP_ULONG64(HANDLE     hRemoteThread);
				WRAP_ULONG64(HANDLE     hSyncEvent);
			};
            /*56*/ WRAP_ULONG64(PVOID      SetEvent);
        };

        ULONGLONG       __Unused__[8];
    };

	// Used to save the current thread's state before calling StealthStub_ASM_x86/x64
    // Integer Registers
	ULONGLONG           Rax;		// 0
    ULONGLONG           Rcx;		// 1
    ULONGLONG           Rdx;		// 2
    ULONGLONG           Rbp;		// 3
    ULONGLONG           Rsp;		// 4
    ULONGLONG           Rsi;		// 5
    ULONGLONG           Rdi;		// 6
    ULONGLONG           Rbx;		// 7
    ULONGLONG           Rip;		// 8
    ULONGLONG           RFlags;		// 9
    ULONGLONG           R8;			// 10
    ULONGLONG           R9;			// 11
    ULONGLONG           R10;		// 12
    ULONGLONG           R11;		// 13
    ULONGLONG           R12;		// 14
    ULONGLONG           R13;		// 15
    ULONGLONG           R14;		// 16
    ULONGLONG           R15;		// 17
	// Stack values (as a couple of values will be overwritten within StealthStub_ASM_x64)
	ULONGLONG			StackA;		// 18
	ULONGLONG			StackB;		// 19
}STEALTH_CONTEXT, *PSTEALTH_CONTEXT;


EASYHOOK_NT_EXPORT RhCreateStealthRemoteThread(
            ULONG InTargetPID,
            LPTHREAD_START_ROUTINE InRemoteRoutine,
            PVOID InRemoteParam,
			HANDLE* OutRemoteThread)
{
/*
Description:

    Using Remote thread creation on bad purpose is not new. This is why many AV software will mark
    processes that do so as SUSPECT. To workaround this issue I introduce
    a new stealth thread creation. This is done by hijacking an existing thread
    and letting it execute an invokation stub which itself creates a thread
    using CreateThread() in the target process. This thread again will execute
    the given remote thread proc. After such a stealth remote thread has been
    created, the hijacked thread will continue execution just like nothing has happened.

    The problem with this approach is, that there are several bad circumstances in
    which the target process might be damaged or crashed. This also won't work
    if no running thread is available in the target process (for example processes
    created with RhCreateAndInject()). But I thought it would be something new to 
    play with ;-). Any sane process shouldn't be damaged by this approach but there
    is no guarantee.

Parameters:

    - InTargetPID

        The process ID of the target.

    - InRemoteRoutine

        A usual thread start routine in the target process space.

    - InRemoteParam

        An uninterpreted value passed to the remote start routine.

	- OutRemoteThread

		Receives a handle to the remote thread which is valid in the
		current process.

Returns:


*/
    NTSTATUS            NtStatus;
    HANDLE              hProc = NULL;
    BOOL                Is64BitTarget;
    STEALTH_CONTEXT     LocalCtx;
    STEALTH_CONTEXT*    RemoteCtx = NULL;
    CONTEXT             Context;
    HANDLE              hHijackedThread = NULL;
	DWORD				hijackedThreadId = 0;
    HANDLE              hCompletionEvent = NULL;
	HANDLE              hSyncEvent = NULL;
    SIZE_T              BytesRead = 0;
	HANDLE				hThreadSnap = INVALID_HANDLE_VALUE; 
	THREADENTRY32		NativeEntry;
	ULONG				SuspendCount;
	ULONG				CtxSize = GetStealthStubSize() + sizeof(LocalCtx);
	BOOLEAN				IsSuspended = FALSE;

	RtlZeroMemory(&LocalCtx, sizeof(LocalCtx));

	if((hProc = OpenProcess(
				PROCESS_VM_OPERATION | PROCESS_DUP_HANDLE | PROCESS_VM_READ | PROCESS_VM_WRITE, 
				FALSE, 
				InTargetPID)) == NULL)
	{
		if(GetLastError() == ERROR_ACCESS_DENIED)
		    THROW(STATUS_ACCESS_DENIED, L"Unable to open target process. Consider using a system service.")
		else
			THROW(STATUS_NOT_FOUND, L"The given target process does not exist!");
	}

	/*
		Check bitness...

		After this we can assume hooking a target that is running in the same
		WOW64 level.
	*/
#ifdef _M_X64
	FORCE(RhIsX64Process(InTargetPID, &Is64BitTarget));
      
    if(!Is64BitTarget)
        THROW(STATUS_WOW_ASSERTION, L"It is not supported to directly operate through the WOW64 barrier.");
#else
	FORCE(RhIsX64Process(InTargetPID, &Is64BitTarget));
      
    if(Is64BitTarget)
        THROW(STATUS_WOW_ASSERTION, L"It is not supported to directly operate through the WOW64 barrier.");
#endif
	

    /*
        Select running thread...
    */

	// take a snapshot of all running threads  
	NativeEntry.dwSize = sizeof(NativeEntry);

	if((hThreadSnap = CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0)) == INVALID_HANDLE_VALUE)
		THROW(STATUS_INTERNAL_ERROR, L"Unable to enumerate system threads.");

	if(!Thread32First(hThreadSnap, &NativeEntry)) 
		THROW(STATUS_INTERNAL_ERROR, L"Unable to get first thread in enumeration.");

	do 
	{ 
		if((NativeEntry.th32OwnerProcessID == InTargetPID) && (NativeEntry.th32ThreadID != GetCurrentThreadId()))
		{
			// is thread active?
			if((hHijackedThread = OpenThread(
					THREAD_SUSPEND_RESUME | THREAD_SET_CONTEXT | THREAD_GET_CONTEXT | THREAD_QUERY_INFORMATION,
					FALSE,
					NativeEntry.th32ThreadID)) == NULL)
				continue;

			if((SuspendCount = SuspendThread(hHijackedThread)) != 0)
			{
				// thread is not running...
				if(SuspendCount != (DWORD)-1)
					ResumeThread(hHijackedThread);

				continue;
			}

			// if thread was active, it is now suspended...
			IsSuspended = TRUE;

			hijackedThreadId = NativeEntry.th32ThreadID;

			break;
		}

	}while(Thread32Next(hThreadSnap, &NativeEntry)); 

	if(hHijackedThread == NULL)
		THROW(STATUS_NOT_SUPPORTED, L"Unable to select active thread in target process.");

	DEBUGMSG(L"RhCreateStealthRemoteThread hijacked thread Id: %d\n", hijackedThreadId);

    /*
        Capture context...
    */
	Context.ContextFlags = CONTEXT_INTEGER | CONTEXT_CONTROL;

    if(!GetThreadContext(hHijackedThread, &Context))
        THROW(STATUS_INTERNAL_ERROR, L"Unable to capture remote thread context.");

#ifdef _M_X64

    LocalCtx.Rax = Context.Rax;
    LocalCtx.Rcx = Context.Rcx;
    LocalCtx.Rdx = Context.Rdx;
    LocalCtx.Rbx = Context.Rbx;
    LocalCtx.Rbp = Context.Rbp;
    LocalCtx.Rsp = Context.Rsp;
    LocalCtx.Rdi = Context.Rdi;
    LocalCtx.Rsi = Context.Rsi;
    LocalCtx.Rip = Context.Rip;
	LocalCtx.RFlags = Context.EFlags;
    LocalCtx.R8 = Context.R8;
    LocalCtx.R9 = Context.R9;
    LocalCtx.R10 = Context.R10;
    LocalCtx.R11 = Context.R11;
    LocalCtx.R12 = Context.R12;
    LocalCtx.R13 = Context.R13;
    LocalCtx.R14 = Context.R14;
    LocalCtx.R15 = Context.R15;

#else

    LocalCtx.Rax = Context.Eax;
    LocalCtx.Rcx = Context.Ecx;
    LocalCtx.Rdx = Context.Edx;
    LocalCtx.Rbx = Context.Ebx;
    LocalCtx.Rbp = Context.Ebp;
    LocalCtx.Rsp = Context.Esp;
    LocalCtx.Rdi = Context.Edi;
    LocalCtx.Rsi = Context.Esi;
    LocalCtx.Rip = Context.Eip;
    LocalCtx.RFlags = Context.EFlags;

#endif

  

    /*
        Hijack target thread...
    */
    LocalCtx.CreateThread = (PVOID)GetProcAddress(hKernel32, "CreateThread");
    LocalCtx.RemoteThreadStart = (PVOID)InRemoteRoutine;
    LocalCtx.RemoteThreadParam = InRemoteParam;
    LocalCtx.SetEvent = (PVOID)GetProcAddress(hKernel32, "SetEvent");
	LocalCtx.CloseHandle = (PVOID)GetProcAddress(hKernel32, "CloseHandle");
	LocalCtx.WaitForSingleObject = (PVOID)GetProcAddress(hKernel32, "WaitForSingleObject");

	if( ((hCompletionEvent = CreateEvent(NULL, TRUE, FALSE, NULL)) == NULL) ||
			((hSyncEvent = CreateEvent(NULL, TRUE, FALSE, NULL)) == NULL))
		THROW(STATUS_INTERNAL_ERROR, L"Unable to create event.");

    if(!DuplicateHandle(GetCurrentProcess(), hCompletionEvent, hProc, &LocalCtx.hCompletionEvent, 0, FALSE, DUPLICATE_SAME_ACCESS) ||
			!DuplicateHandle(GetCurrentProcess(), hSyncEvent, hProc, &LocalCtx.hSyncEvent, 0, FALSE, DUPLICATE_SAME_ACCESS))
        THROW(STATUS_INTERNAL_ERROR, L"Unable to duplicate event.");

	/* 
		Allocate executable stack.

		This is a little hack, because otherwise there would be problems when releasing the
		allocated memory. We can't do it like it is done for the injection assembly, because
		we have to ensure that the context stays unchanged! So we would have to copy a major
		amount of the code to the stack and this is why I decided to copy all the code to
		the stack. The good thing is that stack allocations don't need to be released. Windows
		will do this automatically. Any sane process should not be violated by this attempt.
		We also shift the whole thing some pages behind the RSP value, so that even if code uses
		some sort of "dead stack" reference, we will not overwrite any data.
	*/
#if !_M_X64
    __pragma(warning(push))
    __pragma(warning(disable:4305))
#endif
    if(VirtualAllocEx(hProc, (PVOID)(LocalCtx.Rsp - 4096 * 20), 4096, MEM_COMMIT, PAGE_EXECUTE_READWRITE) == NULL)
		THROW(STATUS_NO_MEMORY, L"Unable to allocate executable thread stack.");
    RemoteCtx = (STEALTH_CONTEXT*)(LocalCtx.Rsp - CtxSize - 4096 * 19);
#if !_M_X64
    __pragma(warning(pop))
#endif

    // prepare thread context...
#ifndef _M_X64
    Context.Eip = (ULONG)RemoteCtx;
    Context.Ebx = (ULONG)RemoteCtx + GetStealthStubSize();
#else
	Context.Rip = (ULONGLONG)RemoteCtx;
    Context.Rbx = (ULONGLONG)RemoteCtx + GetStealthStubSize();
#endif

	// write remote stealth stub
	if(!WriteProcessMemory(hProc, (UCHAR*)RemoteCtx + GetStealthStubSize(), &LocalCtx, sizeof(LocalCtx), &BytesRead))
        THROW(STATUS_INTERNAL_ERROR, L"Unable to write remote context.");

	if(!WriteProcessMemory(hProc, (UCHAR*)RemoteCtx, GetStealthStubPtr(), GetStealthStubSize(), &BytesRead))
        THROW(STATUS_INTERNAL_ERROR, L"Unable to write remote stealth stub.");

	// resume thread
    if(!SetThreadContext(hHijackedThread, &Context))
        THROW(STATUS_INTERNAL_ERROR, L"Unable to adjust remote thread context.");

	ResumeThread(hHijackedThread);

	IsSuspended = FALSE;

	// TODO:
	//::PostThreadMessage(HijackedThreadId, 
	//PostThreadMessage(hijackedThreadId, WM_NULL, 0, 0);

	/*
		Wait for completion and process results...
	*/

    // wait for thread creation...
	if(WaitForSingleObject(hSyncEvent, INFINITE) != WAIT_OBJECT_0)
		THROW(STATUS_INTERNAL_ERROR, L"Unable to wait for remote thread creation.");

	// update local context
	if(!ReadProcessMemory(hProc, (CHAR*)RemoteCtx + GetStealthStubSize(), &LocalCtx, sizeof(LocalCtx), &BytesRead))
		THROW(STATUS_INTERNAL_ERROR, L"Unable to re-read remote context.");

	if(LocalCtx.hRemoteThread == NULL)
		THROW(STATUS_INTERNAL_ERROR, L"Unable to create remote thread.");
	
	if(IsValidPointer(OutRemoteThread, sizeof(HANDLE)))
	{
		if(!DuplicateHandle(hProc, LocalCtx.hRemoteThread, GetCurrentProcess(), OutRemoteThread, 0, FALSE, DUPLICATE_SAME_ACCESS))
			THROW(STATUS_INTERNAL_ERROR, L"Unable to import remote thread handle.");
	}

	// let hijacked thread continue execution
	if(!SetEvent(hCompletionEvent))
		THROW(STATUS_INTERNAL_ERROR, L"Unable to resume hijacked thread.");

    RETURN;

THROW_OUTRO:
FINALLY_OUTRO:
    {
		if(hCompletionEvent != NULL)
		{
			// ensure that hijacked thread can continue exectuion...
			SetEvent(hCompletionEvent);

			CloseHandle(hCompletionEvent);
		}

		if(hSyncEvent != NULL)
			CloseHandle(hSyncEvent);

		if(hHijackedThread != NULL)
		{
			if(IsSuspended)
				ResumeThread(hHijackedThread);

			CloseHandle(hHijackedThread);
		}
       
        if(hProc != NULL)
            CloseHandle(hProc);

		if(hThreadSnap != INVALID_HANDLE_VALUE)
			CloseHandle(hThreadSnap);

        return NtStatus;
    }
}


/*/////////////////////////////////////////////////////////////////////////////////////
//////////////////////////////////////////////////// GetStealthStubSize
///////////////////////////////////////////////////////////////////////////////////////

Dynamically retrieves the size of the trampoline method.
*/
static DWORD ___StealthStubSize = 0;

#ifdef _M_X64
	EXTERN_C void StealthStub_ASM_x64();
#else
	EXTERN_C void __stdcall StealthStub_ASM_x86();
#endif

BYTE* GetStealthStubPtr()
{
#ifdef _M_X64
	BYTE* Ptr = (BYTE*)StealthStub_ASM_x64;
#else
	BYTE* Ptr = (BYTE*)StealthStub_ASM_x86;
#endif

// bypass possible VS2008 debug jump table
	if(*Ptr == 0xE9)
		Ptr += *((int*)(Ptr + 1)) + 5;

	return Ptr;
}

ULONG GetStealthStubSize()
{
    UCHAR*          Ptr;
    UCHAR*          BasePtr;
    ULONG           Index;
    ULONG           Signature;

	if(___StealthStubSize != 0)
		return ___StealthStubSize;
	
	// search for signature
	BasePtr = Ptr = GetStealthStubPtr();

	for(Index = 0; Index < 2000 /* some always large enough value*/; Index++)
	{
		Signature = *((ULONG*)Ptr);

		if(Signature == 0x12345678)	
		{
			___StealthStubSize = (ULONG)(Ptr - BasePtr);

			return ___StealthStubSize;
		}

		Ptr++;
	}

	ASSERT(FALSE,L"stealth.c - ULONG GetStealthStubSize()");

    return 0;
}

