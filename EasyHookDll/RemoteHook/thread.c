// EasyHook (File: EasyHookDll\thread.c)
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

BYTE* GetInjectionPtr();
DWORD GetInjectionSize();

extern DWORD               RhTlsIndex;

EASYHOOK_NT_INTERNAL RhSetWakeUpThreadID(ULONG InThreadID)
{
/*
Description:
    
    Used in conjunction with RhCreateAndInject(). If the given thread
    later is resumed in RhWakeUpProcess(), the injection target will
    start its usual execution.

*/
    NTSTATUS            NtStatus;

    if(!TlsSetValue(RhTlsIndex, (LPVOID)(size_t)InThreadID))
        THROW(STATUS_INTERNAL_ERROR, L"Unable to set TLS value.");

    RETURN(STATUS_SUCCESS);

THROW_OUTRO:
FINALLY_OUTRO:
    return NtStatus;
}







EASYHOOK_NT_EXPORT RhWakeUpProcess()
{
/*
Description:

    Used in conjunction with RhCreateAndInject(). Wakes up the
    injection target. You should call this method after all hooks
    (or whatever) are applied.
*/

    NTSTATUS            NtStatus;
    ULONG               ThreadID = (ULONG)TlsGetValue(RhTlsIndex);
    HANDLE              hThread = NULL;

    if(ThreadID == 0)
        RETURN(STATUS_SUCCESS);
    
    if((hThread = OpenThread(THREAD_SUSPEND_RESUME, FALSE, ThreadID)) == NULL)
        THROW(STATUS_INTERNAL_ERROR, L"Unable to open wake up thread.");

    if(!ResumeThread(hThread))
        THROW(STATUS_INTERNAL_ERROR, L"Unable to resume process main thread.");

    RETURN(STATUS_SUCCESS);
    
THROW_OUTRO:
FINALLY_OUTRO:
    {
        if(hThread != NULL)
            CloseHandle(hThread);

        return NtStatus;
    }
}







EASYHOOK_NT_EXPORT RhGetProcessToken(
            ULONG InProcessId,
            HANDLE* OutToken)
{
/*
Description:

     This method is intended for the managed layer and has no special
     advantage in an unmanaged environment!

Parameters:

    - InProcessId

        The target process shall be accessible with PROCESS_QUERY_INFORMATION.

    - OutToken

        The identity token for the session the process was created in.
*/
    HANDLE			    hProc = NULL;
    NTSTATUS            NtStatus;

    if(!IsValidPointer(OutToken, sizeof(HANDLE)))
        THROW(STATUS_INVALID_PARAMETER_2, L"The given token storage is invalid.");

	if((hProc = OpenProcess(PROCESS_QUERY_INFORMATION, FALSE, InProcessId)) == NULL)
	{
		if(GetLastError() == ERROR_ACCESS_DENIED)
			THROW(STATUS_ACCESS_DENIED, L"The given process is not accessible.")
		else
			THROW(STATUS_NOT_FOUND, L"The given process does not exist.");
	}

	if(!OpenProcessToken(hProc, TOKEN_READ, OutToken))
	    THROW(STATUS_INTERNAL_ERROR, L"Unable to query process token.");

	RETURN(STATUS_SUCCESS);

THROW_OUTRO:
FINALLY_OUTRO:
    {
		if(hProc != NULL)
			CloseHandle(hProc);

        return NtStatus;
	}
}






EASYHOOK_BOOL_EXPORT RhIsAdministrator()
{
/*
Description:

    If someone is able to open the SCM with all access he is also able to create and start system services
    and so he is also able to act as a part of the system! We are just letting
    windows decide and don't enforce that the user is in the builtin admin group.
*/

    SC_HANDLE			hSCManager = NULL;

    if((hSCManager = OpenSCManagerW(NULL, NULL, SC_MANAGER_ALL_ACCESS)) == NULL)
	    return FALSE;

    CloseServiceHandle(hSCManager);

    return TRUE;
}






 typedef LONG WINAPI NtCreateThreadEx_PROC(
	PHANDLE ThreadHandle,
	ACCESS_MASK DesiredAccess,
	LPVOID ObjectAttributes,
	HANDLE ProcessHandle,
	LPTHREAD_START_ROUTINE lpStartAddress,
	LPVOID lpParameter,
	BOOL CreateSuspended,
	DWORD dwStackSize,
	LPVOID Unknown1,
	LPVOID Unknown2,
	LPVOID Unknown3
); 

EASYHOOK_NT_INTERNAL NtCreateThreadEx(
	HANDLE InProcess,
	LPTHREAD_START_ROUTINE InRemoteThreadStart,
	void* InRemoteCallback,
	BOOLEAN InCreateSuspended,
    HANDLE* OutThread)
{
/*
Description:

	Only intended for Vista and later... Will return NULL for all others which
	should use CreateRemoteThread() and services instead!

	In contrast to RtlCreateUserThread() this one will fortunately setup a proper activation
	context stack, which is required to load the NET framework and many other
	common APIs. This is why RtlCreateUserThread() can't be used for Windows XP
	,for example, even if it would replace the windows service approach which is required
	in order to get CreateRemoteThread() working.

	Injection through WOW64 boundaries is still not directly supported and requires
	a WOW64 bypass helper process.

Parameters:

    - InProcess

        A target process opened with PROCESS_ALL_ACCESS.

    - InRemoteThreadStart

        The method executed by the remote thread. Must be valid in the
        context of the given process.

    - InRemoteCallback

        An uninterpreted callback passed to the remote start routine. 
        Must be valid in the context of the given process.

    - OutThread

        Receives a handle to the remote thread. This handle is valid
        in the calling process.

Returns:

    STATUS_NOT_SUPPORTED

        Only Windows Vista and later supports NtCreateThreadEx, all other
        platforms will return this error code.
*/
    HANDLE			        hRemoteThread;
	NTSTATUS            NtStatus;
    NtCreateThreadEx_PROC*  VistaCreateThread;

    if(!IsValidPointer(OutThread, sizeof(HANDLE)))
        THROW(STATUS_INVALID_PARAMETER_4, L"The given handle storage is invalid.");

	// this will only work for vista and later...
	if((VistaCreateThread = (NtCreateThreadEx_PROC*)GetProcAddress(hNtDll, "NtCreateThreadEx")) == NULL)
		THROW(STATUS_NOT_SUPPORTED, L"NtCreateThreadEx() is not supported.");

	FORCE(VistaCreateThread(
			&hRemoteThread,
			0x1FFFFF, // all access
			NULL,
			InProcess,
			(LPTHREAD_START_ROUTINE)InRemoteThreadStart,
			InRemoteCallback,
			InCreateSuspended,
			0,
			NULL,
			NULL,
			NULL
			));

    *OutThread = hRemoteThread;

	RETURN;

THROW_OUTRO:
FINALLY_OUTRO:
    return NtStatus;
}





EASYHOOK_NT_INTERNAL NtForceLdrInitializeThunk(HANDLE hProc)
{
	/*
	Description:

	Allows us to retrieve remote function addresses for a suspended process by starting a 
	remote thread that runs a single instruction function (ret). Thanks to werker: https://github.com/EasyHook/EasyHook/issues/9

	Parameters:

	- hProcess

	The handle to the remote process to force loader initialization
	*/
	HANDLE                  hRemoteThread = NULL;
	UCHAR*                  RemoteInjectCode = NULL;
	BYTE                    InjectCode[3];
	ULONG                   CodeSize;
	SIZE_T                  BytesWritten;
	NTSTATUS                NtStatus;

#ifdef _M_X64
	InjectCode[0] = 0xC3; // ret
	CodeSize = 1;
#else
	InjectCode[0] = 0xC2; // ret 0x4
	InjectCode[1] = 0x04;
	InjectCode[2] = 0x00;
	CodeSize = 3;
#endif

	if ((RemoteInjectCode = (BYTE*)VirtualAllocEx(hProc, NULL, CodeSize, MEM_COMMIT, PAGE_EXECUTE_READWRITE)) == NULL)
		THROW(STATUS_NO_MEMORY, L"Unable to allocate memory in target process.");

	if (!WriteProcessMemory(hProc, RemoteInjectCode, InjectCode, CodeSize, &BytesWritten) || (BytesWritten != CodeSize))
		THROW(STATUS_INTERNAL_ERROR, L"Unable to write into target process memory.");

	if (!RTL_SUCCESS(NtCreateThreadEx(hProc, (LPTHREAD_START_ROUTINE)RemoteInjectCode, NULL, FALSE, &hRemoteThread)))
	{
		if ((hRemoteThread = CreateRemoteThread(hProc, NULL, 0, (LPTHREAD_START_ROUTINE)RemoteInjectCode, NULL, 0, NULL)) == NULL)
			THROW(STATUS_ACCESS_DENIED, L"Unable to create remote thread.");
	}

	WaitForSingleObject(hRemoteThread, INFINITE);

	RETURN;

THROW_OUTRO:
FINALLY_OUTRO :
	return NtStatus;
}





