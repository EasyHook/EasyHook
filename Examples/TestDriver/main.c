/*
    You may use this file without any restriction...
*/
#include "EasyHook.h"

NTSTATUS DriverEntry(
	IN PDRIVER_OBJECT InDriverObject,
	IN PUNICODE_STRING InRegistryPath);

NTSTATUS TestDriverDispatchCreate(
	IN PDEVICE_OBJECT InDeviceObject,
	IN PIRP	InIrp);

NTSTATUS TestDriverDispatchClose(
	IN PDEVICE_OBJECT InDeviceObject,
	IN PIRP InIrp);

NTSTATUS TestDriverDispatchDeviceControl(
	IN PDEVICE_OBJECT InDeviceObject,
	IN PIRP InIrp);

VOID TestDriverUnload(IN PDRIVER_OBJECT DriverObject);

#ifdef ALLOC_PRAGMA

#pragma alloc_text(INIT, DriverEntry)
#pragma alloc_text(PAGE, TestDriverDispatchCreate)
#pragma alloc_text(PAGE, TestDriverDispatchClose)
#pragma alloc_text(PAGE, TestDriverDispatchDeviceControl)
#pragma alloc_text(PAGE, TestDriverUnload)

#endif


NTSTATUS DriverEntry(
	IN PDRIVER_OBJECT		InDriverObject,
	IN PUNICODE_STRING		InRegistryPath)
{
	NTSTATUS						Status;    
	PDEVICE_OBJECT					DeviceObject = NULL;

	/*
		Create device...
	*/
    if (!NT_SUCCESS(Status = IoCreateDevice(
		InDriverObject,
		0,								// DeviceExtensionSize
		NULL,
		0x893D,							// DeviceType
		0,								// DeviceCharacteristics
		TRUE,							// Exclusive
		&DeviceObject					// [OUT]
		)))
		goto ERROR_ABORT;


    InDriverObject->MajorFunction[IRP_MJ_CREATE] = TestDriverDispatchCreate;
    InDriverObject->MajorFunction[IRP_MJ_CLOSE] = TestDriverDispatchClose;
    InDriverObject->MajorFunction[IRP_MJ_DEVICE_CONTROL] = TestDriverDispatchDeviceControl;
    InDriverObject->DriverUnload = TestDriverUnload;

	// run test code...
	return RunTestSuite();

ERROR_ABORT:

	/*
		Rollback in case of errors...
	*/
	if (DeviceObject != NULL)
		IoDeleteDevice(DeviceObject);

	return Status;
}

NTSTATUS TestDriverDispatchCreate(
	IN PDEVICE_OBJECT InDeviceObject,
	IN PIRP InIrp)
{
    InIrp->IoStatus.Information = 0;
    InIrp->IoStatus.Status = STATUS_SUCCESS;

    IoCompleteRequest(InIrp, IO_NO_INCREMENT);

    return STATUS_SUCCESS;
}

NTSTATUS TestDriverDispatchClose(
	IN PDEVICE_OBJECT InDeviceObject,
	IN PIRP InIrp)
{
    InIrp->IoStatus.Information = 0;
    InIrp->IoStatus.Status = STATUS_SUCCESS;

    IoCompleteRequest(InIrp, IO_NO_INCREMENT);

    return STATUS_SUCCESS;
}

NTSTATUS TestDriverDispatchDeviceControl(
	IN PDEVICE_OBJECT InDeviceObject,
	IN PIRP	InIrp)
{
    InIrp->IoStatus.Information = 0;
    InIrp->IoStatus.Status = STATUS_INVALID_PARAMETER;

    IoCompleteRequest(InIrp, IO_NO_INCREMENT);

    return STATUS_SUCCESS;
}

VOID TestDriverUnload(IN PDRIVER_OBJECT InDriverObject)
{
    /*
		Delete the device object
    */

    IoDeleteDevice(InDriverObject->DeviceObject);
}

