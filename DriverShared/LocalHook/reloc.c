// EasyHook (File: EasyHookDll\reloc.c)
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
#include "disassembler/udis86.h"

// GetInstructionLength_x64/x86 were replaced with the Udis86 library
// (http://udis86.sourceforge.net) see udis86.h/.c for appropriate
// licensing and copyright notices.

EASYHOOK_NT_INTERNAL LhGetInstructionLength(void* InPtr)
{
/*
Description:

    Takes a pointer to machine code and returns the length of the
    referenced instruction in bytes.
    
Returns:
    STATUS_INVALID_PARAMETER

        The given pointer references invalid machine code.
*/
	LONG			length = -1;
	// some exotic instructions might not be supported see the project
    // at https://github.com/vmt/udis86 and the forums.

    ud_t ud_obj;
    ud_init(&ud_obj);
#ifdef _M_X64
    ud_set_mode(&ud_obj, 64);
#else
    ud_set_mode(&ud_obj, 32);
#endif
    ud_set_input_buffer(&ud_obj, (uint8_t *)InPtr, 32);
    length = ud_disassemble(&ud_obj); // usually only between 1 and 5

	if(length > 0)
		return length;
	else
		return STATUS_INVALID_PARAMETER;
}

EASYHOOK_NT_INTERNAL LhRoundToNextInstruction(
			void* InCodePtr,
			ULONG InCodeSize)
{
/*
Description:

    Will round the given code size up so that the return
    value spans at least over "InCodeSize" bytes and always
    ends on instruction boundaries.

Parameters:

    - InCodePtr

        A code portion the given size should be aligned to.

    - InCodeSize

        The minimum return value.

Returns:

    STATUS_INVALID_PARAMETER

        The given pointer references invalid machine code.
*/
	UCHAR*				Ptr = (UCHAR*)InCodePtr;
	UCHAR*				BasePtr = Ptr;
    NTSTATUS            NtStatus;

	while(BasePtr + InCodeSize > Ptr)
	{
		FORCE(NtStatus = LhGetInstructionLength(Ptr));

		Ptr += NtStatus;
	}

	return (ULONG)(Ptr - BasePtr);

THROW_OUTRO:
    return NtStatus;
}

EASYHOOK_NT_INTERNAL LhDisassembleInstruction(void* InPtr, ULONG* length, PSTR buf, LONG buffSize, ULONG64 *nextInstr)
{
/*
Description:

    Takes a pointer to machine code and returns the length and
    ASM code for the referenced instruction.
    
Returns:
    STATUS_INVALID_PARAMETER

        The given pointer references invalid machine code.
*/
    // some exotic instructions might not be supported see the project
    // at https://github.com/vmt/udis86.

    ud_t ud_obj;
    ud_init(&ud_obj);
#ifdef _M_X64
    ud_set_mode(&ud_obj, 64);
#else
    ud_set_mode(&ud_obj, 32);
#endif
    ud_set_syntax(&ud_obj, UD_SYN_INTEL);
    ud_set_asm_buffer(&ud_obj, buf, buffSize);
    ud_set_input_buffer(&ud_obj, (uint8_t *)InPtr, 32);
    *length = ud_disassemble(&ud_obj);
    
    *nextInstr = (ULONG64)InPtr + *length;

    if(length > 0)
        return STATUS_SUCCESS;
    else
        return STATUS_INVALID_PARAMETER;
}