typedef BOOL __stdcall IsWow64Process_PROC(HANDLE InProc, BOOL* OutResult);
typedef void GetNativeSystemInfo_PROC(LPSYSTEM_INFO OutSysInfo);

EASYHOOK_NT_EXPORT RhIsX64Process(
            ULONG InProcessId,
            BOOL* OutResult)
{
/*
Description:

    Detects the bitness of a given process.

Parameters:

    - InProcessId

        The calling process must have PROCESS_QUERY_INFORMATION access
        to the process represented by this ID.

    - OutResult

        Is set to TRUE if the given process is running under 64-Bit,
        FALSE otherwise.
*/
	BOOL			            IsTarget64Bit = FALSE;
	HANDLE			            hProc = NULL;
    IsWow64Process_PROC*        pIsWow64Process;
    NTSTATUS            NtStatus;

#ifndef _M_X64
    GetNativeSystemInfo_PROC*   pGetNativeSystemInfo;
    SYSTEM_INFO		            SysInfo;
#endif

    if(!IsValidPointer(OutResult, sizeof(BOOL)))
        THROW(STATUS_INVALID_PARAMETER_2, L"The given result storage is invalid.");

	// open target process
	if((hProc = OpenProcess(PROCESS_QUERY_INFORMATION, FALSE, InProcessId)) == NULL)
	{
		if(GetLastError() == ERROR_ACCESS_DENIED)
			THROW(STATUS_ACCESS_DENIED, L"The given process is not accessible.")
		else
			THROW(STATUS_NOT_FOUND, L"The given process does not exist.");
	}

	// if WOW64 is not available then target must be 32-bit
	pIsWow64Process = (IsWow64Process_PROC*)GetProcAddress(hKernel32, "IsWow64Process");

#ifdef _M_X64
	// if the target is not WOW64, then it is 64-bit
	if(!pIsWow64Process(hProc, &IsTarget64Bit))
		THROW(STATUS_INTERNAL_ERROR, L"Unable to detect wether target process is 64-bit or not.");

	IsTarget64Bit = !IsTarget64Bit;

#else

	IsTarget64Bit = FALSE;

	if(pIsWow64Process != NULL)
	{
		// check if we are running on a 32-bit OS
		pGetNativeSystemInfo = (GetNativeSystemInfo_PROC*)GetProcAddress(hKernel32, "GetNativeSystemInfo");

		if(pGetNativeSystemInfo != NULL)
		{
			pGetNativeSystemInfo(&SysInfo);

			if(SysInfo.wProcessorArchitecture != PROCESSOR_ARCHITECTURE_INTEL)
			{
				// if not, then and only then a 32-bit process will be marked as WOW64 process!
				if(!pIsWow64Process(hProc, &IsTarget64Bit))
					THROW(STATUS_INTERNAL_ERROR, L"Unable to detect wether target process is 64-bit or not.");

				IsTarget64Bit = !IsTarget64Bit;
			}
		}
	}
#endif

	*OutResult = IsTarget64Bit;

    RETURN(STATUS_SUCCESS);

THROW_OUTRO:
FINALLY_OUTRO:
    {
		if(hProc != NULL)
			CloseHandle(hProc);

        return NtStatus;
	}
}

#ifndef _DEBUG
    #pragma optimize ("", off) // suppress _memcpy
#endif

EASYHOOK_BOOL_EXPORT RhIsX64System()
{
/*
Description:

	Determines whether the calling PC is running a 64-Bit version
	of windows.
*/
#ifndef _M_X64
    
	GetNativeSystemInfo_PROC*   pGetNativeSystemInfo;
    SYSTEM_INFO		            SysInfo;

	pGetNativeSystemInfo = (GetNativeSystemInfo_PROC*)GetProcAddress(hKernel32, "GetNativeSystemInfo");

	if(pGetNativeSystemInfo == NULL)
		return FALSE;

	pGetNativeSystemInfo(&SysInfo);

	if(SysInfo.wProcessorArchitecture == PROCESSOR_ARCHITECTURE_INTEL)
		return FALSE;

#endif

	return TRUE;
}

EASYHOOK_NT_EXPORT RtlCreateSuspendedProcess(
		WCHAR* InEXEPath,
        WCHAR* InCommandLine,
		ULONG InCustomFlags,
        ULONG* OutProcessId,
        ULONG* OutThreadId)
{
/*
Description:

    Creates a suspended process with the given parameters.
    This is only intended for the managed layer.

Parameters:

    - InEXEPath

        A relative or absolute path to the EXE file of the process being created.

    - InCommandLine

        Optional command line parameters passed to the newly created process.

	- InCustomFlags

		Additional process creation flags.

    - OutProcessId

        Receives the PID of the newly created process.

    - OutThreadId

        Receives the initial TID of the newly created process.
*/
#define MAX_CREATEPROCESS_COMMANDLINE 32768

    STARTUPINFO				StartInfo;
	PROCESS_INFORMATION		ProcessInfo;
	WCHAR					FullExePath[MAX_PATH + 1];
	WCHAR                   FullCommandLine[MAX_CREATEPROCESS_COMMANDLINE];
    WCHAR					CurrentDir[MAX_PATH + 1];
    WCHAR*					FilePart;
    NTSTATUS            NtStatus;

    // must be executed before any THROW or RETURN!
    RtlZeroMemory(&StartInfo, sizeof(StartInfo));
    RtlZeroMemory(&ProcessInfo, sizeof(ProcessInfo));

    if(!IsValidPointer(OutProcessId, sizeof(ULONG)))
        THROW(STATUS_INVALID_PARAMETER_3, L"The given process ID storage is invalid.");

    if(!IsValidPointer(OutThreadId, sizeof(ULONG)))
        THROW(STATUS_INVALID_PARAMETER_4, L"The given thread ID storage is invalid.");

    // parse path
    if(!RtlFileExists(InEXEPath))
        THROW(STATUS_INVALID_PARAMETER_1, L"The given process file does not exist.");

    if(GetFullPathName(InEXEPath, MAX_PATH, CurrentDir, &FilePart) > MAX_PATH)
        THROW(STATUS_INVALID_PARAMETER_1, L"Full path information exceeds MAX_PATH characters.");

    // compute current directory...
    RtlCopyMemory(FullExePath, CurrentDir, sizeof(FullExePath));
    
    swprintf_s(FullCommandLine, MAX_CREATEPROCESS_COMMANDLINE, L"\"%s\" %s", FullExePath, InCommandLine);

    *FilePart = 0;

    // create suspended process
    StartInfo.cb = sizeof(StartInfo);
	StartInfo.wShowWindow = TRUE;

    if(!CreateProcessW(
		    FullExePath, 
		    FullCommandLine, 
            NULL, NULL,  
            FALSE, 
		    InCustomFlags | CREATE_SUSPENDED,
		    NULL,
		    CurrentDir,
		    &StartInfo,
		    &ProcessInfo))
	    THROW(STATUS_INVALID_PARAMETER, L"Unable to start process; please check the given parameters.");

    *OutProcessId = ProcessInfo.dwProcessId;
    *OutThreadId = ProcessInfo.dwThreadId;

    RETURN;

THROW_OUTRO:
FINALLY_OUTRO:
    {
        if(ProcessInfo.hProcess != NULL)
		    CloseHandle(ProcessInfo.hProcess);

        if(ProcessInfo.hThread != NULL)
		    CloseHandle(ProcessInfo.hThread);

        return NtStatus;
	}
}





