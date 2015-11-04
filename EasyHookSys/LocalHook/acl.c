// EasyHook (File: EasyHookSys\acl.c)
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

    ASSERT(IsValidPointer(InAcl, sizeof(HOOK_ACL)),L"acl.c - IsValidPointer(InAcl, sizeof(HOOK_ACL))");

    if(InProcessCount > MAX_ACE_COUNT)
        return STATUS_INVALID_PARAMETER_2;

    if(!IsValidPointer(InProcessIdList, InProcessCount * sizeof(ULONG)))
        return STATUS_INVALID_PARAMETER_1;

    for(Index = 0; Index < InProcessCount; Index++)
    {
        if(InProcessIdList[Index] == 0)
            InProcessIdList[Index] = (ULONG)PsGetCurrentProcessId();
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