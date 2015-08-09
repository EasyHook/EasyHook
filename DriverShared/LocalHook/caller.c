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

#ifndef DRIVER

	#include <psapi.h>

	typedef NTSYSAPI WORD NTAPI PROC_RtlCaptureStackBackTrace(
		__in DWORD FramesToSkip,
		__in DWORD FramesToCapture,
		__out_ecount(FramesToCapture) PVOID *BackTrace,
		__out_opt PDWORD BackTraceHash
	   );

#else

	typedef struct _SYSTEM_MODULE_
	{
		ULONG_PTR		Reserved1; 
		ULONG_PTR		Reserved2; 
		PVOID			ImageBaseAddress; 
		ULONG			ImageSize; 
		ULONG			Flags; 
		SHORT			Id; 
		SHORT			Rank; 
		SHORT			w018; 
		SHORT			NameOffset; 
		CHAR			Name[256];
	}SYSTEM_MODULE, *PSYSTEM_MODULE;


	typedef struct _SYSTEM_MODULE_INFORMATION_
	{
		ULONG			Count;
		SYSTEM_MODULE	Modules[0];
	}SYSTEM_MODULE_INFORMATION, *PSYSTEM_MODULE_INFORMATION;
#endif


#undef CaptureStackBackTrace

static MODULE_INFORMATION*					LhModuleArray = NULL;
static ULONG								LhModuleCount = 0;

#ifndef DRIVER
	static PROC_RtlCaptureStackBackTrace*	RtlCaptureStackBackTrace = NULL;
	static MODULEINFO*						LhNativeModuleArray = NULL;
	static CHAR*							LhNativePathArray = NULL;
	static HMODULE							ProcessModules[1024];
#else
	static SYSTEM_MODULE_INFORMATION*		LhNativeModuleArray = NULL;
	BOOLEAN									LhModuleListChanged = TRUE;
#endif


#ifdef DRIVER

void LhModuleInfoFinalize()
{
	if(LhNativeModuleArray != NULL)
		RtlFreeMemory(LhNativeModuleArray);

	if(LhModuleArray != NULL)
		RtlFreeMemory(LhModuleArray);
}

EASYHOOK_NT_INTERNAL LhUpdateModuleInformation()
{
	NTSTATUS						NtStatus;
	PSYSTEM_MODULE_INFORMATION		NativeList = NULL;
	ULONG							RequiredSize = 0;
	ULONG							i;
	PSYSTEM_MODULE					Mod;
	MODULE_INFORMATION*				List = NULL;

	if(!LhModuleListChanged)
		return STATUS_SUCCESS;

	LhModuleListChanged = FALSE;

	if(ZwQuerySystemInformation(11, NULL, 0, &RequiredSize) != STATUS_INFO_LENGTH_MISMATCH)
		THROW(STATUS_INTERNAL_ERROR, L"Unable to enumerate system modules.");

	if((NativeList = RtlAllocateMemory(TRUE, RequiredSize)) == NULL)
		THROW(STATUS_NO_MEMORY, L"Unable to allocate memory.");

	if(!RTL_SUCCESS(ZwQuerySystemInformation(11, NativeList, RequiredSize, &RequiredSize)))
		THROW(STATUS_INTERNAL_ERROR, L"Unable to enumerate system modules.");

	if((List = RtlAllocateMemory(FALSE, sizeof(MODULE_INFORMATION) * NativeList->Count)) == NULL)
		THROW(STATUS_NO_MEMORY, L"Unable to allocate memory.");

	for(i = 0; i < NativeList->Count; i++)
	{
		Mod = &NativeList->Modules[i];

		List[i].BaseAddress = Mod->ImageBaseAddress;
		List[i].ImageSize = Mod->ImageSize;
		List[i].ModuleName = Mod->Name + Mod->NameOffset;
		
		memcpy(List[i].Path, Mod->Name, 256);

		if(i + 1 < NativeList->Count)
			List[i].Next = &List[i + 1];
		else
			List[i].Next = NULL;
	}

	RtlAcquireLock(&GlobalHookLock);
	{
		if(LhNativeModuleArray != NULL)
			RtlFreeMemory(LhNativeModuleArray);

		if(LhModuleArray != NULL)
			RtlFreeMemory(LhModuleArray);

		LhNativeModuleArray = NativeList;
		LhModuleArray = List;
		LhModuleCount = NativeList->Count;
	}
	RtlReleaseLock(&GlobalHookLock);

    RETURN;

THROW_OUTRO:
	{
		if(NativeList != NULL)
			RtlFreeMemory(NativeList);

		if(List != NULL)
			RtlFreeMemory(List);
	}
FINALLY_OUTRO:
    return NtStatus;
}

