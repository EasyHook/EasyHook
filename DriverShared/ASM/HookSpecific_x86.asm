;
;    EasyHook - The reinvention of Windows API hooking
; 
;    Copyright (C) 2009 Christoph Husse
;
;    This library is free software; you can redistribute it and/or
;    modify it under the terms of the GNU Lesser General Public
;    License as published by the Free Software Foundation; either
;    version 2.1 of the License, or (at your option) any later version.
;
;    This library is distributed in the hope that it will be useful,
;    but WITHOUT ANY WARRANTY; without even the implied warranty of
;    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
;    Lesser General Public License for more details.
;
;    You should have received a copy of the GNU Lesser General Public
;    License along with this library; if not, write to the Free Software
;    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301  USA
;
;    Please visit http://www.codeplex.com/easyhook for more information
;    about the project and latest updates.
;

.386
.model flat, c
.code

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;	
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;; StealthStub_ASM_x86
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
public StealthStub_ASM_x86@0

StealthStub_ASM_x86@0 PROC

; Create thread...
	push		0
	push		0
	push		dword ptr [ebx + 16]		; save stealth context
	push		dword ptr [ebx + 8]			; RemoteThreadStart
	push		0
	push		0
	call		dword ptr [ebx + 0]			; CreateThread(0, NULL, RemoteThreadStart, RemoteThreadParam, 0, NULL);
	

; signal thread creation...
	push		dword ptr [ebx + 48]		
	mov			dword ptr [ebx + 48], eax
	call		dword ptr [ebx + 56]		; SetEvent(hSyncEvent);
	
; wait for completion
	push		-1
	push		dword ptr [ebx + 32]
	call		dword ptr [ebx + 24]		; WaitForSingleObject(hCompletionEvent, INFINITE)

; close handle
	push		dword ptr [ebx + 32]		
	call		dword ptr [ebx + 40]		; CloseHandle(hCompletionEvent);

; close handle
	push		dword ptr [ebx + 48]		
	call		dword ptr [ebx + 40]		; CloseHandle(hSyncEvent);
	
	
; restore context
	mov			eax, [ebx + 64 + 8 * 0]
	mov			ecx, [ebx + 64 + 8 * 1]
	mov			edx, [ebx + 64 + 8 * 2]
	mov			ebp, [ebx + 64 + 8 * 3]
	mov			esp, [ebx + 64 + 8 * 4]
	mov			esi, [ebx + 64 + 8 * 5]
	mov			edi, [ebx + 64 + 8 * 6]
	push		dword ptr[ebx + 64 + 8 * 9] ; push EFlags	
	push		dword ptr[ebx + 64 + 8 * 8]	; save old EIP
	mov			ebx, [ebx + 64 + 8 * 7]
	
	add			esp, 4
	popfd

; continue execution...
	jmp			dword ptr [esp - 8]	
	
; outro signature, to automatically determine code size
	db 78h
	db 56h
	db 34h
	db 12h
StealthStub_ASM_x86@0 ENDP

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;	
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;; Trampoline_ASM_x86
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
public Trampoline_ASM_x86@0

Trampoline_ASM_x86@0 PROC

; Handle:		1A2B3C05h
; NETEntry:		1A2B3C03h
; OldProc:		1A2B3C01h
; NewProc:		1A2B3C00h
; NETOutro:		1A2B3C06h
; IsExecuted:	1A2B3C02h
; RetAddr:		1A2B3C04h
; Ptr:NewProc:	1A2B3C07h

	mov eax, esp
	push ecx ; both are fastcall parameters, ECX is also used as "this"-pointer
	push edx
	mov ecx, eax; InitialRSP value for NETIntro()...
	
	mov eax, 1A2B3C02h
	db 0F0h ; interlocked increment execution counter
	inc dword ptr [eax]
	
; is a user handler available?
	mov eax, 1A2B3C07h
	cmp dword ptr[eax], 0
	
	db 3Eh ; branch usually taken
	jne CALL_NET_ENTRY
	
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;; call original method
		mov eax, 1A2B3C02h
		db 0F0h ; interlocked decrement execution counter
		dec dword ptr [eax]
		mov eax, 1A2B3C01h
		jmp TRAMPOLINE_EXIT

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;; call hook handler or original method...
CALL_NET_ENTRY:	
	
; call NET intro
	push ecx
	push dword ptr [esp + 12] ; push return address
	push 1A2B3C05h ; Hook handle
	mov eax, 1A2B3C03h
	call eax ; Hook->NETIntro(Hook, RetAddr);
	
