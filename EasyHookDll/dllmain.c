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

HMODULE             hNtDll = NULL;
HMODULE             hKernel32 = NULL;
HMODULE             hCurrentModule = NULL;
DWORD               RhTlsIndex;
HANDLE              hEasyHookHeap = NULL;

BOOL APIENTRY DllMain( HMODULE hModule,
                       DWORD  ul_reason_for_call,
                       LPVOID lpReserved
					 )
{
#ifdef _DEBUG
    int CurrentFlags;
#endif

	switch (ul_reason_for_call)
	{
	case DLL_PROCESS_ATTACH:
        {
            hCurrentModule = hModule;

#ifdef _DEBUG
            CurrentFlags = _CrtSetDbgFlag(_CRTDBG_REPORT_FLAG);
            CurrentFlags |= _CRTDBG_DELAY_FREE_MEM_DF;
            CurrentFlags |= _CRTDBG_LEAK_CHECK_DF;
            CurrentFlags |= _CRTDBG_CHECK_ALWAYS_DF;
            _CrtSetDbgFlag(CurrentFlags);
#endif

	        if(((hNtDll = LoadLibraryA("ntdll.dll")) == NULL) ||
	                ((hKernel32 = LoadLibraryA("kernel32.dll")) == NULL))
                return FALSE;

            hEasyHookHeap = HeapCreate(0, 0, 0);

            DbgCriticalInitialize();

            LhBarrierProcessAttach();

            LhCriticalInitialize();

            // allocate tls slot
            if((RhTlsIndex = TlsAlloc()) == TLS_OUT_OF_INDEXES)
                return FALSE;
        }break;
	case DLL_THREAD_ATTACH:
        {
        }break;
	case DLL_THREAD_DETACH:
        {
            LhBarrierThreadDetach();
        }break;
	case DLL_PROCESS_DETACH:
		{
            // free tls slot
            TlsFree(RhTlsIndex);

            if (lpReserved != NULL) // if lpReserved != NULL then LhWaitForPendingRemovals COULD cause an endless loop
                break;

            // remove all hooks and shutdown thread barrier...
			LhCriticalFinalize();

			LhModuleInfoFinalize();

            LhBarrierProcessDetach();

            DbgCriticalFinalize();

            HeapDestroy(hEasyHookHeap);

            FreeLibrary(hNtDll);
            FreeLibrary(hKernel32);
        }break;
	}
	return TRUE;
}