EASYHOOK_NT_EXPORT RhCreateAndInject(
		WCHAR* InEXEPath,
        WCHAR* InCommandLine,
		ULONG InProcessCreationFlags,
		ULONG InInjectionOptions,
		WCHAR* InLibraryPath_x86,
		WCHAR* InLibraryPath_x64,
		PVOID InPassThruBuffer,
        ULONG InPassThruSize,
        ULONG* OutProcessId)
{
/*
Description:

    Creates a suspended process and immediately injects the user library.
    This is done BEFORE any of the usual process initialization is called.
    When the injection is made, NO thread has actually executed any instruction 
    so far... It is just like your library entry point is the first thing
    executed in such a process and you can allow the original execution to
    take place by calling RhWakeUpProcess() in the injected library. But even
    that is no requirement for the process to work...

Parameters:

    - InEXEPath

        A relative or absolute path to the EXE file of the process being created.

    - InCommandLine

        Optional command line parameters passed to the newly created process.

	- InProcessCreationFlags

		Custom process creation flags.

    - InInjectionOptions

        All flags can be combined.

        EASYHOOK_INJECT_DEFAULT: 
            
            No special behavior. The given libraries are expected to be unmanaged DLLs.
            Further they should export an entry point named 
            "NativeInjectionEntryPoint" (in case of 64-bit) and
            "_NativeInjectionEntryPoint@4" (in case of 32-bit). The expected entry point 
            signature is REMOTE_ENTRY_POINT.

        EASYHOOK_INJECT_MANAGED: 
        
            The given user library is a NET assembly. Further they should export a class
            named "EasyHook.InjectionLoader" with a static method named "Main". The
            signature of this method is expected to be "int (String)". Please refer
            to the managed injection loader of EasyHook for more information about
            writing such managed entry points.

        EASYHOOK_INJECT_STEALTH:

            Uses the experimental stealth thread creation. If it fails
            you may try it with default settings. 

    - InLibraryPath_x86

        A relative or absolute path to the 32-bit version of the user library being injected.
        If you don't want to inject into 32-Bit processes, you may set this parameter to NULL.

    - InLibraryPath_x64

        A relative or absolute path to the 64-bit version of the user library being injected.
        If you don't want to inject into 64-Bit processes, you may set this parameter to NULL.

    - InPassThruBuffer

        An optional buffer containg data to be passed to the injection entry point. Such data
        is available in both, the managed and unmanaged user library entry points.
        Set to NULL if no used.

    - InPassThruSize

        Specifies the size in bytes of the pass thru data. If "InPassThruBuffer" is NULL, this
        parameter shall also be zero.

    - OutProcessId

        Receives the PID of the newly created process.

*/
    ULONG       ProcessId = 0;
    ULONG       ThreadId = 0;
    HANDLE      hProcess = NULL;
    NTSTATUS    NtStatus;

    if(!IsValidPointer(OutProcessId, sizeof(ULONG)))
        THROW(STATUS_INVALID_PARAMETER_8, L"The given process ID storage is invalid.");

    // all other parameters are validate by called APIs...
	FORCE(RtlCreateSuspendedProcess(InEXEPath, InCommandLine, InProcessCreationFlags, &ProcessId, &ThreadId));


    // inject library
    FORCE(RhInjectLibrary(
		    ProcessId,
		    ThreadId,
		    InInjectionOptions,
		    InLibraryPath_x86,
		    InLibraryPath_x64,
		    InPassThruBuffer,
            InPassThruSize));

    *OutProcessId = ProcessId;

    RETURN;

THROW_OUTRO:
    {
        if(ProcessId != 0)
        {
            hProcess = OpenProcess(PROCESS_ALL_ACCESS, FALSE, ProcessId);
            
            TerminateProcess(hProcess, 0);

            CloseHandle(hProcess);
        }
    }
FINALLY_OUTRO:
    return NtStatus;
}

#ifndef _DEBUG
    #pragma optimize ("", on)
#endif






BOOL EASYHOOK_API GetRemoteModuleExportDirectory(HANDLE hProcess, HMODULE hRemote, PIMAGE_EXPORT_DIRECTORY ExportDirectory, IMAGE_DOS_HEADER DosHeader, IMAGE_NT_HEADERS NtHeaders) {
/*
Description:
	
	Retrieves the export dictionary for the provided module.

Parameters:

	- hProcess

		The handle to the remote process to read from

	- hRemote

		The handle to the remote module to retrieve the export dictionary for

	- ExportDictionary

		Will contain the resulting export dictionary

	- DosHeader

		The preloaded DOS PE Header
		e.g.: ReadProcessMemory(hProcess, (void*)hRemote, &DosHeader, sizeof(IMAGE_DOS_HEADER), NULL)
        
	- NtHeaders

		The preloaded NT PE headers
		e.g.: dwNTHeaders = (PDWORD)((DWORD)hRemote + DosHeader.e_lfanew);
			  ReadProcessMemory(hProcess, dwNTHeaders, &NtHeaders, sizeof(IMAGE_NT_HEADERS), NULL)
*/
    PUCHAR ucAllocatedPEHeader;
	PIMAGE_SECTION_HEADER pImageSectionHeader;
	int i;
	DWORD dwEATAddress;

	if(!ExportDirectory)
        return FALSE;

	ucAllocatedPEHeader = (PUCHAR)malloc(1000 * sizeof(UCHAR));

    memset(ExportDirectory, 0, sizeof(IMAGE_EXPORT_DIRECTORY));


    if(!ReadProcessMemory(hProcess, (void*)hRemote, ucAllocatedPEHeader, (SIZE_T)1000, NULL))
        return FALSE;
    
    pImageSectionHeader = (PIMAGE_SECTION_HEADER)(ucAllocatedPEHeader + DosHeader.e_lfanew + sizeof(IMAGE_NT_HEADERS));
    
    for(i = 0; i < NtHeaders.FileHeader.NumberOfSections; i++, pImageSectionHeader++) {
        if(!pImageSectionHeader)
            continue;
        
        if(_stricmp((char*)pImageSectionHeader->Name, ".edata") == 0) {
            if(!ReadProcessMemory(hProcess, (void*)pImageSectionHeader->VirtualAddress, ExportDirectory, sizeof(IMAGE_EXPORT_DIRECTORY), NULL))
                continue;
            
			
			free(ucAllocatedPEHeader);
            return TRUE;
        }
    
    }
    
    dwEATAddress = NtHeaders.OptionalHeader.DataDirectory[0].VirtualAddress;
    if(!dwEATAddress)
        return FALSE;
    
    if(!ReadProcessMemory(hProcess, (void*)((DWORD_PTR)hRemote + dwEATAddress), ExportDirectory, sizeof(IMAGE_EXPORT_DIRECTORY), NULL))
        return FALSE;
    
    free(ucAllocatedPEHeader);
    return TRUE;
}





HMODULE GetRemoteModuleHandle(unsigned long pId, char *module)
{
/*
Description:

	Get Remote Module Handle retrieves a handle to the specified module within the provided process Id

Parameters:

	- pId

		The Id of the target process to find the module within

	- module

		The name of the module

Example:

	GetRemoteModuleHandle(PID, "kernel32.dll");
*/
	MODULEENTRY32 modEntry;
	HANDLE tlh = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE, pId);
	char moduleChar[256] = { 0 };

	modEntry.dwSize = sizeof(MODULEENTRY32);
	Module32First(tlh, &modEntry);
	do
	{
        size_t i;
		wcstombs_s(&i, moduleChar, 256, modEntry.szModule, 256);
		if(!_stricmp(moduleChar, module))
		{
			CloseHandle(tlh);
			return modEntry.hModule;
		}
		modEntry.dwSize = sizeof(MODULEENTRY32);
	}
	while(Module32Next(tlh, &modEntry));

	CloseHandle(tlh);
	return NULL;
}