EASYHOOK_NT_INTERNAL LhRelocateRIPRelativeInstruction(
            ULONGLONG InOffset,
            ULONGLONG InTargetOffset,
            BOOL* OutWasRelocated)
{
/*
Description:

    Check whether the given instruction is RIP relative and
    relocates it. If it is not RIP relative, nothing is done.
    Only applicable to 64-bit processes, 32-bit will always
    return FALSE.

Parameters:

    - InOffset

        The instruction pointer to check for RIP addressing and relocate.

    - InTargetOffset

        The instruction pointer where the RIP relocation should go to.
        Please note that RIP relocation are relocated relative to the
        offset you specify here and therefore are still not absolute!

    - OutWasRelocated

        TRUE if the instruction was RIP relative and has been relocated,
        FALSE otherwise.
*/

#ifndef _M_X64
    return FALSE;
#else
#ifndef MAX_INSTR
#define MAX_INSTR 100
#endif
    NTSTATUS            NtStatus;
	CHAR                Buf[MAX_INSTR];
    ULONG               AsmSize;
    ULONG64             NextInstr;
	CHAR                Line[MAX_INSTR];
    LONG                Pos;
    LONGLONG            RelAddr;
    LONGLONG            MemDelta = InTargetOffset - InOffset;

	ULONGLONG           RelAddrOffset = 0;
    LONGLONG            RelAddrSign = 1;

    ASSERT(MemDelta == (LONG)MemDelta,L"reloc.c - MemDelta == (LONG)MemDelta");

    *OutWasRelocated = FALSE;

    // test field...
    /*
    BYTE t[10] = {0x8b, 0x05, 0x12, 0x34, 0x56, 0x78};
    // udis86 outputs: 0000000000000000 8b0512345678     mov eax, [rip+0x78563412]

    InOffset = (LONGLONG)t;

    MemDelta = InTargetOffset - InOffset;
    */
    // Disassemble the current instruction
    if(!RTL_SUCCESS(LhDisassembleInstruction((void*)InOffset, &AsmSize, Buf, sizeof(Buf), &NextInstr)))
        THROW(STATUS_INVALID_PARAMETER_1, L"Unable to disassemble entry point. ");
    
    // Check that the address is RIP relative (i.e. look for "[rip+")
    Pos = RtlAnsiIndexOf(Buf, '[');
      if(Pos < 0)
        RETURN;

    if (Buf[Pos + 1] == 'r' && Buf[Pos + 2] == 'i' && Buf[Pos + 3] == 'p' &&  (Buf[Pos + 4] == '+' || Buf[Pos + 4] == '-'))
    {
        /*
          Support negative relative addresses
          
          https://easyhook.codeplex.com/workitem/25592
            e.g. Win8.1 64-bit OLEAUT32.dll!VarBoolFromR8
            Entry Point:
              66 0F 2E 05 DC 25 FC FF   ucomisd xmm0, [rip-0x3da24]   IP:ffc46d4
            Relocated:
              66 0F 2E 05 10 69 F6 FF   ucomisd xmm0, [rip-0x996f0]   IP:100203a0
        */
        if (Buf[Pos + 4] == '-')
            RelAddrSign = -1;
        
        Pos += 4;
        // parse content
		if (RtlAnsiSubString(Buf, Pos + 1, RtlAnsiIndexOf(Buf, ']') - Pos - 1, Line, MAX_INSTR) <= 0)
            RETURN;

        // Convert HEX string to LONGLONG
		RelAddr = RtlAnsiHexToLongLong(Line, MAX_INSTR);
        if (!RelAddr)
            RETURN;

        // Apply correct sign
        RelAddr *= RelAddrSign;

        // Verify that we are really RIP relative (i.e. must be 32-bit)
        if(RelAddr != (LONG)RelAddr)
            RETURN;
        
        /*
          Ensure the RelAddr is equal to the RIP address in code
         
          https://easyhook.codeplex.com/workitem/25487
		  Thanks to Michal for pointing out that the operand will not always 
          be at *(NextInstr - 4)
		  e.g. Win8.1 64-bit OLEAUT32.dll!GetVarConversionLocaleSetting 
              Entry Point:
                 83 3D 71 08 06 00 00    cmp dword [rip+0x60871], 0x0  IP:ffa1937
              Relocated:
                 83 3D 09 1E 0B 00 00    cmp dword [rip+0xb1e09], 0x0  IP:ff5039f
        */
		for (Pos = 1; Pos <= NextInstr - InOffset - 4; Pos++) {
			if (*((LONG*)(InOffset + Pos)) == RelAddr) {
				if (RelAddrOffset != 0) {
					// More than one offset matches the address, therefore we can't determine correct offset for operand
                    RelAddrOffset = 0;  
                    break;
				}

				RelAddrOffset = Pos;
			}
		}

		if (RelAddrOffset == 0) {
			THROW(STATUS_INTERNAL_ERROR, L"The given entry point contains a RIP-relative instruction for which we can't determine the correct address offset!");
		}

		/*
            Relocate this instruction...
        */
        // Adjust the relative address
        RelAddr = RelAddr - MemDelta;
        // Ensure the RIP address can still be relocated
        if(RelAddr != (LONG)RelAddr)
            THROW(STATUS_NOT_SUPPORTED, L"The given entry point contains at least one RIP-Relative instruction that could not be relocated!");

        // Copy instruction to target
        RtlCopyMemory((void*)InTargetOffset, (void*)InOffset, (ULONG)(NextInstr - InOffset));
        // Correct the rip address
        *((LONG*)(InTargetOffset + RelAddrOffset)) = (LONG)RelAddr;

        *OutWasRelocated = TRUE;
    }

    RETURN;

THROW_OUTRO:
FINALLY_OUTRO:
    return NtStatus;
#endif
}