; should call original method?
	test eax, eax
	
	db 3Eh ; branch usually taken
	jne CALL_HOOK_HANDLER
	
	; call original method
		mov eax, 1A2B3C02h
		db 0F0h ; interlocked decrement execution counter
		dec dword ptr [eax]
		mov eax, 1A2B3C01h
		jmp TRAMPOLINE_EXIT
		
CALL_HOOK_HANDLER:
; adjust return address --- ATTENTION: this offset "83h" will also change if CALL_NET_OUTRO moves due to changes...
	mov dword ptr [esp + 8], 1A2B3C04h

; call hook handler
	mov eax, 1A2B3C00h
	jmp TRAMPOLINE_EXIT 

CALL_NET_OUTRO: ; this is where the handler returns...

; call NET outro --- ATTENTION: Never change EAX/EDX from now on!
	push 0 ; space for return address
	push eax
	push edx
	
	lea eax, [esp + 8]
	push eax ; Param 2: Address of return address
	push 1A2B3C05h ; Param 1: Hook handle
	mov eax, 1A2B3C06h
	call eax ; Hook->NETOutro(Hook);
	
	mov eax, 1A2B3C02h
	db 0F0h ; interlocked decrement execution counter
	dec dword ptr [eax]
	
	pop edx ; restore return value of user handler...
	pop eax
	
; finally return to saved return address - the caller of this trampoline...
	ret
	
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;; generic outro for both cases...
TRAMPOLINE_EXIT:

	pop edx
	pop ecx
	
	jmp eax ; ATTENTION: In case of hook handler we will return to CALL_NET_OUTRO, otherwise to the caller...
	
; outro signature, to automatically determine code size
	db 78h
	db 56h
	db 34h
	db 12h

Trampoline_ASM_x86@0 ENDP

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;	
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;; HookInjectionCode_ASM_x86
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
public Injection_ASM_x86@0
Injection_ASM_x86@0 PROC
; no registers to save, because this is the thread main function
; save first param (address of hook injection information)

	mov esi, dword ptr [esp + 4]
	
; call LoadLibraryW(Inject->EasyHookPath);
	push dword ptr [esi + 8]
	
	call dword ptr [esi + 40] ; LoadLibraryW@4
	mov ebp, eax
	test eax, eax
	je HookInject_FAILURE_A
	
; call GetProcAddress(eax, Inject->EasyHookEntry);
	push dword ptr [esi + 24]
	push ebp
	call dword ptr [esi + 56] ; GetProcAddress@8
	test eax, eax
	je HookInject_FAILURE_B
	
; call EasyHookEntry(Inject);
	push esi
	call eax
	push eax ; save error code

; call FreeLibrary(ebp)
	push ebp
	call dword ptr [esi + 48] ; FreeLibrary@4
	test eax, eax
	je HookInject_FAILURE_C
	jmp HookInject_EXIT
	
HookInject_FAILURE_A:
	call dword ptr [esi + 88] ; GetLastError
	or eax, 40000000h
	jmp HookInject_FAILURE_E
HookInject_FAILURE_B:
	call dword ptr [esi + 88] ; GetLastError
	or eax, 10000000h
	jmp HookInject_FAILURE_E	
HookInject_FAILURE_C:
	call dword ptr [esi + 88] ; GetLastError
	or eax, 30000000h
	jmp HookInject_FAILURE_E	
HookInject_FAILURE_E:
	push eax ; save error value
	
HookInject_EXIT:

	push 0
	push 0
	push 0; // shadow space for executable stack part...

; call VirtualProtect(Outro, 4, PAGE_EXECUTE_READWRITE, &OldProtect)
	lea ebx, dword ptr [esp + 8] ; we'll write to shadow space
	push ebx
	push 40h
	push 12
	push ebx
	call dword ptr [esi + 72] ; VirtualProtect@16
	test eax, eax
	
	jne HookInject_EXECUTABLE

	; failed to make stack executable
		call dword ptr [esi + 88] ; GetLastError
		or eax, 20000000h
		add esp, 16
		ret
		
HookInject_EXECUTABLE:
; save outro to executable stack
	mov dword ptr [esp],	 0448BD3FFh		; call ebx [VirtualFree()]
	mov dword ptr [esp + 4], 05C8B0C24h		; mov eax, [esp + 12]
	mov dword ptr [esp + 8], 0E3FF1024h		; mov ebx, [esp + 16]
											; jmp ebx [exit thread]
	
; save params for VirtualFree(Inject->RemoteEntryPoint, 0, MEM_RELEASE);
	mov ebx, [esi + 64] ; VirtualFree()
	push 08000h
	push 0
	push dword ptr [esi + 16]
	
	lea eax, dword ptr [esp + 12]
	jmp eax
	
; outro signature, to automatically determine code size
	db 78h
	db 56h
	db 34h
	db 12h

Injection_ASM_x86@0 ENDP

END