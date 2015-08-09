/*
    You may use this file without any restriction...
*/
#include "EasyHook.h"


#define FORCE(expr)     {if(!NT_SUCCESS(NtStatus = (expr))) goto ERROR_ABORT;}

static EASYHOOK_INTERFACE_API_v_1		Interface;

BOOLEAN KeCancelTimer_Hook(PKTIMER InTimer)
{
	PVOID					CallStack[64];
	MODULE_INFORMATION		Mod;
	ULONG					MethodCount;

	Interface.LhBarrierPointerToModule(0, 0);

	Interface.LhBarrierCallStackTrace(CallStack, 64, &MethodCount);

	Interface.LhBarrierGetCallingModule(&Mod);

	return KeCancelTimer(InTimer);
}


NTSTATUS RunTestSuite()
{
	HOOK_TRACE_INFO			hHook = { NULL };
    NTSTATUS                NtStatus;
    ULONG                   ACLEntries[1] = {0};
	UNICODE_STRING			SymbolName;
	KTIMER					Timer;
	BOOLEAN					HasInterface = FALSE;
	PFILE_OBJECT			hEasyHookDrv;

	FORCE(EasyHookQueryInterface(EASYHOOK_INTERFACE_v_1, &Interface, &hEasyHookDrv));

	HasInterface = TRUE;

	RtlInitUnicodeString(&SymbolName, L"KeCancelTimer");

    /*
        The following shows how to install and remove local hooks...
    */
    FORCE(Interface.LhInstallHook(
            MmGetSystemRoutineAddress(&SymbolName),
            KeCancelTimer_Hook,
            (PVOID)0x12345678,
            &hHook));

    // won't invoke the hook handle because hooks are inactive after installation
	KeInitializeTimer(&Timer);

    KeCancelTimer(&Timer);

    // activate the hook for the current thread
    FORCE(Interface.LhSetInclusiveACL(ACLEntries, 1, &hHook));

    // will be redirected into the handler...
    KeCancelTimer(&Timer);

    // this will NOT unhook the entry point. But the associated handler is never called again...
    Interface.LhUninstallHook(&hHook);

    // this will restore ALL entry points of currently rending removals issued by LhUninstallHook()
    Interface.LhWaitForPendingRemovals();

	ObDereferenceObject(hEasyHookDrv);

	return STATUS_SUCCESS;

ERROR_ABORT:

	if(HasInterface)
	{
		ObDereferenceObject(hEasyHookDrv);

		KdPrint(("\n[Error]: \"%S\" (code: %d)\n", Interface.RtlGetLastErrorString(), Interface.RtlGetLastError()));
	}
	else
		KdPrint(("\n[Error]: \"Unable to obtain EasyHook interface.\" (code: %d)\n", NtStatus));

    return NtStatus;
}