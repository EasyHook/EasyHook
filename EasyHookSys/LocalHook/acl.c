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

LONG LhSetACL(
            HOOK_ACL* InAcl,
            BOOL InIsExclusive,
            ULONG* InProcessIdList,
            ULONG InProcessCount)
{
/*
Description:

    This method is used internally to provide a generic interface to
    either the global or local hook ACLs.
    
Parameters:
    - InAcl
        NULL if you want to set the global ACL.
        Any LOCAL_HOOK_INFO::LocalACL to set the hook specific ACL.

    - InIsExclusive
        TRUE if all listed process shall be excluded from interception,
        FALSE otherwise

    - InProcessIdList
        An array of process IDs. If you specific zero for an entry in this array,
        it will be automatically replaced with the calling process ID.

    - InProcessCount
        The count of entries listed in the process ID list. This value must not exceed
        MAX_ACE_COUNT! 
*/

    ULONG           Index;

    ASSERT(IsValidPointer(InAcl, sizeof(HOOK_ACL)));

    if(InProcessCount > MAX_ACE_COUNT)
        return STATUS_INVALID_PARAMETER_2;

    if(!IsValidPointer(InProcessIdList, InProcessCount * sizeof(ULONG)))
        return STATUS_INVALID_PARAMETER_1;

    for(Index = 0; Index < InProcessCount; Index++)
    {
        if(InProcessIdList[Index] == 0)
            InProcessIdList[Index] = PsGetCurrentProcessId();
    }

    // set ACL...
    InAcl->IsExclusive = InIsExclusive;
    InAcl->Count = InProcessCount;

    RtlCopyMemory(InAcl->Entries, InProcessIdList, InProcessCount * sizeof(ULONG));

    return STATUS_SUCCESS;
}

EASYHOOK_NT_EXPORT LhSetInclusiveACL(
            ULONG* InProcessIdList,
            ULONG InProcessCount,
            TRACED_HOOK_HANDLE InHandle)
{
/*
Description:

    Sets an inclusive hook local ACL based on the given process ID list.
    Only processs in this list will be intercepted by the hook. If the
    global ACL also is inclusive, then all processs stated there are
    intercepted too.

Parameters:
    - InProcessIdList
        An array of process IDs. If you specific zero for an entry in this array,
        it will be automatically replaced with the calling process ID.

    - InProcessCount
        The count of entries listed in the process ID list. This value must not exceed
        MAX_ACE_COUNT! 

    - InHandle
        The hook handle whose local ACL is going to be set.
*/
    PLOCAL_HOOK_INFO        Handle;

    if(!LhIsValidHandle(InHandle, &Handle))
        return STATUS_INVALID_PARAMETER_3;

    return LhSetACL(&Handle->LocalACL, FALSE, InProcessIdList, InProcessCount);
}

EASYHOOK_NT_EXPORT LhSetExclusiveACL(
            ULONG* InProcessIdList,
            ULONG InProcessCount,
            TRACED_HOOK_HANDLE InHandle)
{
/*
Description:

    Sets an inclusive hook local ACL based on the given process ID list.
    
Parameters:
    - InProcessIdList
        An array of process IDs. If you specific zero for an entry in this array,
        it will be automatically replaced with the calling process ID.

    - InProcessCount
        The count of entries listed in the process ID list. This value must not exceed
        MAX_ACE_COUNT! 

    - InHandle
        The hook handle whose local ACL is going to be set.
*/
    PLOCAL_HOOK_INFO        Handle;

    if(!LhIsValidHandle(InHandle, &Handle))
        return STATUS_INVALID_PARAMETER_3;

    return LhSetACL(&Handle->LocalACL, TRUE, InProcessIdList, InProcessCount);
}

EASYHOOK_NT_EXPORT LhSetGlobalInclusiveACL(
            ULONG* InProcessIdList,
            ULONG InProcessCount)
{
/*
Description:

    Sets an inclusive global ACL based on the given process ID list.
    
Parameters:
    - InProcessIdList
        An array of process IDs. If you specific zero for an entry in this array,
        it will be automatically replaced with the calling process ID.

    - InProcessCount
        The count of entries listed in the process ID list. This value must not exceed
        MAX_ACE_COUNT! 
*/
    return LhSetACL(LhBarrierGetAcl(), FALSE, InProcessIdList, InProcessCount);
}

EASYHOOK_NT_EXPORT LhSetGlobalExclusiveACL(
            ULONG* InProcessIdList,
            ULONG InProcessCount)
{
/*
Description:

    Sets an exclusive global ACL based on the given process ID list.
    
Parameters:
    - InProcessIdList
        An array of process IDs. If you specific zero for an entry in this array,
        it will be automatically replaced with the calling process ID.

    - InProcessCount
        The count of entries listed in the process ID list. This value must not exceed
        MAX_ACE_COUNT! 
*/
    return LhSetACL(LhBarrierGetAcl(), TRUE, InProcessIdList, InProcessCount);
}