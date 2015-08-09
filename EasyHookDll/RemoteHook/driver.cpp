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
