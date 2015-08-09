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


typedef struct _DRIVER_NOTIFICATION_
{
	SLIST_ENTRY		ListEntry;
	ULONG			ProcessId;
}DRIVER_NOTIFICATION, *PDRIVER_NOTIFICATION;

NTSTATUS DriverEntry(
	IN PDRIVER_OBJECT InDriverObject,
	IN PUNICODE_STRING InRegistryPath);

NTSTATUS EasyHookDispatchCreate(
	IN PDEVICE_OBJECT InDeviceObject,
	IN PIRP	InIrp);

NTSTATUS EasyHookDispatchClose(
	IN PDEVICE_OBJECT InDeviceObject,
	IN PIRP InIrp);

NTSTATUS EasyHookDispatchDeviceControl(
	IN PDEVICE_OBJECT InDeviceObject,
	IN PIRP InIrp);

VOID EasyHookUnload(IN PDRIVER_OBJECT DriverObject);

#ifdef ALLOC_PRAGMA

#pragma alloc_text(INIT, DriverEntry)
#pragma alloc_text(PAGE, EasyHookDispatchCreate)
#pragma alloc_text(PAGE, EasyHookDispatchClose)
#pragma alloc_text(PAGE, EasyHookDispatchDeviceControl)
#pragma alloc_text(PAGE, EasyHookUnload)

#endif

void OnImageLoadNotification(
    IN PUNICODE_STRING  FullImageName,
    IN HANDLE  ProcessId, // where image is mapped
    IN PIMAGE_INFO  ImageInfo)
{
	LhModuleListChanged = TRUE;
}

/**************************************************************

Description:

	Initializes the driver and also loads the system specific PatchGuard
	information.
*/
NTSTATUS DriverEntry(
	IN PDRIVER_OBJECT		InDriverObject,
	IN PUNICODE_STRING		InRegistryPath)
{
	NTSTATUS						Status;    
    UNICODE_STRING					NtDeviceName;
	UNICODE_STRING					DosDeviceName;
    PEASYHOOK_DEVICE_EXTENSION		DeviceExtension;
	PDEVICE_OBJECT					DeviceObject = NULL;
	BOOLEAN							SymbolicLink = FALSE;

	/*
		Create device...
	*/
    RtlInitUnicodeString(&NtDeviceName, EASYHOOK_DEVICE_NAME);

    Status = IoCreateDevice(
		InDriverObject,
		sizeof(EASYHOOK_DEVICE_EXTENSION),		// DeviceExtensionSize
		&NtDeviceName,					// DeviceName
		FILE_DEVICE_EASYHOOK,			// DeviceType
		0,								// DeviceCharacteristics
		TRUE,							// Exclusive
		&DeviceObject					// [OUT]
		);

	if (!NT_SUCCESS(Status))
		goto ERROR_ABORT;

	/*
		Expose interfaces...
	*/
	DeviceExtension = (PEASYHOOK_DEVICE_EXTENSION)DeviceObject->DeviceExtension;
	DeviceExtension->MaxVersion = EASYHOOK_INTERFACE_v_1;

	DeviceExtension->API_v_1.RtlGetLastError = RtlGetLastError;
	DeviceExtension->API_v_1.RtlGetLastErrorString = RtlGetLastErrorString;
	DeviceExtension->API_v_1.LhInstallHook = LhInstallHook;
	DeviceExtension->API_v_1.LhUninstallHook = LhUninstallHook;
	DeviceExtension->API_v_1.LhWaitForPendingRemovals = LhWaitForPendingRemovals;
	DeviceExtension->API_v_1.LhBarrierGetCallback = LhBarrierGetCallback;
	DeviceExtension->API_v_1.LhBarrierGetReturnAddress = LhBarrierGetReturnAddress;
	DeviceExtension->API_v_1.LhBarrierGetAddressOfReturnAddress = LhBarrierGetAddressOfReturnAddress;
	DeviceExtension->API_v_1.LhBarrierBeginStackTrace = LhBarrierBeginStackTrace;
	DeviceExtension->API_v_1.LhBarrierEndStackTrace = LhBarrierEndStackTrace;
	DeviceExtension->API_v_1.LhBarrierPointerToModule = LhBarrierPointerToModule;
	DeviceExtension->API_v_1.LhBarrierGetCallingModule = LhBarrierGetCallingModule;
	DeviceExtension->API_v_1.LhBarrierCallStackTrace = LhBarrierCallStackTrace;
	DeviceExtension->API_v_1.LhSetGlobalExclusiveACL = LhSetGlobalExclusiveACL;
	DeviceExtension->API_v_1.LhSetGlobalInclusiveACL = LhSetGlobalInclusiveACL;
	DeviceExtension->API_v_1.LhSetExclusiveACL = LhSetExclusiveACL;
	DeviceExtension->API_v_1.LhSetInclusiveACL = LhSetInclusiveACL;
	DeviceExtension->API_v_1.LhIsProcessIntercepted = LhIsProcessIntercepted;

	/*
		Register for user-mode accessibility and set major functions...
	*/
    RtlInitUnicodeString(&DosDeviceName, EASYHOOK_DOS_DEVICE_NAME);

    if (!NT_SUCCESS(Status = IoCreateSymbolicLink(&DosDeviceName, &NtDeviceName)))
		goto ERROR_ABORT;

	SymbolicLink = TRUE;

    InDriverObject->MajorFunction[IRP_MJ_CREATE] = EasyHookDispatchCreate;
    InDriverObject->MajorFunction[IRP_MJ_CLOSE] = EasyHookDispatchClose;
    InDriverObject->MajorFunction[IRP_MJ_DEVICE_CONTROL] = EasyHookDispatchDeviceControl;
    InDriverObject->DriverUnload = EasyHookUnload;

	// initialize EasyHook
	if (!NT_SUCCESS(Status = LhBarrierProcessAttach()))
		goto ERROR_ABORT;

	PsSetLoadImageNotifyRoutine(OnImageLoadNotification);

    LhCriticalInitialize();

	return LhUpdateModuleInformation();

ERROR_ABORT:

	/*
		Rollback in case of errors...
	*/
	if (SymbolicLink)
		IoDeleteSymbolicLink(&DosDeviceName);

	if (DeviceObject != NULL)
		IoDeleteDevice(DeviceObject);

	return Status;
}

