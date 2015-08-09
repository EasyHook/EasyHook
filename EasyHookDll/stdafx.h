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
#ifndef _STDAFX_H_
#define _STDAFX_H_


// support for Windows 2000 SP4 and later...
#define NTDDI_VERSION           NTDDI_WIN2KSP4
#define _WIN32_WINNT            0x500
#define _WIN32_IE_              _WIN32_IE_WIN2KSP4


#pragma warning (disable:4100) // unreference formal parameter
#pragma warning(disable:4201) // nameless struct/union
#pragma warning(disable:4102) // unreferenced label
#pragma warning(disable:4127) // conditional expression is constant


#include <windows.h>
#include <winnt.h>
#include <winternl.h>
#include <stdio.h>
#include <stdlib.h>
#include <tlhelp32.h>
#include <strsafe.h>
#include <crtdbg.h>

#pragma warning(disable: 4005)
#include <ntstatus.h>
#pragma warning(default: 4005)

#ifdef __cplusplus
extern "C"{
#endif

#include "EasyHook.h"
#include "DriverShared.h"

BOOL RtlFileExists(WCHAR* InPath);
LONG RtlGetWorkingDirectory(WCHAR* OutPath, ULONG InMaxLength);
LONG RtlGetCurrentModulePath(WCHAR* OutPath, ULONG InMaxLength);

#define RTL_SUCCESS(ntstatus)       SUCCEEDED(ntstatus)

HOOK_ACL* LhBarrierGetAcl();
void LhBarrierThreadDetach();
NTSTATUS LhBarrierProcessAttach();
void LhBarrierProcessDetach();
ULONGLONG LhBarrierIntro(LOCAL_HOOK_INFO* InHandle, void* InRetAddr, void** InAddrOfRetAddr);
void* __stdcall LhBarrierOutro(LOCAL_HOOK_INFO* InHandle, void** InAddrOfRetAddr);

LONG DbgRelocateRIPRelative(
	        ULONGLONG InOffset,
	        ULONGLONG InTargetOffset,
            BOOL* OutWasRelocated);

EASYHOOK_NT_INTERNAL RhSetWakeUpThreadID(ULONG InThreadID);


extern HMODULE             hNtDll;
extern HMODULE             hKernel32;
extern HMODULE             hCurrentModule;
extern HANDLE              hEasyHookHeap;

// this is just to make machine code management easier
#define WRAP_ULONG64(Decl)\
union\
{\
	ULONG64 UNUSED;\
	Decl;\
}\
    
#define UNUSED2(y) __Unused_##y
#define UNUSED1(y) UNUSED2(y)
#define UNUSED UNUSED1(__COUNTER__)

typedef struct _REMOTE_INFO_
{
	// will be the same for all processes
	WRAP_ULONG64(wchar_t* UserLibrary); // fixed 0
	WRAP_ULONG64(wchar_t* EasyHookPath); // fixed 8
	WRAP_ULONG64(wchar_t* PATH); // fixed 16
	WRAP_ULONG64(char* EasyHookEntry); // fixed 24
	WRAP_ULONG64(void* RemoteEntryPoint); // fixed 32
	WRAP_ULONG64(void* LoadLibraryW); // fixed; 40
	WRAP_ULONG64(void* FreeLibrary); // fixed; 48
	WRAP_ULONG64(void* GetProcAddress); // fixed; 56
	WRAP_ULONG64(void* VirtualFree); // fixed; 64
	WRAP_ULONG64(void* VirtualProtect); // fixed; 72
	WRAP_ULONG64(void* ExitThread); // fixed; 80
	WRAP_ULONG64(void* GetLastError); // fixed; 88
	
    BOOL            IsManaged;
	HANDLE          hRemoteSignal; 
	DWORD           HostProcess;
	DWORD           Size;
	BYTE*           UserData;
	DWORD           UserDataSize;
    ULONG           WakeUpThreadID;
}REMOTE_INFO, *LPREMOTE_INFO;


#ifdef __cplusplus
}
#endif

#endif