EASYHOOK_NT_INTERNAL LhRelocateEntryPoint(
				UCHAR* InEntryPoint,
				ULONG InEPSize,
				UCHAR* Buffer,
				ULONG* OutRelocSize)
{
/*
Description:

    Relocates the given entry point into the buffer and finally
    stores the relocated size in OutRelocSize.

Parameters:

    - InEntryPoint

        The entry point to relocate.

    - InEPSize

        Size of the given entry point in bytes.

    - Buffer

        A buffer receiving the relocated entry point.
        To ensure that there is always enough space, you should
        reserve around 100 bytes. After completion this method will
        store the real size in bytes in "OutRelocSize".
		Important: all instructions using RIP relative addresses will 
		be relative to the buffer location in memory.

    - OutRelocSize

        Receives the size of the relocated entry point in bytes.

Returns:

*/
#ifdef _M_X64
    #define POINTER_TYPE    LONGLONG
#else
    #define POINTER_TYPE    LONG
#endif
	UCHAR*				pRes = Buffer;
	UCHAR*				pOld = InEntryPoint;
    UCHAR			    b1;
	UCHAR			    b2;
	ULONG			    OpcodeLen;
	POINTER_TYPE   	    AbsAddr;
	BOOL			    a16 = FALSE;
	BOOL			    IsRIPRelative;
    ULONG               InstrLen;
    NTSTATUS            NtStatus;

	ASSERT(InEPSize < 20,L"reloc.c - InEPSize < 20");

	while(pOld < InEntryPoint + InEPSize)
	{
		b1 = *(pOld);
		b2 = *(pOld + 1);
		OpcodeLen = 0;
		AbsAddr = 0;
		IsRIPRelative = FALSE;

		// check for prefixes
		switch(b1)
		{
            case 0x67: // address-size override prefix
    			// TODO: this implementation does not take into consideration all scenarios
                //       e.g. http://wiki.osdev.org/X86-64_Instruction_Encoding#Operand-size_and_address-size_override_prefix
                a16 = TRUE; 
                // We increment pOld to skip this prefix, pOld is then decremented 
                // if the instruction is to be copied unchanged.
				pOld++;
				continue;
            /*
                Don't need to do anything with 0x66 (operand/data-size prefix), we only need to know when an address-size override prefix is present.
                Instructions with 0x66 generally end up unchanged (except 64-bit rip relative addresses where we only adjust the address anyway)
            */
		}

		/////////////////////////////////////////////////////////
		// get relative address value
		switch(b1)
		{
			case 0xE9: // jmp imm16/imm32
			{
				/* only allowed as first instruction and only if the trampoline can be planted 
				   within a 32-bit boundary around the original entrypoint. So the jumper will 
				   be only 5 bytes and whereever the underlying code returns it will always
				   be in a solid state. But this can only be guaranteed if the jump is the first
				   instruction... */
				if(pOld != InEntryPoint)
					THROW(STATUS_NOT_SUPPORTED, L"Hooking far jumps is only supported if they are the first instruction.");
				
				// ATTENTION: will continue in "case 0xE8"
			}
			case 0xE8: // call imm16/imm32
			{
				if(a16)
				{
					AbsAddr = *((__int16*)(pOld + 1));
					OpcodeLen = 3;
				}
				else
				{
					AbsAddr = *((__int32*)(pOld + 1));
					OpcodeLen = 5;
				}
			}break;

        	case 0xEB: // jmp imm8
            {
                AbsAddr = *((__int8*)(pOld + 1));
                OpcodeLen = 2;
            }break;
        /*
			The problem with (conditional) jumps is that there will be no return into the relocated entry point.
			So the execution will be proceeded in the original method and this will cause the whole
			application to remain in an unstable state. Only near jumps with 32-bit offset are allowed as
			first instruction (see above)...
		*/
			case 0xE3: // jcxz imm8
			{
				THROW(STATUS_NOT_SUPPORTED, L"Hooking near (conditional) jumps is not supported.");
			}break;
			case 0x0F:
			{
				if((b2 & 0xF0) == 0x80) // jcc imm16/imm32
					THROW(STATUS_NOT_SUPPORTED,  L"Hooking far conditional jumps is not supported.");
			}break;
		}

		if((b1 & 0xF0) == 0x70) // jcc imm8
			THROW(STATUS_NOT_SUPPORTED,  L"Hooking near conditional jumps is not supported.");

		/////////////////////////////////////////////////////////
		// convert to: mov eax, AbsAddr

		if(OpcodeLen > 0)
		{
			AbsAddr += (POINTER_TYPE)(pOld + OpcodeLen);

#ifdef _M_X64
			*(pRes++) = 0x48; // REX.W-Prefix
#endif
			*(pRes++) = 0xB8;               // mov eax,
			*((LONGLONG*)pRes) = AbsAddr;   //          address

			pRes += sizeof(void*);

			// points into entry point?
			if((AbsAddr >= (LONGLONG)InEntryPoint) && (AbsAddr < (LONGLONG)InEntryPoint + InEPSize))
				/* is not really unhookable but not worth the effort... */
				THROW(STATUS_NOT_SUPPORTED, L"Hooking jumps into the hooked entry point is not supported.");

			/////////////////////////////////////////////////////////
			// insert alternate code
			switch(b1)
			{
			case 0xE8: // call eax
				{
					*(pRes++) = 0xFF;
					*(pRes++) = 0xD0;
				}break;
			case 0xE9: // jmp eax
            case 0xEB: // jmp imm8
				{
					*(pRes++) = 0xFF;
					*(pRes++) = 0xE0;
				}break;
			}

			/* such conversions shouldnt be necessary in general...
			   maybe the method was already hooked or uses some hook protection or is just
			   bad programmed. EasyHook is capable of hooking the same method
			   many times simultanously. Even if other (unknown) hook libraries are hooking methods that
			   are already hooked by EasyHook. Only if EasyHook hooks methods that are already
			   hooked with other libraries there can be problems if the other libraries are not
			   capable of such a "bad" circumstance.
			*/

			*OutRelocSize = (ULONG)(pRes - Buffer);
		}
		else
		{
            // Check for RIP relative instructions and relocate
            FORCE(LhRelocateRIPRelativeInstruction((ULONGLONG)pOld, (ULONGLONG)pRes, &IsRIPRelative));
		}

		// If 16-bit address-prefix override, move pointer back to start of instruction
		if (a16) pOld--;

		// find next instruction
		FORCE(InstrLen = LhGetInstructionLength(pOld));

		if(OpcodeLen == 0)
		{
			// just copy the instruction
			if(!IsRIPRelative)
				RtlCopyMemory(pRes, pOld, InstrLen);

			pRes += InstrLen;
		}

		pOld += InstrLen;
		IsRIPRelative = FALSE;
		a16 = FALSE;
	}

	*OutRelocSize = (ULONG)(pRes - Buffer);

	RETURN(STATUS_SUCCESS);

THROW_OUTRO:
FINALLY_OUTRO:
    return NtStatus;
}