void * GetRemoteFuncAddress(unsigned long pId, HANDLE hProcess, char* module, char* func) {
/*
Description:

	Get remote function address for the module within the remote process.

	This is done by retrieving the exports found within the specified module and
	finding a match for "func". The export name must exactly match "func".

Parameters:

	- pId
		
		The Id of the remote process

	- hProcess

		The handle of the remote process as returned by a call to OpenProcess

	- module

		The name of the module that contains the function within the remote process

	- func

		The name of the function within the module to find the address for

Example:

	INT_PTR fAddress = GetRemoteFuncAddress(pId, hProcess, "kernel32.dll", "GetProcAddress");
*/
	HMODULE hRemote = GetRemoteModuleHandle(pId, module);
    IMAGE_DOS_HEADER DosHeader;
    IMAGE_NT_HEADERS NtHeaders;
	IMAGE_EXPORT_DIRECTORY EATDirectory;

    DWORD*    AddressOfFunctions;
	DWORD*    AddressOfNames;
	WORD*    AddressOfOrdinals;

	unsigned int i;

	DWORD_PTR dwExportBase;
	DWORD_PTR dwExportSize;
	DWORD_PTR dwAddressOfFunction;
	DWORD_PTR dwAddressOfName;

	char pszFunctionName[256] = { 0 };
	char pszRedirectName[256] = { 0 };
	char pszModuleName[256] = { 0 };
    char pszFunctionRedi[256] = { 0 };

    int a = 0;
	int b = 0;

	WORD OrdinalValue;

	DWORD_PTR dwAddressOfRedirectedFunction;
	DWORD_PTR dwAddressOfRedirectedName;

	char pszRedirectedFunctionName[ 256 ] = { 0 };

	if(!hRemote)
        return NULL;
    
	// Load DOS PE header
    if(!ReadProcessMemory(hProcess, (void*)hRemote, &DosHeader, sizeof(IMAGE_DOS_HEADER), NULL) || DosHeader.e_magic != IMAGE_DOS_SIGNATURE)
        return NULL;

	// Load NT PE headers
    if(!ReadProcessMemory(hProcess, (void *)((DWORD_PTR)hRemote + DosHeader.e_lfanew), &NtHeaders, sizeof(IMAGE_NT_HEADERS), NULL) || NtHeaders.Signature != IMAGE_NT_SIGNATURE)
        return NULL;

    // Load image export directory
    if(!GetRemoteModuleExportDirectory(hProcess, hRemote, &EATDirectory, DosHeader, NtHeaders))
        return NULL;

	// Allocate room for all the function information
    AddressOfFunctions	= (DWORD*)malloc(EATDirectory.NumberOfFunctions * sizeof(DWORD));
    AddressOfNames      = (DWORD*)malloc(EATDirectory.NumberOfNames * sizeof(DWORD));
    AddressOfOrdinals   = (WORD*)malloc(EATDirectory.NumberOfNames * sizeof(WORD));

	// Read function address locations
    if(!ReadProcessMemory(hProcess, (void*)((DWORD_PTR)hRemote + (DWORD_PTR)EATDirectory.AddressOfFunctions), AddressOfFunctions, EATDirectory.NumberOfFunctions * sizeof(DWORD), NULL)) {
        free(AddressOfFunctions);
        free(AddressOfNames);
        free(AddressOfOrdinals);
        return NULL;
    }

	// Read function name locations
    if(!ReadProcessMemory(hProcess, (void*)((DWORD_PTR)hRemote + (DWORD_PTR)EATDirectory.AddressOfNames), AddressOfNames, EATDirectory.NumberOfNames * sizeof(DWORD), NULL)) {
        free(AddressOfFunctions);
        free(AddressOfNames);
        free(AddressOfOrdinals);
        return NULL;
    }

	// Read function name ordinal locations
    if(!ReadProcessMemory(hProcess, (void*)((DWORD_PTR)hRemote + (DWORD_PTR)EATDirectory.AddressOfNameOrdinals), AddressOfOrdinals, EATDirectory.NumberOfNames * sizeof(WORD), NULL)) {
        free(AddressOfFunctions);
        free(AddressOfNames);
        free(AddressOfOrdinals);
        return NULL;
    }

    dwExportBase = ((DWORD_PTR)hRemote + NtHeaders.OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_EXPORT].VirtualAddress);
    dwExportSize = (dwExportBase + NtHeaders.OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_EXPORT].Size);

	// Check each name for a match
    for(i = 0; i < EATDirectory.NumberOfNames; ++i) {
        dwAddressOfFunction    = (DWORD_PTR)hRemote + AddressOfFunctions[i];
        dwAddressOfName        = (DWORD_PTR)hRemote + AddressOfNames[i];

		memset(&pszFunctionName, 0, 256);

        if(!ReadProcessMemory(hProcess, (void*)dwAddressOfName, pszFunctionName, 256, NULL))
            continue;

		// Skip until we find the matching function name
        if(_stricmp(pszFunctionName, func) != 0)
            continue;

		// Check if address of function is found in another module
        if(dwAddressOfFunction >= dwExportBase && dwAddressOfFunction <= dwExportSize) {
            memset(&pszRedirectName, 0, 256);

            if(!ReadProcessMemory(hProcess, (void*)dwAddressOfFunction, pszRedirectName, 256, NULL))
                continue;

            memset(&pszModuleName, 0, 256);
			memset(&pszFunctionRedi, 0, 256);

            a = 0;
            for(; pszRedirectName[a] != '.'; a++)
                pszModuleName[a] = pszRedirectName[a];
            a++;
            pszModuleName[a] = '\0';

            b = 0;
            for(; pszRedirectName[a] != '\0'; a++, b++)
                pszFunctionRedi[b] = pszRedirectName[a];
            b++;
            pszFunctionRedi[b] = '\0';

            strcat_s(pszModuleName, 256, ".dll");

            free(AddressOfFunctions);
            free(AddressOfNames);
            free(AddressOfOrdinals);

            return GetRemoteFuncAddress(pId, hProcess, pszModuleName, pszFunctionRedi);
        }

        OrdinalValue = AddressOfOrdinals[i];

        if (OrdinalValue >= EATDirectory.NumberOfNames)
        {
            return NULL;
        }

		// If ordinal doesn't match index retrieve correct address
        if(OrdinalValue != i) {
            dwAddressOfRedirectedFunction	= ((DWORD_PTR)hRemote + (DWORD_PTR)AddressOfFunctions[OrdinalValue]);
            dwAddressOfRedirectedName		= ((DWORD_PTR)hRemote + (DWORD_PTR)AddressOfNames[OrdinalValue]);

            memset(&pszRedirectedFunctionName, 0, 256);

            free(AddressOfFunctions);
            free(AddressOfNames);
            free(AddressOfOrdinals);

            if(!ReadProcessMemory(hProcess, (void*)dwAddressOfRedirectedName, pszRedirectedFunctionName, 256, NULL))
                return NULL;
            else
                return (void*)dwAddressOfRedirectedFunction;
        }
		// Otherwise return the address
        else {
            free(AddressOfFunctions);
            free(AddressOfNames);
            free(AddressOfOrdinals);

            return (void*)dwAddressOfFunction;
        }
    }

    free(AddressOfFunctions);
    free(AddressOfNames);
    free(AddressOfOrdinals);

    return NULL;
}





