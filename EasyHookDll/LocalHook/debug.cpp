// EasyHook (File: EasyHookDll\debug.cpp)
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

#include <dbgeng.h>

#ifdef __cplusplus
extern "C"{
#endif

typedef HRESULT __stdcall DebugCreate_PROC(
	        __in REFIID InterfaceId,
	        __out PVOID* Interface
	        );

typedef LONG NTAPI ZwQueryInformationProcess_PROC(
            IN HANDLE ProcessHandle,
            IN ULONG ProcessInformationClass,
            OUT PVOID ProcessInformation,
            IN ULONG ProcessInformationLength,
            OUT PULONG ReturnLength OPTIONAL
            );

typedef DWORD WINAPI ZwGetProcessId_PROC(
            IN HANDLE Process
            );

typedef LONG NTAPI ZwQueryInformationThread_PROC(
            IN HANDLE ThreadHandle,
            IN ULONG ThreadInformationClass,
            OUT PVOID ThreadInformation,
            IN ULONG ThreadInformationLength,
            OUT PULONG ReturnLength OPTIONAL
            );

typedef DWORD WINAPI ZwGetThreadId_PROC(
            IN HANDLE Thread
            );

#define ObjectNameInformation           1

typedef LONG ZwQueryObject_PROC(
            HANDLE InHandle,
            ULONG InInfoClass,
            PVOID OutInfo,
            ULONG InInfoSize,
            PULONG OutRequiredSize);

typedef struct _CLIENT_ID
{
    DWORD	    UniqueProcess;
    DWORD	    UniqueThread;
}CLIENT_ID, * PCLIENT_ID;

typedef struct _THREAD_BASIC_INFORMATION
{
    LONG ExitStatus;
    PNT_TIB TebBaseAddress;
    CLIENT_ID ClientId;
    DWORD AffinityMask;
    LONG Priority;
    LONG BasePriority;
}THREAD_BASIC_INFORMATION, *PTHREAD_BASIC_INFORMATION;

static HMODULE                          hDbgEng = NULL;
static IDebugClient*			        DebugClient = NULL;
static IDebugControl*			        DebugControl = NULL;
static DebugCreate_PROC*                CreateDebugEngine = NULL;
static ZwGetThreadId_PROC*              ZwGetThreadId = NULL;
static ZwGetProcessId_PROC*             ZwGetProcessId = NULL;
static ZwQueryInformationThread_PROC*   ZwQueryInformationThread = NULL;
static ZwQueryInformationProcess_PROC*  ZwQueryInformationProcess = NULL;
static ZwQueryObject_PROC*              ZwQueryObject = NULL;

EASYHOOK_NT_EXPORT DbgAttachDebugger()
{
/*
Description:

    Attaches a debugger to the current process (64-bit only). This is no 
	longer necessary for RIP-relocation and disassembling. Multiple
    calls will do nothing.

*/
    NTSTATUS            NtStatus;


    if(hDbgEng != NULL)
        RETURN;

#ifdef _M_X64

    if((hDbgEng = LoadLibraryW(L"dbgeng.dll")) == NULL)
        THROW(STATUS_NOT_SUPPORTED, L"Unable to load 'dbgeng.dll'.");

    if((CreateDebugEngine = (DebugCreate_PROC*)GetProcAddress(hDbgEng, "DebugCreate")) == NULL)
        THROW(STATUS_NOT_SUPPORTED, L"'dbgeng.dll' does not export 'DebugCreate'.");

    // initialize microsoft debugging engine
    if(!RTL_SUCCESS(CreateDebugEngine(__uuidof(IDebugClient), (void**)&DebugClient)))
        THROW(STATUS_NOT_SUPPORTED, L"Unable to obtain IDebugClient interface.");

    if(!RTL_SUCCESS(DebugClient->QueryInterface(__uuidof(IDebugControl), (void**)&DebugControl)))
        THROW(STATUS_NOT_SUPPORTED, L"Unable to obtain IDebugControl interface.");

    // attach to current process
	if(!RTL_SUCCESS(NtStatus = DebugClient->AttachProcess(0, GetCurrentProcessId(), DEBUG_ATTACH_NONINVASIVE |
			DEBUG_ATTACH_NONINVASIVE_NO_SUSPEND)))
	{
		NtStatus = NtStatus;

		THROW(STATUS_NOT_SUPPORTED, L"Unable to attach debugger to current process.");
	}

	// wait for completion
	if(!RTL_SUCCESS(DebugControl->WaitForEvent(0, 5000)))
	    THROW(STATUS_INTERNAL_ERROR, L"Unable to wait for debugger.");

    RETURN;

THROW_OUTRO:
    {
        DbgDetachDebugger();
    }
#else
	hDbgEng = (HMODULE)-1;

    RETURN;
#endif

FINALLY_OUTRO:
    {
        return NtStatus;
    }
}

EASYHOOK_NT_EXPORT DbgDetachDebugger()
{
#ifdef _M_X64
/*
Description:

    Detaches the debugger from the current process. If there
    is none attached, the method will do nothing.

*/
    if(DebugClient != NULL)
    {
	    DebugClient->DetachProcesses();
	    DebugClient->Release();
    }

    if(DebugControl != NULL)
	    DebugControl->Release();

    if(hDbgEng != NULL)
	    FreeLibrary(hDbgEng);
#endif

    DebugClient = NULL;
    DebugControl = NULL;
    hDbgEng = NULL;

    return STATUS_SUCCESS;
}



EASYHOOK_BOOL_EXPORT DbgIsEnabled()
{
/*
Description:

    Returns TRUE if the debugger is attached, FALSE otherwise.

*/
    return hDbgEng != NULL;
}




EASYHOOK_BOOL_EXPORT DbgIsAvailable()
{
/*
Description:

    Returns TRUE if a debugger can be attached to the current process,
    FALSE otherwise.

*/
#ifdef _M_x64
    HMODULE     Module = NULL;

    if((Module = LoadLibraryW(L"dbgeng.dll")) == NULL)
        return FALSE;

    FreeLibrary(Module);

    Module = NULL;
#endif
    return TRUE;
}




EASYHOOK_NT_INTERNAL DbgCriticalInitialize()
{
/*
Description:

    Loads some special APIs required for handler utilities.
    This should work on any sane system and will be called on
    DLL load.
*/

    // try to load newer methods first
    if((ZwGetProcessId = (ZwGetProcessId_PROC*)GetProcAddress(hKernel32, "GetProcessId")) == NULL)
    {
	    if((ZwQueryInformationProcess = (ZwQueryInformationProcess_PROC*)::GetProcAddress(
			    hNtDll, "NtQueryInformationProcess")) == NULL)
		    return STATUS_PROCEDURE_NOT_FOUND;
    }

    if((ZwGetThreadId = (ZwGetThreadId_PROC*)GetProcAddress(hKernel32, "GetThreadId")) == NULL)
    {
	    if((ZwQueryInformationThread = (ZwQueryInformationThread_PROC*)::GetProcAddress(
			    hNtDll, "NtQueryInformationThread")) == NULL)
            return STATUS_PROCEDURE_NOT_FOUND;
    }

    if((ZwQueryObject = (ZwQueryObject_PROC*)GetProcAddress(hNtDll, "ZwQueryObject")) == NULL)
        return STATUS_PROCEDURE_NOT_FOUND;

    return STATUS_SUCCESS;
}





EASYHOOK_NT_INTERNAL DbgCriticalFinalize()
{
/*
Description:

    Will be called on DLL unload and just ensures that
    the debugger is detached.

*/
    return DbgDetachDebugger();
}



EASYHOOK_NT_INTERNAL DbgRelocateRIPRelative(
	        ULONGLONG InOffset,
	        ULONGLONG InTargetOffset,
            BOOL* OutWasRelocated)
{
/*
Description:

    Check whether the given instruction is RIP relative and
    relocates it. If it is not RIP relative, nothing is done.

Parameters:

    - InOffset

        The instruction pointer to check for RIP addressing and relocate.

    - InTargetOffset

        The instruction pointer where the RIP relocation should go to.
        Please note that RIP relocation are relocated relative to the
        offset you specify here and therefore are still not absolute!

    - OutWasRelocated

        TRUE if the instruction was RIP relative and has been relocated,
        FALSE otherwise.
*/
#ifndef _M_X64
	return FALSE;
#else
	NTSTATUS            NtStatus;
	CHAR					Buf[MAX_PATH];
	ULONG					AsmSize;
	ULONG64					NextInstr;
	CHAR					Line[MAX_PATH];
	LONG					Pos;
	LONGLONG				RelAddr;
	LONGLONG				MemDelta = InTargetOffset - InOffset;

	ASSERT(MemDelta == (LONG)MemDelta,L"debug.cpp - MemDelta == (LONG)MemDelta");

    *OutWasRelocated = FALSE;

	// test field...
	/*BYTE t[10] = {0x8b, 0x05, 0x12, 0x34, 0x56, 0x78};

	InOffset = (LONGLONG)t;

	MemDelta = InTargetOffset - InOffset;
*/
	if(!RTL_SUCCESS(DebugControl->Disassemble(InOffset, 0, Buf, sizeof(Buf), &AsmSize, &NextInstr)))
		THROW(STATUS_INVALID_PARAMETER_1, L"Unable to disassemble entry point. ");

	Pos = RtlAnsiIndexOf(Buf, '[');

	if(Pos < 0)
		RETURN;

	// parse content
	if(RtlAnsiSubString(Buf, Pos + 1, RtlAnsiIndexOf(Buf, ']') - Pos - 1, Line, MAX_PATH) != 16)
		RETURN;

	if(!RtlAnsiDbgHexToLongLong(Line, 16, &RelAddr))
		RETURN;
	
	// verify that we are really RIP relative...
	RelAddr -= NextInstr;

	if(RelAddr != (LONG)RelAddr)
		RETURN;

	if(*((LONG*)(NextInstr - 4)) != RelAddr)
		RETURN;

	/*
		Just relocate this instruction...
	*/
	RelAddr = RelAddr - MemDelta;

	if(RelAddr != (LONG)RelAddr)
		THROW(STATUS_NOT_SUPPORTED, L"The given entry point contains at least one RIP-Relative instruction that could not be relocated!");

	// copy instruction
	RtlCopyMemory((void*)InTargetOffset, (void*)InOffset, (ULONG)(NextInstr - InOffset));

	*((LONG*)(InTargetOffset + (NextInstr - InOffset) - 4)) = (LONG)RelAddr;

    *OutWasRelocated = TRUE;

	RETURN;

THROW_OUTRO:
FINALLY_OUTRO:
    return NtStatus;
#endif
}







EASYHOOK_NT_EXPORT DbgGetThreadIdByHandle(
                HANDLE InThreadHandle,
                ULONG* OutThreadId)
{
/*
Description:

    Translates the given thread handle back to its thread ID if possible.

Parameters:

    - InThreadHandle

        A thread handle with THREAD_QUERY_INFORMATION access.

    - OutThreadId

        Receives the thread ID.
*/
    THREAD_BASIC_INFORMATION        ThreadInfo;
    NTSTATUS                        NtStatus;

    if(!IsValidPointer(OutThreadId, sizeof(ULONG)))
        THROW(STATUS_INVALID_PARAMETER_2, L"Invalid TID storage specified."); 

    if(ZwQueryInformationThread != NULL)
    {
	    // use deprecated API
	    FORCE(ZwQueryInformationThread(
            InThreadHandle, 
            0 /* ThreadBasicInformation */, 
            &ThreadInfo, 
            sizeof(ThreadInfo), 
            NULL));

        *OutThreadId = ThreadInfo.ClientId.UniqueThread;
    }
    else
    {
	    ASSERT(ZwGetThreadId != NULL,L"debug.cpp - ZwGetThreadId != NULL");

	    // use new support API
	    if((*OutThreadId = ZwGetThreadId(InThreadHandle)) == 0)
		    THROW(STATUS_INVALID_PARAMETER_1, L"Invalid thread handler or improper privileges.");
    }

    RETURN;

THROW_OUTRO:
FINALLY_OUTRO:
    return NtStatus;
}






EASYHOOK_NT_EXPORT DbgGetProcessIdByHandle(
                HANDLE InProcessHandle,
                ULONG* OutProcessId)
{
/*
Description:

    Translates the given process handle back to its process ID if possible.

Parameters:

    - InProcessHandle

        A process handle with PROCESS_QUERY_INFORMATION access.

    - OutProcessId

        Receives the process ID.
*/
    PROCESS_BASIC_INFORMATION       ProcInfo;
    NTSTATUS                        NtStatus;

    if(!IsValidPointer(OutProcessId, sizeof(ULONG)))
        THROW(STATUS_INVALID_PARAMETER_2, L"The given process ID storage is invalid.");

    if(ZwQueryInformationProcess != NULL)
    {
	    // use deprecated API
	    FORCE(ZwQueryInformationProcess(
            InProcessHandle, 
            0 /* ProcessBasicInformation */, 
            &ProcInfo, 
            sizeof(ProcInfo), 
            NULL));

	    *OutProcessId = (ULONG)ProcInfo.UniqueProcessId;
    }
    else
    {
	    ASSERT(ZwGetProcessId != NULL,L"debug.cpp - ZwGetProcessId != NULL");

	    // use new support API
	    if((*OutProcessId = ZwGetProcessId(InProcessHandle)) == 0)
		    THROW(STATUS_INVALID_PARAMETER_1, L"Invalid process handle or improper privileges.");
    }

RETURN;

THROW_OUTRO:
FINALLY_OUTRO:
    return NtStatus;
}





EASYHOOK_NT_EXPORT DbgHandleToObjectName(
                HANDLE InNamedHandle,
                PUNICODE_STRING OutNameBuffer,
                ULONG InBufferSize,
                ULONG* OutRequiredSize)
{
/*
Description:

    Queries the kernel space name of a named object. This is
    only possible if the handle refers to a named object of course.

Parameters:

    - InNamedHandle

        A valid file, event, section, etc.

    - OutNameBuffer

        A buffer large enough to hold the kernel space object name.
        To query the required size in bytes, set this parameter to
        NULL.

    - InBufferSize

        The maximum size in bytes the given buffer can hold.

    - OutRequiredSize

        Receives the required size in bytes. This parameter can be NULL.
*/
    ULONG           RequiredSize;
    NTSTATUS        NtStatus;


    if((InNamedHandle == NULL) || (InNamedHandle == INVALID_HANDLE_VALUE))
        THROW(STATUS_INVALID_PARAMETER_1, L"The given handle is invalid.");

    if(!IsValidPointer(OutNameBuffer, InBufferSize))
    {
        // determine required length
        if(InBufferSize != 0)
            THROW(STATUS_INVALID_PARAMETER_3, L"If no buffer is specified, the buffer size is expected to be zero.");

        if(OutRequiredSize == NULL)
            THROW(STATUS_INVALID_PARAMETER_4, L"If no buffer is specified, you are expected to query the required size.");
    }

    if((NtStatus = ZwQueryObject(InNamedHandle, ObjectNameInformation, NULL, 0, &RequiredSize)) 
            != STATUS_INFO_LENGTH_MISMATCH)
        FORCE(NtStatus);

    if(IsValidPointer(OutNameBuffer, InBufferSize))
    {
        // query string
        if(InBufferSize < RequiredSize)
            THROW(STATUS_BUFFER_TOO_SMALL, L"The given buffer is not long enough to hold all the data.");

        FORCE(ZwQueryObject(InNamedHandle, ObjectNameInformation, OutNameBuffer, InBufferSize, &RequiredSize));
    }

    if(IsValidPointer(OutRequiredSize, sizeof(ULONG)))
        *OutRequiredSize = RequiredSize;

    RETURN;

THROW_OUTRO:
FINALLY_OUTRO:
    return NtStatus;
}
#ifdef __cplusplus
}
#endif