#else

void LhModuleInfoFinalize()
{
	if(LhNativeModuleArray != NULL)
		RtlFreeMemory(LhNativeModuleArray);

	if(LhModuleArray != NULL)
		RtlFreeMemory(LhModuleArray);

	if(LhNativePathArray != NULL)
		RtlFreeMemory(LhNativePathArray);
}

EASYHOOK_NT_INTERNAL LhUpdateModuleInformation()
{
	
/*
Description:

    Is supposed to be called interlocked... "ProcessModules" is 
    outsourced to prevent "__chkstk".
    Will just enumerate current process modules and extract
    required information for each of them.
*/
    LONG					NtStatus = STATUS_UNHANDLED_EXCEPTION;
    ULONG					Index;
    ULONG					ModIndex;
    MODULEINFO*				NativeList = NULL;
    ULONG					ModuleCount;
	MODULEINFO*				Mod = NULL;
	MODULE_INFORMATION*		List = NULL;
	CHAR*					PathList = NULL;
	CHAR*					ModPath = NULL;
	ULONG					ModPathSize = 0;
	LONG					iChar = 0;

    // enumerate modules...
	RtlAcquireLock(&GlobalHookLock);
	{
		if(!EnumProcessModules(
				GetCurrentProcess(),
				ProcessModules,
				sizeof(ProcessModules),
				&ModuleCount))
		{
			RtlReleaseLock(&GlobalHookLock);

			THROW(STATUS_INTERNAL_ERROR, L"Unable to enumerate current process modules.");
		}
	}
	RtlReleaseLock(&GlobalHookLock);

    ModuleCount /= sizeof(HMODULE);

    // retrieve module information
    if((NativeList = (MODULEINFO*)RtlAllocateMemory(FALSE, ModuleCount * sizeof(NativeList[0]))) == NULL)
        THROW(STATUS_NO_MEMORY, L"Unable to allocate memory for module information.");

	if((List = (MODULE_INFORMATION*)RtlAllocateMemory(TRUE, sizeof(MODULE_INFORMATION) * ModuleCount)) == NULL)
		THROW(STATUS_NO_MEMORY, L"Unable to allocate memory.");

	if((PathList = (CHAR*)RtlAllocateMemory(TRUE, MAX_PATH * ModuleCount)) == NULL)
		THROW(STATUS_NO_MEMORY, L"Unable to allocate memory.");

    for(Index = 0, ModIndex = 0; Index < ModuleCount; Index++)
    {
		// collect information
        if(!GetModuleInformation(
                GetCurrentProcess(),
                ProcessModules[Index],
                &NativeList[ModIndex],
                sizeof(NativeList[ModIndex])))
            continue;

		GetModuleFileNameA(
				ProcessModules[Index],
				&PathList[ModIndex * MAX_PATH],
				MAX_PATH);

		if(GetLastError() != ERROR_SUCCESS)
			continue;

        Mod = &NativeList[ModIndex];

		// normalize module information
		List[ModIndex].BaseAddress = (UCHAR*)Mod->lpBaseOfDll;
		List[ModIndex].ImageSize = Mod->SizeOfImage;

		memcpy(List[ModIndex].Path, &PathList[ModIndex * MAX_PATH], MAX_PATH + 1);

		ModPath = List[ModIndex].Path;
		ModPathSize = RtlAnsiLength(ModPath);

		for(iChar = ModPathSize; iChar >= 0; iChar--)
		{
			if(ModPath[iChar] == '\\')
			{
				List[ModIndex].ModuleName = &ModPath[iChar + 1];

				break;
			}
		}

		if(ModIndex + 1 < ModuleCount)
			List[ModIndex].Next = &List[ModIndex + 1];
		else
			List[ModIndex].Next = NULL;

        ModIndex++;
    }

    // save changes...
	RtlAcquireLock(&GlobalHookLock);
	{
		if(LhNativeModuleArray != NULL)
			RtlFreeMemory(LhNativeModuleArray);

		if(LhModuleArray != NULL)
			RtlFreeMemory(LhModuleArray);

		if(LhNativePathArray != NULL)
			RtlFreeMemory(LhNativePathArray);

		LhNativePathArray = PathList;
		LhNativeModuleArray = NativeList;
		LhModuleArray = List;
		LhModuleCount = ModIndex;
	}
	RtlReleaseLock(&GlobalHookLock);

    RETURN;

THROW_OUTRO:
	{
		if(NativeList != NULL)
			RtlFreeMemory(NativeList);

		if(List != NULL)
			RtlFreeMemory(List);
	}
FINALLY_OUTRO:
    return NtStatus;
}

