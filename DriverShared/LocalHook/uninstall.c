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


EASYHOOK_NT_EXPORT LhUninstallHook(TRACED_HOOK_HANDLE InHandle)
{
/*
Description:

    Removes the given hook. To also release associated resources,
    you will have to call LhWaitForPendingRemovals(). In any case
    your hook handler will never be executed again, after calling this
    method.

Parameters:

    - InHandle

        A traced hook handle. If the hook is already removed, this method
        will still return STATUS_SUCCESS.
*/
    LOCAL_HOOK_INFO*        Hook = NULL;
    LOCAL_HOOK_INFO*        List;
    LOCAL_HOOK_INFO*        Prev;
    NTSTATUS                NtStatus;
    BOOLEAN                 IsAllocated = FALSE;

     if(!IsValidPointer(InHandle, sizeof(HOOK_TRACE_INFO)))
        return FALSE;

    RtlAcquireLock(&GlobalHookLock);
    {
        if((InHandle->Link != NULL) && LhIsValidHandle(InHandle, &Hook))
        {
            InHandle->Link = NULL;

            if(Hook->HookProc != NULL)
            {
                Hook->HookProc = NULL;  

                IsAllocated = TRUE;
            }
        }

        if(!IsAllocated)
        {
            RtlReleaseLock(&GlobalHookLock);

            RETURN;
        }

        // remove from global list
        List = GlobalHookListHead.Next;
        Prev = &GlobalHookListHead;

        while(List != NULL)
        {
            if(List == Hook)
            {
                Prev->Next = Hook->Next;

                break;
            }

            List = List->Next;
        }

        // add to removal list
        Hook->Next = GlobalRemovalListHead.Next;
        GlobalRemovalListHead.Next = Hook;
    }
    RtlReleaseLock(&GlobalHookLock);

    RETURN(STATUS_SUCCESS);

//THROW_OUTRO:
FINALLY_OUTRO:
    return NtStatus;
}






EASYHOOK_NT_EXPORT LhUninstallAllHooks()
{
/*
Description:

    Will remove ALL hooks. To also release associated resources,
    you will have to call LhWaitForPendingRemovals().
*/
    LOCAL_HOOK_INFO*        Hook;
    LOCAL_HOOK_INFO*        List;
    NTSTATUS                NtStatus;

    RtlAcquireLock(&GlobalHookLock);
    {
        // remove from global list
        List = GlobalHookListHead.Next;

        while(List != NULL)
        {
            Hook = List;
            List = List->Next;

            // remove tracking
            if(LhIsValidHandle(Hook->Tracking, NULL))
            {
                Hook->Tracking->Link = NULL;
            }

            // add to removal list
            Hook->HookProc = NULL;
            Hook->Next = GlobalRemovalListHead.Next;

            GlobalRemovalListHead.Next = Hook;
        }

		GlobalHookListHead.Next = NULL;
    }
    RtlReleaseLock(&GlobalHookLock);

    RETURN(STATUS_SUCCESS);

FINALLY_OUTRO:
    return NtStatus;
}






EASYHOOK_NT_EXPORT LhWaitForPendingRemovals()
{
/*
Descriptions:

    For stability reasons, all resources associated with a hook
    have to be released if no thread is currently executing the
    handler. Separating this wait loop from the uninstallation
    method is a great performance gain, because you can release
    all hooks first, and then wait for all removals simultaneously.

*/
    PLOCAL_HOOK_INFO        Hook;
    NTSTATUS                NtStatus = STATUS_SUCCESS;
    INT32                  Timeout = 1000;
#pragma warning(disable: 4127)
    while(TRUE)
#pragma warning(default: 4127)
    {
        // pop from removal list
        RtlAcquireLock(&GlobalHookLock);
        {
            Hook = GlobalRemovalListHead.Next;
            
            if(Hook == NULL)
            {
                RtlReleaseLock(&GlobalHookLock);

                break;
            }

            GlobalRemovalListHead.Next = Hook->Next;
        }
        RtlReleaseLock(&GlobalHookLock);

        // restore entry point...
        if(Hook->HookCopy == *((ULONGLONG*)Hook->TargetProc))
        {
            *((ULONGLONG*)Hook->TargetProc) = Hook->TargetBackup;

#ifdef X64_DRIVER
			*((ULONGLONG*)(Hook->TargetProc + 8)) = Hook->TargetBackup_x64;
#endif

#pragma warning(disable: 4127)
            while (TRUE)
#pragma warning(default: 4127)
            {
                if (*Hook->IsExecutedPtr <= 0)
                {
                    // release slot
                    if (GlobalSlotList[Hook->HLSIndex] == Hook->HLSIdent)
                    {
                        GlobalSlotList[Hook->HLSIndex] = 0;
                    }

                    // release memory...
                    LhFreeMemory(&Hook);
                    break;
                }

                if (Timeout <= 0)
                {
                    // this hook was not released within timeout or cannot be released.
                    // We will leak the memory, but not hang forever.
                    NtStatus = STATUS_TIMEOUT;
                    break;
                }

                RtlSleep(25);
                Timeout -= 25;
            }
        }
        else
        {
            // hook was changed... no chance to release resources
        }
    }

    return NtStatus;
}




void LhCriticalFinalize()
{
/*
Description:

    Will be called in the DLL_PROCESS_DETACH event and just uninstalls
    all hooks. If it is possible also their memory is released. 
*/
    LhUninstallAllHooks();

    LhWaitForPendingRemovals();

	RtlDeleteLock(&GlobalHookLock);
}