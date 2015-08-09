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
            ULONG* InThreadIdList,
            ULONG InThreadCount)
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
        TRUE if all listed thread shall be excluded from interception,
        FALSE otherwise

    - InThreadIdList
        An array of thread IDs. If you specific zero for an entry in this array,
        it will be automatically replaced with the calling thread ID.

    - InThreadCount
        The count of entries listed in the thread ID list. This value must not exceed
        MAX_ACE_COUNT! 
*/

    ULONG           Index;

    ASSERT(IsValidPointer(InAcl, sizeof(HOOK_ACL)),L"acl.c - IsValidPointer(InAcl, sizeof(HOOK_ACL))");

    if(InThreadCount > MAX_ACE_COUNT)
        return STATUS_INVALID_PARAMETER_2;

    if(!IsValidPointer(InThreadIdList, InThreadCount * sizeof(ULONG)))
        return STATUS_INVALID_PARAMETER_1;

    for(Index = 0; Index < InThreadCount; Index++)
    {
        if(InThreadIdList[Index] == 0)
            InThreadIdList[Index] = GetCurrentThreadId();
    }

    // set ACL...
    InAcl->IsExclusive = InIsExclusive;
    InAcl->Count = InThreadCount;

    RtlCopyMemory(InAcl->Entries, InThreadIdList, InThreadCount * sizeof(ULONG));

    return STATUS_SUCCESS;
}

EASYHOOK_NT_EXPORT LhSetInclusiveACL(
            ULONG* InThreadIdList,
            ULONG InThreadCount,
            TRACED_HOOK_HANDLE InHandle)
{
/*
Description:

    Sets an inclusive hook local ACL based on the given thread ID list.
    Only threads in this list will be intercepted by the hook. If the
    global ACL also is inclusive, then all threads stated there are
    intercepted too.

Parameters:
    - InThreadIdList
        An array of thread IDs. If you specific zero for an entry in this array,
        it will be automatically replaced with the calling thread ID.

    - InThreadCount
        The count of entries listed in the thread ID list. This value must not exceed
        MAX_ACE_COUNT! 

    - InHandle
        The hook handle whose local ACL is going to be set.
*/
    PLOCAL_HOOK_INFO        Handle;

    if(!LhIsValidHandle(InHandle, &Handle))
        return STATUS_INVALID_PARAMETER_3;

    return LhSetACL(&Handle->LocalACL, FALSE, InThreadIdList, InThreadCount);
}

EASYHOOK_NT_EXPORT LhSetExclusiveACL(
            ULONG* InThreadIdList,
            ULONG InThreadCount,
            TRACED_HOOK_HANDLE InHandle)
{
/*
Description:

    Sets an inclusive hook local ACL based on the given thread ID list.
    
Parameters:
    - InThreadIdList
        An array of thread IDs. If you specific zero for an entry in this array,
        it will be automatically replaced with the calling thread ID.

    - InThreadCount
        The count of entries listed in the thread ID list. This value must not exceed
        MAX_ACE_COUNT! 

    - InHandle
        The hook handle whose local ACL is going to be set.
*/
    PLOCAL_HOOK_INFO        Handle;

    if(!LhIsValidHandle(InHandle, &Handle))
        return STATUS_INVALID_PARAMETER_3;

    return LhSetACL(&Handle->LocalACL, TRUE, InThreadIdList, InThreadCount);
}

EASYHOOK_NT_EXPORT LhSetGlobalInclusiveACL(
            ULONG* InThreadIdList,
            ULONG InThreadCount)
{
/*
Description:

    Sets an inclusive global ACL based on the given thread ID list.
    
Parameters:
    - InThreadIdList
        An array of thread IDs. If you specific zero for an entry in this array,
        it will be automatically replaced with the calling thread ID.

    - InThreadCount
        The count of entries listed in the thread ID list. This value must not exceed
        MAX_ACE_COUNT! 
*/
    return LhSetACL(LhBarrierGetAcl(), FALSE, InThreadIdList, InThreadCount);
}

EASYHOOK_NT_EXPORT LhSetGlobalExclusiveACL(
            ULONG* InThreadIdList,
            ULONG InThreadCount)
{
/*
Description:

    Sets an exclusive global ACL based on the given thread ID list.
    
Parameters:
    - InThreadIdList
        An array of thread IDs. If you specific zero for an entry in this array,
        it will be automatically replaced with the calling thread ID.

    - InThreadCount
        The count of entries listed in the thread ID list. This value must not exceed
        MAX_ACE_COUNT! 
*/
    return LhSetACL(LhBarrierGetAcl(), TRUE, InThreadIdList, InThreadCount);
}