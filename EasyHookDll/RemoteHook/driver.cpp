// EasyHook (File: EasyHookDll\driver.cpp)
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


EASYHOOK_NT_EXPORT RhInstallDriver(
			WCHAR* InDriverPath,
			WCHAR* InDriverName)
{
/*
Description:

	Installs the given driver.

Parameters:

	- InDriverPath

		A relative or full path to the driver's executable

	- InDriverName

		A name to register the driver in the service control manager.

*/   
	WCHAR				DriverPath[MAX_PATH + 1];
	SC_HANDLE			hSCManager = NULL;
	SC_HANDLE			hService = NULL;	
	NTSTATUS			NtStatus;

	GetFullPathNameW(InDriverPath, MAX_PATH, DriverPath, NULL);

	if(!RtlFileExists(DriverPath))
		THROW(STATUS_NOT_FOUND, L"The EasyHook driver file does not exist.");

	if((hSCManager = OpenSCManagerW(
			NULL, 
			NULL, 
			SC_MANAGER_ALL_ACCESS)) == NULL)
		THROW(STATUS_ACCESS_DENIED, L"Unable to open service control manager. Are you running as administrator?");

	// does service exist?
	if((hService = OpenService(
			hSCManager, 
			InDriverName, 
			SERVICE_ALL_ACCESS)) == NULL)
	{
		if(GetLastError() != ERROR_SERVICE_DOES_NOT_EXIST)
			THROW(STATUS_INTERNAL_ERROR, L"An unknown error has occurred during driver installation.");

		// Create the service
		if((hService = CreateServiceW(
				hSCManager,              
				InDriverName,            
				InDriverName,           
				SERVICE_ALL_ACCESS,        
				SERVICE_KERNEL_DRIVER,
				SERVICE_DEMAND_START,    
				SERVICE_ERROR_NORMAL,     
				DriverPath,            
				NULL, NULL, NULL, NULL, NULL)) == NULL)
			THROW(STATUS_INTERNAL_ERROR, L"Unable to install driver.");
	}

	// start and connect service...
	if(!StartServiceW(hService, 0, NULL) && (GetLastError() != ERROR_SERVICE_ALREADY_RUNNING)
			&& (GetLastError() != ERROR_SERVICE_DISABLED))
		THROW(STATUS_INTERNAL_ERROR, L"Unable to start driver!");

	RETURN;
	
THROW_OUTRO:
FINALLY_OUTRO:
	{
		if(hService != NULL)
		{
			DeleteService(hService);

			CloseServiceHandle(hService);
		}

		if(hSCManager != NULL)
			CloseServiceHandle(hSCManager);

		return NtStatus;
	}
}

EASYHOOK_NT_EXPORT RhInstallSupportDriver()
{
/*
Description:

    Installs the EasyHook support driver. 
	This will allow your driver to successfully obtain the EasyHook driver
	API using EasyHookQueryInterface().

*/   
	WCHAR*				DriverName = L"EasyHook32Drv.sys";

	if(RhIsX64System())
		DriverName = L"EasyHook64Drv.sys";
	
	return RhInstallDriver(DriverName, DriverName);
}