#endif


EASYHOOK_NT_EXPORT LhEnumModules(
			HMODULE* OutModuleArray, 
            ULONG InMaxModuleCount,
            ULONG* OutModuleCount)
{
/*
Description:

	For performance reasons, only the module base addresses are returned.
	You may then loop through the array and use LhBarrierPointerToModule()
	to query each module information.

Parameters:
	
	- OutModuleArray

		An array receiveing module pointers. Set to NULL to only query "OutModuleCount".

	- InMaxModuleCount

		The maximum count of modules that the given buffer can hold.

	- OutModuleCount

		The actual count of modules loaded into the current process or into the kernel,
		depending on the caller's context. This pointer must be specified if no
		module buffer is passed; if one is passed, this parameter is optional.
*/
	ULONG					ModIndex = 0;
	NTSTATUS				NtStatus;
	MODULE_INFORMATION*		List;

	if(IsValidPointer(OutModuleArray, InMaxModuleCount * sizeof(PVOID)))
	{
		// loop through the module list...
		RtlAcquireLock(&GlobalHookLock);
		{
			if(IsValidPointer(OutModuleCount, sizeof(ULONG)))
				*OutModuleCount = LhModuleCount;

			List = LhModuleArray;

			// walk through process modules
			while(List != NULL)
			{
				if(ModIndex > InMaxModuleCount)
				{
					RtlReleaseLock(&GlobalHookLock);

					THROW(STATUS_BUFFER_TOO_SMALL, L"The given buffer was filled but could not hold all modules.");
				}

				OutModuleArray[ModIndex++] = (HMODULE)List->BaseAddress;

				List = List->Next;
			}	
		}	
		RtlReleaseLock(&GlobalHookLock);
	}
	else
	{
		// return module count...
		if(!IsValidPointer(OutModuleCount, sizeof(ULONG)))
			THROW(STATUS_INVALID_PARAMETER_3, L"If no buffer is specified you need to pass a module count storage.");

		*OutModuleCount = LhModuleCount;
	}

	RETURN;

THROW_OUTRO:
FINALLY_OUTRO:
    return NtStatus;
}



