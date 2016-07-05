// EasyHook (File: EasyHookDll\alloc.c)
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

void LhFreeMemory(PLOCAL_HOOK_INFO* RefHandle)
{
/*
Description:

    Will release the memory for a given hook.

Parameters:

    - RefHandle

        A pointer to a valid hook handle. It will be set to NULL
        by this method!
*/

#if defined(_M_X64) && !defined(DRIVER)
    VirtualFree(*RefHandle, 0, MEM_RELEASE);
#else
    RtlFreeMemory(*RefHandle);
#endif

    *RefHandle = NULL;
}

///////////////////////////////////////////////////////////////////////////////////
/////////////////////// LhAllocateMemory
///////////////////////////////////////////////////////////////////////////////////
void* LhAllocateMemory(void* InEntryPoint)
{
/*
Description:

    Allocates one page of hook specific memory.

Parameters:

    - InEntryPoint

        Ignored for 32-Bit versions and drivers. In 64-Bit user mode, the returned
        pointer will always be in a 31-bit boundary around this parameter. This way
        a relative jumper can still be placed instead of having to consume much more entry
        point bytes for an absolute jump!

Returns:

    NULL if no memory could be allocated, a valid pointer otherwise.

*/
	ULONG pageSize;
	return LhAllocateMemoryEx(InEntryPoint, &pageSize);
}

///////////////////////////////////////////////////////////////////////////////////
/////////////////////// LhAllocateMemoryEx
///////////////////////////////////////////////////////////////////////////////////
void* LhAllocateMemoryEx(void* InEntryPoint, ULONG* OutPageSize)
{
/*
Description:

    Allocates one page of hook specific memory. The page size is returned in OutPageSize

Parameters:

    - InEntryPoint

        Ignored for 32-Bit versions and drivers. In 64-Bit user mode, the returned
        pointer will always be in a 31-bit boundary around this parameter. This way
        a relative jumper can still be placed instead of having to consume much more entry
        point bytes for an absolute jump!

    - OutPageSize

        Will be updated to contain the page size.

Returns:

    NULL if no memory could be allocated, a valid pointer otherwise.

*/

    UCHAR*			    Res = NULL;

#if defined(_M_X64) && !defined(DRIVER)
    LONGLONG            Base;
    LONGLONG		    iStart;
    LONGLONG		    iEnd;
    LONGLONG            Index;

#endif
	
#if !defined(DRIVER)
    SYSTEM_INFO		    SysInfo;
    ULONG               PAGE_SIZE;

    GetSystemInfo(&SysInfo);

    PAGE_SIZE = SysInfo.dwPageSize;
    *OutPageSize = PAGE_SIZE;
#endif


    // reserve page with execution privileges
#if defined(_M_X64) && !defined(DRIVER)

    /*
        Reserve memory around entry point...
    */
    iStart = ((LONGLONG)InEntryPoint) - ((LONGLONG)0x7FFFFF00);
    iEnd = ((LONGLONG)InEntryPoint) + ((LONGLONG)0x7FFFFF00);

    if(iStart < (LONGLONG)SysInfo.lpMinimumApplicationAddress)
        iStart = (LONGLONG)SysInfo.lpMinimumApplicationAddress; // shall not be null, because then VirtualAlloc() will not work as expected

    if(iEnd > (LONGLONG)SysInfo.lpMaximumApplicationAddress)
        iEnd = (LONGLONG)SysInfo.lpMaximumApplicationAddress;

    // we are trying to get memory as near as possible to relocate most RIP-relative instructions
    for(Base = (LONGLONG)InEntryPoint, Index = 0; ; Index += PAGE_SIZE)
    {
		BOOLEAN end = TRUE;
		if(Base + Index < iEnd)
		{
			if((Res = (UCHAR*)VirtualAlloc((void*)(Base + Index), PAGE_SIZE, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE)) != NULL)
				break;
			end = FALSE;
		}

        if(Base - Index > iStart)
        {
	        if((Res = (BYTE*)VirtualAlloc((void*)(Base - Index), PAGE_SIZE, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE)) != NULL)
		        break;
			end = FALSE;
        }

		if (end)
			break;
    }

    if(Res == NULL)
	    return NULL;
#else
    
	*OutPageSize = PAGE_SIZE;
	// in 32-bit mode the trampoline will always be reachable
	// In 64-bit driver mode we use an absolute address so the trampoline will always be reachable
    if((Res = (UCHAR*)RtlAllocateMemory(TRUE, PAGE_SIZE)) == NULL)
        return NULL;

#endif

    return Res;
}