udis86
======

Udis86 is a disassembler engine that interprets and decodes a stream of
binary machine code bytes as opcodes defined in the x86 and x86-64 class
of Instruction Set Archictures. The core component of this project is
libudis86 which provides a clean and simple interface to disassemble
binary code, and to inspect the disassembly to various degrees of
details. The library is designed to aid software projects that entail
analysis and manipulation of all flavors of x86 binary code.


LICENSE
-------

udis86 is an open source project, distributed under the terms of the
2-clause "Simplified BSD License". A copy of the license is included
with the source in $(SRC)/LICENSE.


libudis86
---------

* Full support for the x86 and x86-64 (AMD64) range of instruction set
  architectures.
* Full support for all AMD-V, INTEL-VMX, MMX, SSE, SSE2, SSE3, SSSE3,
  SSE4.1, SSE4.2, FPU(x87), and AMD 3Dnow! instructions.
* Supports 16bit, 32bit, and 64bit disassembly modes.
* Supports instruction meta-data using XML based decode tables.
* Generates output in AT&T or INTEL assembler language syntaxes.
* Supports flexbile input methods: File, Buffer, and Hooks.
* Reentrant.
* Clean and very easy-to-use API.


udcli
-----

udcli is a small command-line tool for your quick disassembly needs.

  $ echo "65 67 89 87 76 65 54 56 78 89 09 00 87" | udcli -32 -x 
  0000000000000000 656789877665    mov [gs:bx+0x6576], eax
  0000000000000000 54              push esp
  0000000000000000 56              push esi
  0000000000000000 7889            js 0x93 
  0000000000000000 0900            or [eax], eax


Documentation
-------------

libudis86 api is fully documented and included with the package in
$(SRC)/docs/manual


Author
------

udis86 is written and maintained by Vivek Thampi (vivek.mt@gmail.com).