EASYHOOK_NT_EXPORT LhBarrierPointerToModule(
              PVOID InPointer,
              MODULE_INFORMATION* OutModule)
{
/*
Description:

    Translates the given pointer (likely a method) to its
    owning module if possible.

Parameters:

    - InPointer

        A method pointer to be translated.

    - OutModule

        Receives the owner of a given method.
        
Returns:

    STATUS_NOT_FOUND
            
        No matching module could be found.
*/
    UCHAR*					Pointer = (UCHAR*)InPointer;
    NTSTATUS				NtStatus;
    BOOL					CanTryAgain = TRUE;
	MODULE_INFORMATION*		List;

	if(!IsValidPointer(OutModule, sizeof(MODULE_INFORMATION)))
		THROW(STATUS_INVALID_PARAMETER_2, L"The given module storage is invalid.");

LABEL_TRY_AGAIN:

	RtlAcquireLock(&GlobalHookLock);
	{
		List = LhModuleArray;

		// walk through process modules
		while(List != NULL)
		{
			if((Pointer >= List->BaseAddress) && (Pointer <= List->BaseAddress + List->ImageSize))
			{
				*OutModule = *List;

				RtlReleaseLock(&GlobalHookLock);

				RETURN;
			}

			List = List->Next;
		}
	}
	RtlReleaseLock(&GlobalHookLock);

    if((InPointer == NULL) || (InPointer == (PVOID)~0))
    {
        // this pointer does not belong to any module...
    }
    else
    {
        // unable to find calling module...
        FORCE(LhUpdateModuleInformation());

        if(CanTryAgain)
        {
            CanTryAgain = FALSE;

            goto LABEL_TRY_AGAIN;
        }
    }

    THROW(STATUS_NOT_FOUND, L"Unable to determine module.");

THROW_OUTRO:
FINALLY_OUTRO:
    return NtStatus;
}






EASYHOOK_NT_EXPORT LhBarrierCallStackTrace(
            PVOID* OutMethodArray, 
            ULONG InMaxMethodCount,
            ULONG* OutMethodCount)
{
/*
Description:

    Creates a call stack trace and translates all method entries
    back into their owning modules.

Parameters:

    - OutMethodArray

        An array receiving the methods on the call stack.

    - InMaxMethodCount

        The length of the method array. 

    - OutMethodCount

        The actual count of methods on the call stack. This will never
        be greater than 64.

Returns:

    STATUS_NOT_IMPLEMENTED

        Only supported since Windows XP.
*/
    NTSTATUS				NtStatus;
    PVOID					Backup = NULL;

	if(InMaxMethodCount > 64)
		THROW(STATUS_INVALID_PARAMETER_2, L"At maximum 64 modules are supported.");

	if(!IsValidPointer(OutMethodArray, InMaxMethodCount * sizeof(PVOID)))
		THROW(STATUS_INVALID_PARAMETER_1, L"The given module buffer is invalid.");

	if(!IsValidPointer(OutMethodCount, sizeof(ULONG)))
		THROW(STATUS_INVALID_PARAMETER_3, L"Invalid module count storage.");

    FORCE(LhBarrierBeginStackTrace(&Backup));

#ifndef DRIVER
    if(RtlCaptureStackBackTrace == NULL)
        RtlCaptureStackBackTrace = (PROC_RtlCaptureStackBackTrace*)GetProcAddress(hKernel32, "RtlCaptureStackBackTrace");

    if(RtlCaptureStackBackTrace == NULL)
        THROW(STATUS_NOT_IMPLEMENTED, L"This method requires Windows XP or later.");
#endif

    *OutMethodCount = RtlCaptureStackBackTrace(1, 32, OutMethodArray, NULL);

    RETURN;

THROW_OUTRO:
FINALLY_OUTRO:
     {
        if(Backup != NULL)
            LhBarrierEndStackTrace(Backup);

        return NtStatus;
    }
}

EASYHOOK_NT_EXPORT LhBarrierGetCallingModule(MODULE_INFORMATION* OutModule)
{
/*
Description:

    The given storage will receive the calling unmanaged module.

Returns:

    STATUS_NOT_FOUND
            
        No matching module could be found.

*/
    NTSTATUS        NtStatus;
    UCHAR*          ReturnAddress;

    FORCE(LhBarrierGetReturnAddress((PVOID*)&ReturnAddress));

    FORCE(LhBarrierPointerToModule(ReturnAddress, OutModule));

    RETURN;

THROW_OUTRO:
FINALLY_OUTRO:
	return NtStatus;
}