NTSTATUS EasyHookDispatchCreate(
	IN PDEVICE_OBJECT InDeviceObject,
	IN PIRP InIrp)
{
    InIrp->IoStatus.Information = 0;
    InIrp->IoStatus.Status = STATUS_SUCCESS;

    IoCompleteRequest(InIrp, IO_NO_INCREMENT);

    return STATUS_SUCCESS;
}

NTSTATUS EasyHookDispatchClose(
	IN PDEVICE_OBJECT InDeviceObject,
	IN PIRP InIrp)
{
    InIrp->IoStatus.Information = 0;
    InIrp->IoStatus.Status = STATUS_SUCCESS;

    IoCompleteRequest(InIrp, IO_NO_INCREMENT);

    return STATUS_SUCCESS;
}

/************************************************************
	
Description:

	Handles all device requests.

*/
NTSTATUS EasyHookDispatchDeviceControl(
	IN PDEVICE_OBJECT InDeviceObject,
	IN PIRP	InIrp)
{
    InIrp->IoStatus.Information = 0;
    InIrp->IoStatus.Status = STATUS_INVALID_PARAMETER;

    IoCompleteRequest(InIrp, IO_NO_INCREMENT);

    return STATUS_SUCCESS;
}

/***************************************************

Description:

	Release all resources and remove the driver object.
*/
VOID EasyHookUnload(IN PDRIVER_OBJECT InDriverObject)
{
    UNICODE_STRING			DosDeviceName;

    // remove all hooks and shutdown thread barrier...
    LhCriticalFinalize();

    LhBarrierProcessDetach();

	PsRemoveLoadImageNotifyRoutine(OnImageLoadNotification);

    /*
		Delete the symbolic link
    */
    RtlInitUnicodeString(&DosDeviceName, EASYHOOK_DOS_DEVICE_NAME);

    IoDeleteSymbolicLink(&DosDeviceName);

    /*
		Delete the device object
    */

    IoDeleteDevice(InDriverObject->DeviceObject);
}

