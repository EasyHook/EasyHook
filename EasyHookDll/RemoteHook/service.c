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

EASYHOOK_NT_EXPORT RtlInstallService(
            WCHAR* InServiceName,
            WCHAR* InExePath,
            WCHAR* InChannelName)
{
/*
Description:

    This method is intended for the managed layer only. It will
    provide a convenient way to install a service which seems to
    be impossible with NET code in any efficient manner.
    
Parameters:

    - InServiceName

        A unique service name under which the service shall be registered.
        In case of the EasyHook service, this value is expected to be exactly
        the filename without extension of the full "InExePath".

    - InExePath

        A relative or absolute path to the service EXE file.

    - InChannelName

        The channel name for the service to register its IPC channel.
        This should be randomly generated.

Returns:

    STATUS_ALREADY_REGISTERED

        A service with this name is already registered. To prevent name collisions
        I recommend to rename the service to an unique name for your specific
        application. Please refer to the README file for more information.

    STATUS_ACCESS_DENIED

        You are not administrator?!
*/
	SC_HANDLE			hSCManager = NULL;
	SC_HANDLE			hService = NULL;
    NTSTATUS            NtStatus;
    LPCWSTR		        StartParams[1] = {InChannelName};
    ULONG               res;

	if((hSCManager = OpenSCManagerW(NULL, NULL, SC_MANAGER_ALL_ACCESS)) == NULL)
		THROW(STATUS_ACCESS_DENIED, L"Unable to open service control manager. Check for administrator privileges!");

	/* 
        Does service exist?
        Internally the service will always be removed automatically. 
        So there shouldn't be any problems. Only if two or more concurrent
        applications are using EasyHook, this will lead to an error, because
        it is very hard to don't get them confused.
    */
	if((hService = OpenService(hSCManager, InServiceName, SERVICE_ALL_ACCESS)) == NULL)
	{
		if(GetLastError() != ERROR_SERVICE_DOES_NOT_EXIST)
			THROW(STATUS_INTERNAL_ERROR, L"Unable to open already registered service.");
	}
	else
	{
        DeleteService(hService);

        CloseServiceHandle(hService);

        hService = NULL;

        THROW(STATUS_ALREADY_REGISTERED, L"The service is already registered. Use the service control manager to remove it!");
	}

	// install service
	if((hService = CreateServiceW(
			hSCManager,              
			InServiceName,            
			InServiceName,           
			SERVICE_ALL_ACCESS,        
			SERVICE_WIN32_OWN_PROCESS,
			SERVICE_DEMAND_START,    
			SERVICE_ERROR_NORMAL,     
			InExePath,            
			NULL, NULL, NULL, NULL, NULL)) == NULL)
		THROW(STATUS_INTERNAL_ERROR, L"Unable to install service as system process.");

    // start service
	if(!StartServiceW(hService, 1, (LPCWSTR*)StartParams))
    {
        res = GetLastError();

        THROW(STATUS_INTERNAL_ERROR, L"Unable to start service.");
    }

    RETURN(STATUS_SUCCESS);

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