EASYHOOK_NT_EXPORT LhGetHookBypassAddress(TRACED_HOOK_HANDLE InHook, PVOID** OutAddress)
{
/*
Description:

	Retrieves the address to bypass the hook. Using the returned value to call the original
	function bypasses all thread safety measures and must be used with care.
	This function should be called each time the address is required to ensure the hook  and
	associated memory is still valid at the time of use.
	CAUTION:
	This must be used with extreme caution. If the hook is uninstalled and pending hooks 
	removed, the address returned by this function will no longer point to valid memory and 
	attempting to use the address will result in unexpected behaviour, most likely crashing 
	the process.

Parameters:

	- InHook

		The hook to retrieve the relocated entry point for.

	- OutAddress

		Upon successfully retrieving the hook details this will contain
		the address of the relocated function entry point. This address
		can be used to call the original function from outside of a hook
		while still bypassing the hook.
	
Returns:

	STATUS_SUCCESS             - OutAddress will contain the result
	STATUS_INVALID_PARAMETER_1 - the hook is invalid
	STATUS_INVALID_PARAMETER_3 - the target pointer is invalid

*/
	NTSTATUS			NtStatus;
	PLOCAL_HOOK_INFO    Handle;

	if (!LhIsValidHandle(InHook, &Handle))
		THROW(STATUS_INVALID_PARAMETER_1, L"The given hook handle is invalid or already disposed.");

	if (!IsValidPointer(OutAddress, sizeof(PVOID*)))
		THROW(STATUS_INVALID_PARAMETER_3, L"Invalid pointer for result storage.");

	*OutAddress = (PVOID*)Handle->OldProc;

	RETURN;

THROW_OUTRO:
FINALLY_OUTRO:
	return NtStatus;
}