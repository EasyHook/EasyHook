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
#ifndef _NTSTATUS_H_
#define _NTSTATUS_H_

#define STATUS_SUCCESS                   ((NTSTATUS)0x000000000)
#define STATUS_NOT_SUPPORTED             ((NTSTATUS)0xC00000BBL)
#define STATUS_INTERNAL_ERROR            ((NTSTATUS)0xC00000E5L)
#define STATUS_PROCEDURE_NOT_FOUND       ((NTSTATUS)0xC000007AL)
#define STATUS_NOINTERFACE               ((NTSTATUS)0xC00002B9L)
#define STATUS_INFO_LENGTH_MISMATCH      ((NTSTATUS)0xC0000004L)
#define STATUS_BUFFER_TOO_SMALL          ((NTSTATUS)0xC0000023L)
#define STATUS_INVALID_PARAMETER         ((NTSTATUS)0xC000000DL)
#define STATUS_INSUFFICIENT_RESOURCES    ((NTSTATUS)0xC000009AL)
#define STATUS_UNHANDLED_EXCEPTION       ((NTSTATUS)0xC0000144L)
#define STATUS_NOT_FOUND                 ((NTSTATUS)0xC0000225L)
#define STATUS_NOT_IMPLEMENTED           ((NTSTATUS)0xC0000002L)
#define STATUS_ACCESS_DENIED             ((NTSTATUS)0xC0000022L)
#define STATUS_ALREADY_REGISTERED        ((NTSTATUS)0xC0000718L)
#define STATUS_WOW_ASSERTION             ((NTSTATUS)0xC0009898L)
#define STATUS_BUFFER_OVERFLOW           ((NTSTATUS)0x80000005L)
#define STATUS_DLL_INIT_FAILED           ((NTSTATUS)0xC0000142L)


#define STATUS_INVALID_PARAMETER_1       ((NTSTATUS)0xC00000EFL)
#define STATUS_INVALID_PARAMETER_1       ((NTSTATUS)0xC00000EFL)
#define STATUS_INVALID_PARAMETER_2       ((NTSTATUS)0xC00000F0L)
#define STATUS_INVALID_PARAMETER_3       ((NTSTATUS)0xC00000F1L)
#define STATUS_INVALID_PARAMETER_4       ((NTSTATUS)0xC00000F2L)
#define STATUS_INVALID_PARAMETER_5       ((NTSTATUS)0xC00000F3L)
#define STATUS_INVALID_PARAMETER_6       ((NTSTATUS)0xC00000F4L)
#define STATUS_INVALID_PARAMETER_7       ((NTSTATUS)0xC00000F5L)
#define STATUS_INVALID_PARAMETER_8       ((NTSTATUS)0xC00000F6L)

#endif