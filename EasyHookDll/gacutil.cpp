// EasyHook (File: EasyHookDll\gacutil.cpp)
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

#include <fusion.h>
#include <mscoree.h>
#include <atlbase.h>
#include "stdafx.h"

typedef HRESULT (__stdcall *CreateAsmCache)(IAssemblyCache **ppAsmCache, DWORD dwReserved);
typedef HRESULT (__stdcall *LoadLibraryShim_PROC)(LPCWSTR szDllName, LPCWSTR szVersion, LPVOID pvReserved, HMODULE *phModDll);

typedef struct _INTERNAL_CONTEXT_{
	HMODULE					hFusionDll;
	HMODULE					hMsCorEE;
	CreateAsmCache			CreateAssemblyCache;
	LoadLibraryShim_PROC	LoadLibraryShim;
	CComPtr<IAssemblyCache> Cache;
}INTERNAL_CONTEXT, *LPINTERNAL_CONTEXT;

extern "C" __declspec(dllexport) void __stdcall GacReleaseContext(LPINTERNAL_CONTEXT* RefContext){

	if(*RefContext == NULL)
		return;

	LPINTERNAL_CONTEXT	Context = *RefContext;

	if(Context->hFusionDll != NULL)
		FreeLibrary(Context->hFusionDll);

	if(Context->hMsCorEE != NULL)
		FreeLibrary(Context->hMsCorEE);

	memset(Context, 0, sizeof(INTERNAL_CONTEXT));

	RtlFreeMemory(Context);

	*RefContext = NULL;
}

extern "C" int __cdecl main(int argc, char** argw){ return 0; }

extern "C" __declspec(dllexport) LPINTERNAL_CONTEXT __stdcall GacCreateContext(){
	LPINTERNAL_CONTEXT	Result = NULL;

	if((Result = (LPINTERNAL_CONTEXT)RtlAllocateMemory(TRUE, sizeof(INTERNAL_CONTEXT))) == NULL)
		return NULL;

	memset(Result, 0, sizeof(INTERNAL_CONTEXT));

	if((Result->hMsCorEE = LoadLibrary(L"mscoree.dll")) == NULL)
		goto ERROR_ABORT;

	if((Result->LoadLibraryShim = (LoadLibraryShim_PROC)GetProcAddress(Result->hMsCorEE, "LoadLibraryShim")) == NULL)
		goto ERROR_ABORT;

	Result->LoadLibraryShim(L"fusion.dll", 0, 0, &Result->hFusionDll);

	if(Result->hFusionDll == NULL)
		goto ERROR_ABORT;

	if((Result->CreateAssemblyCache = (CreateAsmCache)GetProcAddress(Result->hFusionDll, "CreateAssemblyCache")) == NULL)
		goto ERROR_ABORT;

	if (!SUCCEEDED(Result->CreateAssemblyCache(&Result->Cache, 0)))
		goto ERROR_ABORT;

	return Result;

ERROR_ABORT:
	
	GacReleaseContext(&Result);

	return NULL;
}

extern "C" __declspec(dllexport) BOOL __stdcall GacInstallAssembly(
		LPINTERNAL_CONTEXT InContext,
		WCHAR* InAssemblyPath,
		WCHAR* InDescription,
		WCHAR* InUniqueID){

	FUSION_INSTALL_REFERENCE InstallInfo;

	// setup installation parameters
	memset(&InstallInfo, 0, sizeof(InstallInfo));

	InstallInfo.cbSize = sizeof(InstallInfo);
	InstallInfo.dwFlags = 0;
	InstallInfo.guidScheme = FUSION_REFCOUNT_OPAQUE_STRING_GUID;
	InstallInfo.szIdentifier = InUniqueID;
	InstallInfo.szNonCannonicalData = InDescription;

	// install assembly with given parameters
	if(!SUCCEEDED(InContext->Cache->InstallAssembly(0, InAssemblyPath, &InstallInfo)))
		return FALSE;

	return TRUE;
}

extern "C" __declspec(dllexport) BOOL __stdcall GacUninstallAssembly(
		LPINTERNAL_CONTEXT InContext,
		WCHAR* InAssemblyName,
		WCHAR* InDescription,
		WCHAR* InUniqueID){

	FUSION_INSTALL_REFERENCE InstallInfo;

	// setup uninstallation parameters
	memset(&InstallInfo, 0, sizeof(InstallInfo));

	InstallInfo.cbSize = sizeof(InstallInfo);
	InstallInfo.dwFlags = 0;
	InstallInfo.guidScheme = FUSION_REFCOUNT_OPAQUE_STRING_GUID;
	InstallInfo.szIdentifier = InUniqueID;
	InstallInfo.szNonCannonicalData = InDescription;

	CComPtr<IAssemblyCache> Cache;

	if (!SUCCEEDED(InContext->CreateAssemblyCache(&Cache, 0)))
		return FALSE;

	// uninstall assembly with given parameters
	if(!SUCCEEDED(Cache->UninstallAssembly(0, InAssemblyName, &InstallInfo, NULL)))
		return FALSE;

	return TRUE;
}