EASYHOOK_NT_EXPORT RhInjectLibrary(
		ULONG InTargetPID,
		ULONG InWakeUpTID,
		ULONG InInjectionOptions,
		WCHAR* InLibraryPath_x86,
		WCHAR* InLibraryPath_x64,
		PVOID InPassThruBuffer,
        ULONG InPassThruSize)
{
/*
Description:

    Injects a library into the target process. This is a very stable operation.
    The problem so far is, that only the NET layer will support injection
    through WOW64 boundaries and into other terminal sessions. It is quite
    complex to realize with unmanaged code and that's why it is not supported!

    If you really need this feature I highly recommend to at least look at C++.NET
    because using the managed injection can speed up your development progress
    about orders of magnitudes. I know by experience that writing the required
    multi-process injection code in any unmanaged language is a rather daunting task!

Parameters:

    - InTargetPID

        The process in which the library should be injected.
    
    - InWakeUpTID

        If the target process was created suspended (RhCreateAndInject), then
        this parameter should be set to the main thread ID of the target.
        You may later resume the process from within the injected library
        by calling RhWakeUpProcess(). If the process is already running, you
        should specify zero.

    - InInjectionOptions

        All flags can be combined.

        EASYHOOK_INJECT_DEFAULT: 
            
            No special behavior. The given libraries are expected to be unmanaged DLLs.
            Further they should export an entry point named 
            "NativeInjectionEntryPoint" (in case of 64-bit) and
            "_NativeInjectionEntryPoint@4" (in case of 32-bit). The expected entry point 
            signature is REMOTE_ENTRY_POINT.

        EASYHOOK_INJECT_MANAGED: 
        
            The given user library is a NET assembly. Further they should export a class
            named "EasyHook.InjectionLoader" with a static method named "Main". The
            signature of this method is expected to be "int (String)". Please refer
            to the managed injection loader of EasyHook for more information about
            writing such managed entry points.

        EASYHOOK_INJECT_STEALTH:

            Uses the experimental stealth thread creation. If it fails
            you may try it with default settings. 

		EASYHOOK_INJECT_HEART_BEAT:
			
			Is only used internally to workaround the managed process creation bug.
			For curiosity, NET seems to hijack our remote thread if a managed process
			is created suspended. It doesn't do anything with the suspended main thread,


    - InLibraryPath_x86

        A relative or absolute path to the 32-bit version of the user library being injected.
        If you don't want to inject into 32-Bit processes, you may set this parameter to NULL.

    - InLibraryPath_x64

        A relative or absolute path to the 64-bit version of the user library being injected.
        If you don't want to inject into 64-Bit processes, you may set this parameter to NULL.

    - InPassThruBuffer

        An optional buffer containg data to be passed to the injection entry point. Such data
        is available in both, the managed and unmanaged user library entry points.
        Set to NULL if no used.

    - InPassThruSize

        Specifies the size in bytes of the pass thru data. If "InPassThruBuffer" is NULL, this
        parameter shall also be zero.

Returns:

    

*/
	HANDLE					hProc = NULL;
	HANDLE					hRemoteThread = NULL;
	HANDLE					hSignal = NULL;
	UCHAR*					RemoteInjectCode = NULL;
	LPREMOTE_INFO			Info = NULL;
    LPREMOTE_INFO           RemoteInfo = NULL;
	ULONG					RemoteInfoSize = 0;
	BYTE*					Offset = 0;
    ULONG                   CodeSize;
    BOOL                    Is64BitTarget;
    NTSTATUS				NtStatus;
    LONGLONG                Diff;
    HANDLE                  Handles[2];

    ULONG                   UserLibrarySize;
    ULONG                   PATHSize;
    ULONG                   EasyHookPathSize;
    ULONG                   EasyHookEntrySize;
    ULONG                   Code;

    SIZE_T                  BytesWritten;
    WCHAR                   UserLibrary[MAX_PATH+1];
    WCHAR					PATH[MAX_PATH + 1];
    WCHAR					EasyHookPath[MAX_PATH + 1];
#ifdef _M_X64
	CHAR*					EasyHookEntry = "HookCompleteInjection";
#else
	CHAR*					EasyHookEntry = "_HookCompleteInjection@4";
#endif

    // validate parameters
    if(InPassThruSize > MAX_PASSTHRU_SIZE)
        THROW(STATUS_INVALID_PARAMETER_7, L"The given pass thru buffer is too large.");

    if(InPassThruBuffer != NULL)
    {
        if(!IsValidPointer(InPassThruBuffer, InPassThruSize))
            THROW(STATUS_INVALID_PARAMETER_6, L"The given pass thru buffer is invalid.");
    }
    else if(InPassThruSize != 0)
        THROW(STATUS_INVALID_PARAMETER_7, L"If no pass thru buffer is specified, the pass thru length also has to be zero.");

	if(InTargetPID == GetCurrentProcessId())
		THROW(STATUS_NOT_SUPPORTED, L"For stability reasons it is not supported to inject into the calling process.");

	// open target process
	if((hProc = OpenProcess(PROCESS_ALL_ACCESS, FALSE, InTargetPID)) == NULL)
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
        THROW(STATUS_WOW_ASSERTION, L"It is not supported to directly hook through the WOW64 barrier.");

    if(!GetFullPathNameW(InLibraryPath_x64, MAX_PATH, UserLibrary, NULL))
        THROW(STATUS_INVALID_PARAMETER_5, L"Unable to get full path to the given 64-bit library.");
#else
	FORCE(RhIsX64Process(InTargetPID, &Is64BitTarget));
      
    if(Is64BitTarget)
        THROW(STATUS_WOW_ASSERTION, L"It is not supported to directly hook through the WOW64 barrier.");

	if(!GetFullPathNameW(InLibraryPath_x86, MAX_PATH, UserLibrary, NULL))
        THROW(STATUS_INVALID_PARAMETER_4, L"Unable to get full path to the given 32-bit library.");
#endif

	/*
		Validate library path...
	*/
	if(!RtlFileExists(UserLibrary))
    {
    #ifdef _M_X64
        THROW(STATUS_INVALID_PARAMETER_5, L"The given 64-Bit library does not exist!");
    #else
        THROW(STATUS_INVALID_PARAMETER_4, L"The given 32-Bit library does not exist!");
    #endif
    }

	// import strings...
    RtlGetWorkingDirectory(PATH, MAX_PATH - 1);
    RtlGetCurrentModulePath(EasyHookPath, MAX_PATH);

	// allocate remote information block
    EasyHookPathSize = (RtlUnicodeLength(EasyHookPath) + 1) * 2;
    EasyHookEntrySize = (RtlAnsiLength(EasyHookEntry) + 1);
    PATHSize = (RtlUnicodeLength(PATH) + 1 + 1) * 2;
    UserLibrarySize = (RtlUnicodeLength(UserLibrary) + 1 + 1) * 2;

    PATH[PATHSize / 2 - 2] = ';';
    PATH[PATHSize / 2 - 1] = 0;

	RemoteInfoSize = EasyHookPathSize + EasyHookEntrySize + PATHSize + InPassThruSize + UserLibrarySize;

	RemoteInfoSize += sizeof(REMOTE_INFO);

	if((Info = (LPREMOTE_INFO)RtlAllocateMemory(TRUE, RemoteInfoSize)) == NULL)
		THROW(STATUS_NO_MEMORY, L"Unable to allocate memory in current process.");

	// Ensure that if we have injected into a suspended process that we can retrieve the remote function addresses
	FORCE(NtForceLdrInitializeThunk(hProc));

	// Determine function addresses within remote process
    Info->LoadLibraryW   = (PVOID)GetRemoteFuncAddress(InTargetPID, hProc, "kernel32.dll", "LoadLibraryW");
	Info->FreeLibrary    = (PVOID)GetRemoteFuncAddress(InTargetPID, hProc, "kernel32.dll", "FreeLibrary");
	Info->GetProcAddress = (PVOID)GetRemoteFuncAddress(InTargetPID, hProc, "kernel32.dll", "GetProcAddress");
	Info->VirtualFree    = (PVOID)GetRemoteFuncAddress(InTargetPID, hProc, "kernel32.dll", "VirtualFree");
	Info->VirtualProtect = (PVOID)GetRemoteFuncAddress(InTargetPID, hProc, "kernel32.dll", "VirtualProtect");
	Info->ExitThread     = (PVOID)GetRemoteFuncAddress(InTargetPID, hProc, "kernel32.dll", "ExitThread");
	Info->GetLastError   = (PVOID)GetRemoteFuncAddress(InTargetPID, hProc, "kernel32.dll", "GetLastError");

    Info->WakeUpThreadID = InWakeUpTID;
    Info->IsManaged = InInjectionOptions & EASYHOOK_INJECT_MANAGED;

	// allocate memory in target process
	CodeSize = GetInjectionSize();

	if((RemoteInjectCode = (BYTE*)VirtualAllocEx(hProc, NULL, CodeSize + RemoteInfoSize, MEM_COMMIT, PAGE_EXECUTE_READWRITE)) == NULL)
        THROW(STATUS_NO_MEMORY, L"Unable to allocate memory in target process.");

	// save strings
	Offset = (BYTE*)(Info + 1);

	Info->EasyHookEntry = (char*)Offset;
	Info->EasyHookPath = (wchar_t*)(Offset += EasyHookEntrySize);
	Info->PATH = (wchar_t*)(Offset += EasyHookPathSize);
	Info->UserData = (BYTE*)(Offset += PATHSize);
    Info->UserLibrary = (WCHAR*)(Offset += InPassThruSize);

	Info->Size = RemoteInfoSize;
	Info->HostProcess = GetCurrentProcessId();
	Info->UserDataSize = 0;

	Offset += UserLibrarySize;

	if((ULONG)(Offset - ((BYTE*)Info)) > Info->Size)
        THROW(STATUS_BUFFER_OVERFLOW, L"A buffer overflow in internal memory was detected.");

	RtlCopyMemory(Info->EasyHookPath, EasyHookPath, EasyHookPathSize);
	RtlCopyMemory(Info->PATH, PATH, PATHSize);
	RtlCopyMemory(Info->EasyHookEntry, EasyHookEntry, EasyHookEntrySize);
    RtlCopyMemory(Info->UserLibrary, UserLibrary, UserLibrarySize);


	if(InPassThruBuffer != NULL)
	{
		RtlCopyMemory(Info->UserData, InPassThruBuffer, InPassThruSize);

		Info->UserDataSize = InPassThruSize;
	}

	// copy code into target process
	if(!WriteProcessMemory(hProc, RemoteInjectCode, GetInjectionPtr(), CodeSize, &BytesWritten) || (BytesWritten != CodeSize))
		THROW(STATUS_INTERNAL_ERROR, L"Unable to write into target process memory.");

	// create and export signal event>
	if((hSignal = CreateEvent(NULL, TRUE, FALSE, NULL)) == NULL)
        THROW(STATUS_INSUFFICIENT_RESOURCES, L"Unable to create event.");

	// Possible resource leck: the remote handles cannt be closed here if an error occurs
	if(!DuplicateHandle(GetCurrentProcess(), hSignal, hProc, &Info->hRemoteSignal, EVENT_ALL_ACCESS, FALSE, 0))
		THROW(STATUS_INTERNAL_ERROR, L"Failed to duplicate remote event.");

	// relocate remote information
	RemoteInfo = (LPREMOTE_INFO)(RemoteInjectCode + CodeSize);
	Diff = ((BYTE*)RemoteInfo - (BYTE*)Info);

	Info->EasyHookEntry = (char*)(((BYTE*)Info->EasyHookEntry) + Diff);
	Info->EasyHookPath = (wchar_t*)(((BYTE*)Info->EasyHookPath) + Diff);
	Info->PATH = (wchar_t*)(((BYTE*)Info->PATH) + Diff);
    Info->UserLibrary = (wchar_t*)(((BYTE*)Info->UserLibrary) + Diff);

	if(Info->UserData != NULL)
		Info->UserData = (BYTE*)(((BYTE*)Info->UserData) + Diff);

	Info->RemoteEntryPoint = RemoteInjectCode;

	if(!WriteProcessMemory(hProc, RemoteInfo, Info, RemoteInfoSize, &BytesWritten) || (BytesWritten != RemoteInfoSize))
		THROW(STATUS_INTERNAL_ERROR, L"Unable to write into target process memory.");

	if((InInjectionOptions & EASYHOOK_INJECT_STEALTH) != 0)
	{
		FORCE(RhCreateStealthRemoteThread(InTargetPID, (LPTHREAD_START_ROUTINE)RemoteInjectCode, RemoteInfo, &hRemoteThread));
	}
	else
	{
		if(!RTL_SUCCESS(NtCreateThreadEx(hProc, (LPTHREAD_START_ROUTINE)RemoteInjectCode, RemoteInfo, FALSE, &hRemoteThread)))
		{
			// create remote thread and wait for injection completion
			if((hRemoteThread = CreateRemoteThread(hProc, NULL, 0, (LPTHREAD_START_ROUTINE)RemoteInjectCode, RemoteInfo, 0, NULL)) == NULL)
				THROW(STATUS_ACCESS_DENIED, L"Unable to create remote thread.");
		}
	}

	/*
	 * The assembler codes are designed to let us derive extensive error information...
	*/
    Handles[1] = hSignal;
	Handles[0] = hRemoteThread;

	Code = WaitForMultipleObjects(2, Handles, FALSE, INFINITE);

	if(Code == WAIT_OBJECT_0)
	{
		// parse error code
		GetExitCodeThread(hRemoteThread, &Code);

		SetLastError(Code & 0x0FFFFFFF);

		switch(Code & 0xF0000000)
		{
		case 0x10000000: THROW(STATUS_INTERNAL_ERROR, L"Unable to find internal entry point.");
		case 0x20000000: THROW(STATUS_INTERNAL_ERROR, L"Unable to make stack executable.");
		case 0x30000000: THROW(STATUS_INTERNAL_ERROR, L"Unable to release injected library.");
		case 0x40000000: THROW(STATUS_INTERNAL_ERROR, L"Unable to find EasyHook library in target process context.");
		case 0xF0000000: // error in C++ injection completion
			{
				switch(Code & 0xFF)
				{
#ifdef _M_X64
                case 20: THROW(STATUS_INVALID_PARAMETER_5, L"Unable to load the given 64-bit library into target process.");
                case 21: THROW(STATUS_INVALID_PARAMETER_5, L"Unable to find the required native entry point in the given 64-bit library.");
                case 12: THROW(STATUS_INVALID_PARAMETER_5, L"Unable to find the required managed entry point in the given 64-bit library.");
#else
                case 20: THROW(STATUS_INVALID_PARAMETER_4, L"Unable to load the given 32-bit library into target process.");
                case 21: THROW(STATUS_INVALID_PARAMETER_4, L"Unable to find the required native entry point in the given 32-bit library.");
                case 12: THROW(STATUS_INVALID_PARAMETER_4, L"Unable to find the required managed entry point in the given 32-bit library.");
#endif
                
                case 13: THROW(STATUS_DLL_INIT_FAILED, L"The user defined managed entry point failed in the target process. Make sure that EasyHook is registered in the GAC. Refer to event logs for more information.");
				case 1: THROW(STATUS_INTERNAL_ERROR, L"Unable to allocate memory in target process.");
				case 2: THROW(STATUS_INTERNAL_ERROR, L"Unable to adjust target's PATH variable.");
                case 10: THROW(STATUS_INTERNAL_ERROR, L"Unable to load 'mscoree.dll' into target process.");
				case 11: THROW(STATUS_INTERNAL_ERROR, L"Unable to bind NET Runtime to target process.");
				case 22: THROW(STATUS_INTERNAL_ERROR, L"Unable to signal remote event.");
				default: THROW(STATUS_INTERNAL_ERROR, L"Unknown error in injected C++ completion routine.");
				}
			}break;
		case 0:
			THROW(STATUS_INTERNAL_ERROR, L"C++ completion routine has returned success but didn't raise the remote event.");
		default:
			THROW(STATUS_INTERNAL_ERROR, L"Unknown error in injected assembler code.");
		}
	}
	else if(Code != WAIT_OBJECT_0 + 1)
		THROW(STATUS_INTERNAL_ERROR, L"Unable to wait for injection completion due to timeout. ");

    RETURN;

THROW_OUTRO:
FINALLY_OUTRO:
    {
		// release resources
		if(hProc != NULL)
			CloseHandle(hProc);

		if(Info != NULL)
			RtlFreeMemory(Info);

		if(hRemoteThread != NULL)
			CloseHandle(hRemoteThread);

		if(hSignal != NULL)
			CloseHandle(hSignal);

        return NtStatus;
	}
}

