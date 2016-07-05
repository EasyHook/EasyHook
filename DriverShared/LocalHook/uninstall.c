// EasyHook (File: EasyHookDll\uninstall.c)
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
#ifdef X64_DRIVER
    KIRQL                  CurrentIRQL = PASSIVE_LEVEL;
#endif

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
#ifdef X64_DRIVER
            CurrentIRQL = KeGetCurrentIrql();
            RtlWPOff();
#endif
            *((ULONGLONG*)Hook->TargetProc) = Hook->TargetBackup;
#ifdef X64_DRIVER
            // we support a trampoline jump of up to 16 bytes in X64_DRIVER
            *((ULONGLONG*)(Hook->TargetProc + 8)) = Hook->TargetBackup_x64;
            RtlWPOn(CurrentIRQL);
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