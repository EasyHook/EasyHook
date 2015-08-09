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

    // we are trying to get memory as near as possible to relocate most RIP-relative addressings
    for(Base = (LONGLONG)InEntryPoint, Index = 0; ; Index += PAGE_SIZE)
    {
		if(Base + Index < iEnd)
		{
			if((Res = (UCHAR*)VirtualAlloc((void*)(Base + Index), PAGE_SIZE, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE)) != NULL)
				break;
		}

        if(Base - Index > iStart)
        {
	        if((Res = (BYTE*)VirtualAlloc((void*)(Base - Index), PAGE_SIZE, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE)) != NULL)
		        break;
        }
    }

    if(Res == NULL)
	    return NULL;
#else
    // in 32-bit mode the trampoline will always be reachable
    if((Res = (UCHAR*)RtlAllocateMemory(TRUE, PAGE_SIZE)) == NULL)
        return NULL;

#endif

    return Res;
}