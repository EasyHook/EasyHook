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
using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.ConstrainedExecution;
using System.Diagnostics;

namespace EasyHook
{
    public partial class LocalHook
    {
        /// <summary>
        /// RIP relocation is disabled by default. If you want to enable it,
        /// just call this method which will attach a debugger to the current
        /// process. There may be circumstances under which this might fail
        /// and this is why it is not done by default. On 32-Bit system this
        /// method will always succeed and do nothing...
        /// </summary>
        public static void EnableRIPRelocation()
        {
            NativeAPI.DbgAttachDebugger();
        }

        /// <summary>
        /// Tries to get the underlying thread ID for a given handle.
        /// </summary>
        /// <remarks>
        /// This is not always possible. The handle has to be opened with <c>THREAD_QUERY_INFORMATION</c>
        /// access. 
        /// </remarks>
        /// <param name="InThreadHandle">A valid thread handle.</param>
        /// <returns>A valid thread ID associated with the given thread handle.</returns>
        /// <exception cref="AccessViolationException">
        /// The given handle was not opened with <c>THREAD_QUERY_INFORMATION</c> access.</exception>
        /// <exception cref="ArgumentException">
        /// The handle is invalid. 
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// Should never occur and just notifies you that a handle to thread ID conversion is not
        /// available on the current platform.
        /// </exception>
        public static Int32 GetThreadIdByHandle(IntPtr InThreadHandle)
        {
            Int32 Result;

            NativeAPI.DbgGetThreadIdByHandle(InThreadHandle, out Result);

            return Result;
        }

        /// <summary>
        /// Tries to get the underlying process ID for a given handle.
        /// </summary>
        /// <remarks>
        /// This is not always possible. The handle has to be opened with <c>PROCESS_QUERY_INFORMATION</c>
        /// access. 
        /// </remarks>
        /// <param name="InProcessHandle">A valid process handle.</param>
        /// <returns>A valid process ID associated with the given process handle.</returns>
        /// <exception cref="AccessViolationException">
        /// The given handle was not opened with <c>PROCESS_QUERY_INFORMATION</c> access.</exception>
        /// <exception cref="ArgumentException">
        /// The handle is invalid. 
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// Should never occur and just notifies you that a handle to thread ID conversion is not
        /// available on the current platform.
        /// </exception>
        public static Int32 GetProcessIdByHandle(IntPtr InProcessHandle)
        {
            Int32 Result;

            NativeAPI.DbgGetProcessIdByHandle(InProcessHandle, out Result);

            return Result;
        }

        [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
        private class UNICODE_STRING
        {
            public Int16 Length;
            public Int16 MaximumLength;
            public String Buffer;
        }

        internal class NameBuffer : CriticalFinalizerObject
        {
            public IntPtr Buffer;
            public Int32 Size;

            public NameBuffer()
            {
                Size = 0;
                Buffer = Marshal.AllocCoTaskMem(0);
            }

            public void Alloc(Int32 InDesiredSize)
            {
                IntPtr Tmp;

                if (InDesiredSize < Size)
                    return;

                if ((Tmp = Marshal.ReAllocCoTaskMem(Buffer, InDesiredSize)) == IntPtr.Zero)
                    throw new OutOfMemoryException("Unable to allocate unmanaged memory.");

                Buffer = Tmp;
                Size = InDesiredSize;
            }

            ~NameBuffer()
            {
                if(Buffer != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(Buffer);

                Buffer = IntPtr.Zero;
            }
        }
        private static NameBuffer Buffer = new NameBuffer();

        /// <summary>
        /// Reads the kernel object name for a given windows usermode handle.
        /// Executes in approx. 100 micro secounds.
        /// </summary>
        /// <remarks><para>
        /// This allows you to translate a handle back to the associated filename for example.
        /// But keep in mind that such names are only valid for kernel service routines, like
        /// <c>NtCreateFile</c>. You won't have success when calling <c>CreateFile</c> on such
        /// object names! The regular windows user mode API has some methods that will allow
        /// you to convert such kernelmode names back into usermode names. I know this because I did it
        /// some years ago but I've already forgotten how it has to be done! I can only give you
        /// some hints: <c>FindFirstVolume()</c>, <c>FindFirstVolumeMountPoint()</c>,
        /// <c>QueryDosDevice()</c>, <c>GetVolumePathNamesForVolumeName()</c>
        /// </para>
        /// <param name="InHandle">A valid usermode handle.</param>
        /// </remarks>
        /// <returns>The kernel object name associated with the given handle.</returns>
        /// <exception cref="ArgumentException">
        /// The given handle is invalid or could not be accessed for unknown reasons.
        /// </exception>
        public static String GetNameByHandle(IntPtr InHandle)
        {
            Int32 RequiredSize;

            NativeAPI.DbgHandleToObjectName(
                InHandle,
                IntPtr.Zero,
                0,
                out RequiredSize);


            lock (Buffer)
            {
                Buffer.Alloc(RequiredSize + 1);

                NativeAPI.DbgHandleToObjectName(
                    InHandle,
                    Buffer.Buffer,
                    RequiredSize,
                    out RequiredSize);

                UNICODE_STRING Result = new UNICODE_STRING();

                Marshal.PtrToStructure(Buffer.Buffer, Result);

                return Result.Buffer;
            }
        }
    }
}