/*/////////////////////////////////////////////////////////////////////////////////////
//////////////////////////////////////////////////// GetInjectionSize
///////////////////////////////////////////////////////////////////////////////////////

Dynamically retrieves the size of the trampoline method.
*/
static DWORD ___InjectionSize = 0;

#ifdef _M_X64
	EXTERN_C void Injection_ASM_x64();
#else
	EXTERN_C void __stdcall Injection_ASM_x86();
#endif

BYTE* GetInjectionPtr()
{
#ifdef _M_X64
	BYTE* Ptr = (BYTE*)Injection_ASM_x64;
#else
	BYTE* Ptr = (BYTE*)Injection_ASM_x86;
#endif

// bypass possible VS2008 debug jump table
	if(*Ptr == 0xE9)
		Ptr += *((int*)(Ptr + 1)) + 5;

	return Ptr;
}

ULONG GetInjectionSize()
{
    UCHAR*          Ptr;
    UCHAR*          BasePtr;
    ULONG           Index;
    ULONG           Signature;

	if(___InjectionSize != 0)
		return ___InjectionSize;
	
	// search for signature
	BasePtr = Ptr = GetInjectionPtr();

	for(Index = 0; Index < 2000 /* some always large enough value*/; Index++)
	{
		Signature = *((ULONG*)Ptr);

		if(Signature == 0x12345678)	
		{
			___InjectionSize = (ULONG)(Ptr - BasePtr);

			return ___InjectionSize;
		}

		Ptr++;
	}

	ASSERT(FALSE,L"thread.c - ULONG GetInjectionSize()");

    return 0;
}








