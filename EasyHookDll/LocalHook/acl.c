// EasyHook (File: EasyHookDll\acl.c)
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

    Sets an exclusive hook local ACL based on the given thread ID list.
    
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