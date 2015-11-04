// EasyHook (File: EasyHookDll\dllmain.c)
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