EASYHOOK_NT_EXPORT TestFuncHooks(ULONG pId, 
        PCHAR module,
        TEST_FUNC_HOOKS_OPTIONS options,
        TEST_FUNC_HOOKS_RESULT** outResults,
        int* resultCount)
{
/*
Description:

    Tests whether it is possible to hook DLL exports found within the specified module.

Parameters:

    - pId

        The process Id to test.
    
    - module

        The name of the module to look for exports within.

    - options

        Optionally specifies whether to output the results to a text file, or
        the name of a single export to test.

    - outResults

        Returns the array of results. This should be freed by a subsequent 
        call to ReleaseTestFuncHookResults.

    - resultCount

        The number of items added to outResults.

Returns:

    NTSTATUS

*/
    FILE *f = NULL;
    HANDLE hProcess;

    HMODULE hRemote = GetRemoteModuleHandle(pId, module);
    IMAGE_DOS_HEADER DosHeader;
    IMAGE_NT_HEADERS NtHeaders;
	IMAGE_EXPORT_DIRECTORY EATDirectory;

    DWORD*    AddressOfFunctions;
	DWORD*    AddressOfNames;
	WORD*    AddressOfOrdinals;

	unsigned int i;

	DWORD_PTR dwExportBase;
	DWORD_PTR dwExportSize;
	DWORD_PTR dwAddressOfFunction;
	DWORD_PTR dwAddressOfName;

	char pszFunctionName[256] = { 0 };
	char pszRedirectName[256] = { 0 };
	char pszModuleName[256] = { 0 };
    char pszFunctionRedi[256] = { 0 };

    unsigned int a = 0;
	int b = 0;

	WORD OrdinalValue;

	DWORD_PTR dwAddressOfRedirectedFunction;
	DWORD_PTR dwAddressOfRedirectedName;

    ULONG asmLength;
    ULONG64 nextInstr;
    CHAR buf[MAX_PATH];

    CHAR asmBuf[MAX_PATH];
    unsigned char *opcodes;
    ULONG entryPointSize = 0;
    UCHAR entryPoint[ 256 ] = { 0 };
    DWORD_PTR pEntryPoint = 0;
    LOCAL_HOOK_INFO* hookBuf = NULL;
    ULONG relocBufSize = 0;

    CHAR outputBuf[1024] = { 0 };

    TEST_FUNC_HOOKS_RESULT* results = NULL;
    TEST_FUNC_HOOKS_RESULT* result;
    unsigned int count = 0;

	char pszRedirectedFunctionName[ 256 ] = { 0 };

    // Initialise results
    *resultCount = 0;
    *outResults = NULL;

    // open target process
    if((hProcess = OpenProcess(PROCESS_ALL_ACCESS, FALSE, pId)) == NULL)
	{
		if(GetLastError() == ERROR_ACCESS_DENIED)
            return STATUS_ACCESS_DENIED;
		else
			return STATUS_NOT_FOUND;
	}

	if(!hRemote)
        return STATUS_NOT_FOUND;
    
	// Load DOS PE header
    if(!ReadProcessMemory(hProcess, (void*)hRemote, &DosHeader, sizeof(IMAGE_DOS_HEADER), NULL) || DosHeader.e_magic != IMAGE_DOS_SIGNATURE)
        return STATUS_NOT_FOUND;

	// Load NT PE headers
    if(!ReadProcessMemory(hProcess, (void *)((DWORD_PTR)hRemote + DosHeader.e_lfanew), &NtHeaders, sizeof(IMAGE_NT_HEADERS), NULL) || NtHeaders.Signature != IMAGE_NT_SIGNATURE)
        return STATUS_NOT_FOUND;

    // Load image export directory
    if(!GetRemoteModuleExportDirectory(hProcess, hRemote, &EATDirectory, DosHeader, NtHeaders))
        return STATUS_NOT_FOUND;

	// Allocate room for all the function information
    AddressOfFunctions	= (DWORD*)malloc(EATDirectory.NumberOfFunctions * sizeof(DWORD));
    AddressOfNames      = (DWORD*)malloc(EATDirectory.NumberOfNames * sizeof(DWORD));
    AddressOfOrdinals   = (WORD*)malloc(EATDirectory.NumberOfNames * sizeof(WORD));

	// Read function address locations
    if(!ReadProcessMemory(hProcess, (void*)((DWORD_PTR)hRemote + (DWORD_PTR)EATDirectory.AddressOfFunctions), AddressOfFunctions, EATDirectory.NumberOfFunctions * sizeof(DWORD), NULL)) {
        free(AddressOfFunctions);
        free(AddressOfNames);
        free(AddressOfOrdinals);
        return STATUS_NOT_FOUND;
    }

	// Read function name locations
    if(!ReadProcessMemory(hProcess, (void*)((DWORD_PTR)hRemote + (DWORD_PTR)EATDirectory.AddressOfNames), AddressOfNames, EATDirectory.NumberOfNames * sizeof(DWORD), NULL)) {
        free(AddressOfFunctions);
        free(AddressOfNames);
        free(AddressOfOrdinals);
        return STATUS_NOT_FOUND;
    }

	// Read function name ordinal locations
    if(!ReadProcessMemory(hProcess, (void*)((DWORD_PTR)hRemote + (DWORD_PTR)EATDirectory.AddressOfNameOrdinals), AddressOfOrdinals, EATDirectory.NumberOfNames * sizeof(WORD), NULL)) {
        free(AddressOfFunctions);
        free(AddressOfNames);
        free(AddressOfOrdinals);
        return STATUS_NOT_FOUND;
    }

    dwExportBase = ((DWORD_PTR)hRemote + NtHeaders.OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_EXPORT].VirtualAddress);
    dwExportSize = (dwExportBase + NtHeaders.OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_EXPORT].Size);

    // Determine result count
    for(i = 0; i < EATDirectory.NumberOfNames; ++i) {
        dwAddressOfFunction    = (DWORD_PTR)hRemote + AddressOfFunctions[i];
        dwAddressOfName        = (DWORD_PTR)hRemote + AddressOfNames[i];

		memset(&pszFunctionName, 0, 256);

        if(!ReadProcessMemory(hProcess, (void*)dwAddressOfName, pszFunctionName, 256, NULL))
            continue;

        // Skip until we find the matching function name
        if (options.FilterByName != NULL && strlen(options.FilterByName) > 0)
        {
            if (_stricmp(pszFunctionName, options.FilterByName) == 0)
                count++;
        }
        else 
        {
            count++;
        }
    }

    // Allocate memory for the results
    *resultCount = count;
    results = (TEST_FUNC_HOOKS_RESULT *)CoTaskMemAlloc(count * sizeof(TEST_FUNC_HOOKS_RESULT));
    *outResults = results;

    if (results == NULL)
    {
        *outResults = NULL;
        *resultCount = 0;
        return STATUS_NO_MEMORY;
    }

    memset(results, 0, count * sizeof(TEST_FUNC_HOOKS_RESULT));

    for (a = 0; a < count; a++) {
        result = &results[a];
        result->FnName = (LPSTR)CoTaskMemAlloc(MAX_PATH);
        memset(result->FnName, 0, MAX_PATH);

        result->ModuleRedirect = (LPSTR)CoTaskMemAlloc(MAX_PATH);
        memset(result->ModuleRedirect, 0, MAX_PATH);

        result->FnRedirect = (LPSTR)CoTaskMemAlloc(MAX_PATH);
        memset(result->FnRedirect, 0, MAX_PATH);

        result->EntryDisasm = (LPSTR)CoTaskMemAlloc(1024);
        memset(result->EntryDisasm, 0, 1024);

        result->RelocDisasm = (LPSTR)CoTaskMemAlloc(1024);
        memset(result->RelocDisasm, 0, 1024);

        result->Error = (LPSTR)CoTaskMemAlloc(1024);
        memset(result->Error, 0, 1024);
    }


    count = a = b = 0;

	// Check each name for a match
    for(i = 0; i < EATDirectory.NumberOfNames; ++i) {
        dwAddressOfFunction    = (DWORD_PTR)hRemote + AddressOfFunctions[i];
        dwAddressOfName        = (DWORD_PTR)hRemote + AddressOfNames[i];

		memset(&pszFunctionName, 0, 256);
        
        if(!ReadProcessMemory(hProcess, (void*)dwAddressOfName, pszFunctionName, 256, NULL))
            continue;

        memset(&outputBuf, 0, 1024);

        // Skip until we find the matching function name
        if (options.FilterByName != NULL && strlen(options.FilterByName) > 0 && (_stricmp(pszFunctionName, options.FilterByName) != 0))
            continue;

        // Get next result obj
        result = &results[count++];
        
        strcpy_s(result->FnName, MAX_PATH, pszFunctionName);
        
        // Check if address of function is found in another module
        if(dwAddressOfFunction >= dwExportBase && dwAddressOfFunction <= dwExportSize) {
            memset(&pszRedirectName, 0, 256);

            if(!ReadProcessMemory(hProcess, (void*)dwAddressOfFunction, pszRedirectName, 256, NULL))
                continue;

            memset(&pszModuleName, 0, 256);
			memset(&pszFunctionRedi, 0, 256);

            a = 0;
            for(; pszRedirectName[a] != '.'; a++)
                pszModuleName[a] = pszRedirectName[a];
            a++;
            pszModuleName[a] = '\0';

            b = 0;
            for(; pszRedirectName[a] != '\0'; a++, b++)
                pszFunctionRedi[b] = pszRedirectName[a];
            b++;
            pszFunctionRedi[b] = '\0';

            strcat_s(pszModuleName, 256, ".dll");
            strcpy_s(result->ModuleRedirect, MAX_PATH, pszModuleName);

            dwAddressOfFunction = (DWORD_PTR)GetRemoteFuncAddress(pId, hProcess, pszModuleName, pszFunctionRedi);
            if (dwAddressOfFunction == 0) {
                strcpy_s(result->Error, 1024, "DLL redirect unreadable");
                continue;
            }
            else {
                strcpy_s(result->FnRedirect, MAX_PATH, pszFunctionRedi);
            }
        }
        else {
            OrdinalValue = AddressOfOrdinals[i];

            if (OrdinalValue >= EATDirectory.NumberOfNames)
            {
                    strcpy_s(result->Error, 1024, "Ordinal redirect out of range");
                    continue;
            }

		    // If ordinal doesn't match index retrieve correct address
            if(OrdinalValue != i) {
                dwAddressOfRedirectedFunction	= ((DWORD_PTR)hRemote + (DWORD_PTR)AddressOfFunctions[OrdinalValue]);
                dwAddressOfRedirectedName		= ((DWORD_PTR)hRemote + (DWORD_PTR)AddressOfNames[OrdinalValue]);

                memset(&pszRedirectedFunctionName, 0, 256);

                if(!ReadProcessMemory(hProcess, (void*)dwAddressOfRedirectedName, pszRedirectedFunctionName, 256, NULL))
                {
                    
                    strcpy_s(result->Error, 1024, "Ordinal redirect unreadable");
                    continue;
                }
                else
                {
                    strcpy_s(result->FnRedirect, MAX_PATH, pszRedirectedFunctionName);
                    dwAddressOfFunction = dwAddressOfRedirectedFunction;
                }
            }
        }
        result->FnAddress = (void*)dwAddressOfFunction;
        
        entryPointSize = 0;

        a = 0;
        
        // 1. Allocate memory and prepare the hook
        if (!RTL_SUCCESS(LhAllocateHook((void*)dwAddressOfFunction, (void*)dwAddressOfFunction, NULL, &hookBuf, &relocBufSize)))
        {
            // Unable to allocate hook or unable to relocate instructions
            BOOL usedDefault = FALSE;
            WideCharToMultiByte(CP_ACP,
                                WC_COMPOSITECHECK | WC_DEFAULTCHAR,
                                RtlGetLastErrorString(),
                                lstrlenW(RtlGetLastErrorString()),
                                outputBuf,
                                1024,
                                "?",
                                &usedDefault);
            sprintf_s(result->Error, 1024, "Unable to allocate hook: %s", outputBuf);
            memset(&asmBuf, 0, MAX_PATH);
            pEntryPoint = dwAddressOfFunction;
            // Disassemble instructions
            while ((a < 2 || (pEntryPoint - dwAddressOfFunction < 5)) && RTL_SUCCESS(LhDisassembleInstruction((void*)pEntryPoint, &asmLength, buf, MAX_PATH, &nextInstr)))
            {
                opcodes = (unsigned char *)pEntryPoint;
                sprintf_s(asmBuf, 260, "\t");
                for (b = 0; b < (int)(nextInstr - pEntryPoint); b++)
                {
                    entryPoint[entryPointSize + b] = *opcodes;
                
                    sprintf_s(asmBuf + strlen(asmBuf), 260 - strlen(asmBuf), "%02X ", *opcodes);
                    opcodes++;
                }

                sprintf_s(result->EntryDisasm + strlen(result->EntryDisasm), 1024 - strlen(result->EntryDisasm), "%-35s%-30sIP:%x\n", asmBuf, buf, nextInstr);
                a++;

                pEntryPoint = (DWORD_PTR)nextInstr;
            }
            
            continue;
        }
        entryPointSize = hookBuf->EntrySize;

        // 2. Disassemble entry point and relocated buffer
        if (entryPointSize == 0)
            strcpy_s(result->Error, 1024, "Entry point size is Zero");
        else if (entryPointSize >= 20)
            sprintf_s(result->Error, 1024, "Entry point is too large: %d", entryPointSize);
        else
        {
            pEntryPoint = dwAddressOfFunction;
            memset(asmBuf, 0, MAX_PATH);
            while ((pEntryPoint - (DWORD_PTR)dwAddressOfFunction < entryPointSize) && RTL_SUCCESS(LhDisassembleInstruction((void*)pEntryPoint, &asmLength, buf, MAX_PATH, &nextInstr)))
            {
                opcodes = (PUCHAR)pEntryPoint;
                sprintf_s(asmBuf, 260, "\t");
                for (b = 0; b < (int)(nextInstr - pEntryPoint); b++)
                {
                    sprintf_s(asmBuf + strlen(asmBuf), 260 - strlen(asmBuf), "%02X ", *opcodes);
                    opcodes++;
                }
                sprintf_s(result->EntryDisasm + strlen(result->EntryDisasm), 1024 - strlen(result->EntryDisasm), "%-35s%-30sIP:%x\n", asmBuf, buf, nextInstr);
                pEntryPoint = (DWORD_PTR)nextInstr;
            }
            pEntryPoint = (DWORD_PTR)hookBuf->OldProc;
            result->RelocAddress = (void*)pEntryPoint;
            memset(asmBuf, 0, MAX_PATH);
            while ((pEntryPoint - (DWORD_PTR)hookBuf->OldProc < relocBufSize) && RTL_SUCCESS(LhDisassembleInstruction((void*)pEntryPoint, &asmLength, buf, MAX_PATH, &nextInstr)))
            {
                opcodes = (PUCHAR)pEntryPoint;
                sprintf_s(asmBuf, 260, "\t");
                for (b = 0; b < (int)(nextInstr - pEntryPoint); b++)
                {
                    sprintf_s(asmBuf + strlen(asmBuf), 260 - strlen(asmBuf), "%02X ", *opcodes);
                    opcodes++;
                }
                sprintf_s(result->RelocDisasm + strlen(result->RelocDisasm), 1024 - strlen(result->RelocDisasm), "%-35s%-30sIP:%x\n", asmBuf, buf, nextInstr);
                pEntryPoint = (DWORD_PTR)nextInstr;
            }
        }
        
        if (hookBuf != NULL)
            LhFreeMemory(&hookBuf);
        hookBuf = NULL;
    }

    // Write to file
        
    if (options.Filename != NULL && strlen(options.Filename) > 0)
    {
        fopen_s(&f, options.Filename, "w");
        if (f == NULL)
        {
            printf("Error opening file!\n");
            return STATUS_NOT_FOUND;
        }

        for (i = 0; i < count; i++)
        {
            result = &results[i];

            fprintf(f, "\nFunction: %s\n", result->FnName);
            
            if (result->ModuleRedirect != NULL && strlen(result->ModuleRedirect))
                fprintf(f, "\t(redirected to DLL: %s!%s)\n", result->ModuleRedirect, result->FnRedirect);
            if (result->FnRedirect != NULL && strlen(result->FnRedirect))
                fprintf(f, "\t(redirected to function: %s)\n", result->FnRedirect);

            if (result->Error != NULL && strlen(result->Error) > 0)
            {
                fprintf(f, "ERROR: %s\n", result->Error);

                if (result->EntryDisasm != NULL && strlen(result->EntryDisasm) > 0) {
                    fprintf(f, "%s", result->EntryDisasm);
                }
            }
            else
            {
                fprintf(f, "Entry point:@ %p\n", result->FnAddress);
                fprintf(f, "%s", result->EntryDisasm);
                fprintf(f, "Relocated entry point:@ %p\n", result->RelocAddress);
                fprintf(f, "%s", result->RelocDisasm);
            }
        }

        fclose(f);
    }

    free(AddressOfFunctions);
    free(AddressOfNames);
    free(AddressOfOrdinals);
    
    return 0;
}


EASYHOOK_NT_EXPORT ReleaseTestFuncHookResults(TEST_FUNC_HOOKS_RESULT* results, int count)
{
/*
Description:

    Free the memory allocated for the results from a previous call to TestFuncHooks.

Parameters:

    - results

        Pointer to array of TEST_FUNC_HOOKS_RESULT to be freed.
    
    - count

        The number of elements within results.

Returns:

    STATUS_SUCCESS

*/
    int i = 0;
    TEST_FUNC_HOOKS_RESULT* result;
    for (i = 0; i < count; i++)
    {
        result = &results[i];

        CoTaskMemFree(result->FnName);
        CoTaskMemFree(result->ModuleRedirect);
        CoTaskMemFree(result->FnRedirect);
        CoTaskMemFree(result->EntryDisasm);
        CoTaskMemFree(result->RelocDisasm);
        CoTaskMemFree(result->Error);
    }

    CoTaskMemFree(results);

    return STATUS_SUCCESS;
}