// EasyHook (File: EasyHook\DllImport.cs)
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

using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace EasyHook
{
    static class NativeAPI_EasyHook
    {
        static bool _initialised = false;

        public static string DllName32 { get; private set; } = "EasyHook32.dll";
        public static string DllName64 { get; private set; } = "EasyHook64.dll";

        /// <summary>
        /// The path and/or name of the EasyHook native binary (e.g. EasyHook32.dll or EasyHook64.dll)
        /// </summary>
        public static string DllName
        {
            get
            {
                return NativeAPI.Is64Bit ? DllName64 : DllName32;
            }
        }

        /// <summary>
        /// Override the easyhook native dll filename. Using force=true will force all existing hooks to be unloaded.
        /// </summary>
        /// <param name="filename32">The 32-bit EasyHook library file name to use</param>
        /// <param name="filename64">The 64-bit EasyHook library file name to use</param>
        /// <param name="force">If the filename differs from previously set filename and <paramref name="force"/> is false: will throw an exception if any native EasyHook functions have been accessed, if true: existing EasyHook native library will be unloaded if already loaded</param>
        public static void SetDllNames(string filename32, string filename64, bool force)
        {
            lock (_mutex)
            {
                if (filename32 != DllName32 || filename64 != DllName64)
                {
                    if (_initialised)
                    {
                        if (!force)
                        {
                            throw new InvalidOperationException($"{nameof(DllName)} cannot be changed after any {nameof(NativeAPI)} exports have been called.");
                        }
                        else
                        {
                            _initialised = false;
                            NativeAPI.FreeLibrary(_moduleHandle); // unload existing
                            _delegates.Clear();
                        }
                    }

                    DllName32 = filename32;
                    DllName64 = filename64;
                }
            }
        }

        static IntPtr _moduleHandle;
        static Dictionary<string, Delegate> _delegates = new Dictionary<string, Delegate>();
        static object _mutex = new object();

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="DllNotFoundException" />
        static void Initialise()
        {
            lock (_mutex)
            {
                if (_initialised)
                {
                    return;
                }

                _moduleHandle = NativeAPI.LoadLibrary(DllName);

                _initialised = _moduleHandle != IntPtr.Zero;
                if (!_initialised)
                {
                    throw new DllNotFoundException(DllName);
                }
            }
        }

        /// <summary>
        /// Attempt to load method address and assign to delegate
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <exception cref="MissingMethodException" />
        /// <returns></returns>
        static T GetDelegate<T>(string name) where T : class
        {
            lock (_mutex)
            {
                Initialise();

                if (!_delegates.ContainsKey(name))
                {
                    _delegates[name] = Marshal.GetDelegateForFunctionPointer(NativeAPI.GetProcAddress(_moduleHandle, name), typeof(T));
                }

                var method = _delegates[name] as T;

                if (method == null)
                {
                    throw new MissingMethodException(name);
                }

                return method;
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        delegate string RtlGetLastErrorStringCopy_Delegate();
        public static string RtlGetLastErrorStringCopy()
        {
            var method = GetDelegate<RtlGetLastErrorStringCopy_Delegate>(nameof(RtlGetLastErrorStringCopy));
            return method.Invoke();
        }

        public static String RtlGetLastErrorString()
        {
            return RtlGetLastErrorStringCopy();
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        delegate Int32 RtlGetLastError_Delegate();
        public static Int32 RtlGetLastError()
        {
            var method = GetDelegate<RtlGetLastError_Delegate>(nameof(RtlGetLastError));
            return method.Invoke();
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        delegate void LhUninstallAllHooks_Delegate();
        public static void LhUninstallAllHooks()
        {
            var method = GetDelegate<LhUninstallAllHooks_Delegate>(nameof(LhUninstallAllHooks));
            method.Invoke();
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        delegate Int32 LhInstallHook_Delegate(
            IntPtr InEntryPoint,
            IntPtr InHookProc,
            IntPtr InCallback,
            IntPtr OutHandle);
        public static Int32 LhInstallHook(
            IntPtr InEntryPoint,
            IntPtr InHookProc,
            IntPtr InCallback,
            IntPtr OutHandle)
        {
            var method = GetDelegate<LhInstallHook_Delegate>(nameof(LhInstallHook));
            return method.Invoke(InEntryPoint, InHookProc, InCallback, OutHandle);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        delegate Int32 LhUninstallHook_Delegate(IntPtr RefHandle);
        public static Int32 LhUninstallHook(IntPtr RefHandle)
        {
            var method = GetDelegate<LhUninstallHook_Delegate>(nameof(LhUninstallHook));
            return method.Invoke(RefHandle);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        delegate Int32 LhWaitForPendingRemovals_Delegate();
        public static Int32 LhWaitForPendingRemovals()
        {
            var method = GetDelegate<LhWaitForPendingRemovals_Delegate>(nameof(LhWaitForPendingRemovals));
            return method.Invoke();
        }


        /*
            Setup the ACLs after hook installation. Please note that every
            hook starts suspended. You will have to set a proper ACL to
            make it active!
        */
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        delegate Int32 LhSetInclusiveACL_Delegate(
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex=1)]
            Int32[] InThreadIdList,
            Int32 InThreadCount,
            IntPtr InHandle);
        public static Int32 LhSetInclusiveACL(
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex=1)]
            Int32[] InThreadIdList,
            Int32 InThreadCount,
            IntPtr InHandle)
        {
            var method = GetDelegate<LhSetInclusiveACL_Delegate>(nameof(LhSetInclusiveACL));
            return method.Invoke(
                InThreadIdList,
                InThreadCount,
                InHandle);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        delegate Int32 LhSetExclusiveACL_Delegate(
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex=1)]
            Int32[] InThreadIdList,
            Int32 InThreadCount,
            IntPtr InHandle);
        public static Int32 LhSetExclusiveACL(
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex=1)]
            Int32[] InThreadIdList,
            Int32 InThreadCount,
            IntPtr InHandle)
        {
            var method = GetDelegate<LhSetExclusiveACL_Delegate>(nameof(LhSetExclusiveACL));
            return method.Invoke(
                InThreadIdList,
                InThreadCount,
                InHandle);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        delegate Int32 LhSetGlobalInclusiveACL_Delegate(
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
            Int32[] InThreadIdList,
            Int32 InThreadCount);
        public static Int32 LhSetGlobalInclusiveACL(
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
            Int32[] InThreadIdList,
            Int32 InThreadCount)
        {
            var method = GetDelegate<LhSetGlobalInclusiveACL_Delegate>(nameof(LhSetGlobalInclusiveACL));
            return method.Invoke(
                InThreadIdList,
                InThreadCount);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        delegate Int32 LhSetGlobalExclusiveACL_Delegate(
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex=1)]
            Int32[] InThreadIdList,
            Int32 InThreadCount);
        public static Int32 LhSetGlobalExclusiveACL(
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex=1)]
            Int32[] InThreadIdList,
            Int32 InThreadCount)
        {
            var method = GetDelegate<LhSetGlobalExclusiveACL_Delegate>(nameof(LhSetGlobalExclusiveACL));
            return method.Invoke(
                InThreadIdList,
                InThreadCount);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        delegate Int32 LhIsThreadIntercepted_Delegate(
            IntPtr InHandle,
            Int32 InThreadID,
            out Boolean OutResult);
        public static Int32 LhIsThreadIntercepted(
            IntPtr InHandle,
            Int32 InThreadID,
            out Boolean OutResult)
        {
            var method = GetDelegate<LhIsThreadIntercepted_Delegate>(nameof(LhIsThreadIntercepted));
            return method.Invoke(
                InHandle,
                InThreadID,
                out OutResult);
        }

        /*
            The following barrier methods are meant to be used in hook handlers only!

            They will all fail with STATUS_NOT_SUPPORTED if called outside a
            valid hook handler...
        */
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        delegate Int32 LhBarrierGetCallback_Delegate(out IntPtr OutValue);
        public static Int32 LhBarrierGetCallback(out IntPtr OutValue)
        {
            var method = GetDelegate<LhBarrierGetCallback_Delegate>(nameof(LhBarrierGetCallback));
            return method.Invoke(out OutValue);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        delegate Int32 LhBarrierGetReturnAddress_Delegate(out IntPtr OutValue);
        public static Int32 LhBarrierGetReturnAddress(out IntPtr OutValue)
        {
            var method = GetDelegate<LhBarrierGetReturnAddress_Delegate>(nameof(LhBarrierGetReturnAddress));
            return method.Invoke(out OutValue);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        delegate Int32 LhBarrierGetAddressOfReturnAddress_Delegate(out IntPtr OutValue);
        public static Int32 LhBarrierGetAddressOfReturnAddress(out IntPtr OutValue)
        {
            var method = GetDelegate<LhBarrierGetAddressOfReturnAddress_Delegate>(nameof(LhBarrierGetAddressOfReturnAddress));
            return method.Invoke(out OutValue);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        delegate Int32 LhBarrierBeginStackTrace_Delegate(out IntPtr OutBackup);
        public static Int32 LhBarrierBeginStackTrace(out IntPtr OutBackup)
        {
            var method = GetDelegate<LhBarrierBeginStackTrace_Delegate>(nameof(LhBarrierBeginStackTrace));
            return method.Invoke(out OutBackup);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        delegate Int32 LhBarrierEndStackTrace_Delegate(IntPtr OutBackup);
        public static Int32 LhBarrierEndStackTrace(IntPtr OutBackup)
        {
            var method = GetDelegate<LhBarrierEndStackTrace_Delegate>(nameof(LhBarrierEndStackTrace));
            return method.Invoke(OutBackup);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        delegate Int32 LhBarrierGetCallingModule_Delegate(out IntPtr OutValue);
        public static Int32 LhBarrierGetCallingModule(out IntPtr OutValue)
        {
            var method = GetDelegate<LhBarrierGetCallingModule_Delegate>(nameof(LhBarrierGetCallingModule));
            return method.Invoke(out OutValue);
        }

        /*
            Debug helper API.
        */
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        delegate Int32 DbgAttachDebugger_Delegate();
        public static Int32 DbgAttachDebugger()
        {
            var method = GetDelegate<DbgAttachDebugger_Delegate>(nameof(DbgAttachDebugger));
            return method.Invoke();
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        delegate Int32 DbgGetThreadIdByHandle_Delegate(
            IntPtr InThreadHandle,
            out Int32 OutThreadId);
        public static Int32 DbgGetThreadIdByHandle(
            IntPtr InThreadHandle,
            out Int32 OutThreadId)
        {
            var method = GetDelegate<DbgGetThreadIdByHandle_Delegate>(nameof(DbgGetThreadIdByHandle));
            return method.Invoke(
                InThreadHandle,
                out OutThreadId);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        delegate Int32 DbgGetProcessIdByHandle_Delegate(
            IntPtr InProcessHandle,
            out Int32 OutProcessId);
        public static Int32 DbgGetProcessIdByHandle(
            IntPtr InProcessHandle,
            out Int32 OutProcessId)
        {
            var method = GetDelegate<DbgGetProcessIdByHandle_Delegate>(nameof(DbgGetProcessIdByHandle));
            return method.Invoke(
                InProcessHandle,
                out OutProcessId);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        delegate Int32 DbgHandleToObjectName_Delegate(
            IntPtr InNamedHandle,
            IntPtr OutNameBuffer,
            Int32 InBufferSize,
            out Int32 OutRequiredSize);
        public static Int32 DbgHandleToObjectName(
            IntPtr InNamedHandle,
            IntPtr OutNameBuffer,
            Int32 InBufferSize,
            out Int32 OutRequiredSize)
        {
            var method = GetDelegate<DbgHandleToObjectName_Delegate>(nameof(DbgHandleToObjectName));
            return method.Invoke(
                InNamedHandle,
                OutNameBuffer,
                InBufferSize,
                out OutRequiredSize);
        }

        /*
            Injection support API.
        */
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        delegate Int32 RhInjectLibrary_Delegate(
            Int32 InTargetPID,
            Int32 InWakeUpTID,
            Int32 InInjectionOptions,
            String InLibraryPath_x86,
            String InLibraryPath_x64,
            IntPtr InPassThruBuffer,
            Int32 InPassThruSize);
        public static Int32 RhInjectLibrary(
            Int32 InTargetPID,
            Int32 InWakeUpTID,
            Int32 InInjectionOptions,
            String InLibraryPath_x86,
            String InLibraryPath_x64,
            IntPtr InPassThruBuffer,
            Int32 InPassThruSize)
        {
            var method = GetDelegate<RhInjectLibrary_Delegate>(nameof(RhInjectLibrary));
            return method.Invoke(
                InTargetPID,
                InWakeUpTID,
                InInjectionOptions,
                InLibraryPath_x86,
                InLibraryPath_x64,
                InPassThruBuffer,
                InPassThruSize);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        delegate Int32 RhIsX64Process_Delegate(
            Int32 InProcessId,
            out Boolean OutResult);
        public static Int32 RhIsX64Process(
            Int32 InProcessId,
            out Boolean OutResult)
        {
            var method = GetDelegate<RhIsX64Process_Delegate>(nameof(RhIsX64Process));
            return method.Invoke(
                InProcessId,
                out OutResult);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        delegate Boolean RhIsAdministrator_Delegate();
        public static Boolean RhIsAdministrator()
        {
            var method = GetDelegate<RhIsAdministrator_Delegate>(nameof(RhIsAdministrator));
            return method.Invoke();
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        delegate Int32 RhGetProcessToken_Delegate(Int32 InProcessId, out IntPtr OutToken);
        public static Int32 RhGetProcessToken(Int32 InProcessId, out IntPtr OutToken)
        {
            var method = GetDelegate<RhGetProcessToken_Delegate>(nameof(RhGetProcessToken));
            return method.Invoke(
                InProcessId,
                out OutToken);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        delegate Int32 RtlInstallService_Delegate(
            String InServiceName,
            String InExePath,
            String InChannelName);
        public static Int32 RtlInstallService(
            String InServiceName,
            String InExePath,
            String InChannelName)
        {
            var method = GetDelegate<RtlInstallService_Delegate>(nameof(RtlInstallService));
            return method.Invoke(
                InServiceName,
                InExePath,
                InChannelName);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        delegate Int32 RhWakeUpProcess_Delegate();
        public static Int32 RhWakeUpProcess()
        {
            var method = GetDelegate<RhWakeUpProcess_Delegate>(nameof(RhWakeUpProcess));
            return method.Invoke();
        }


        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        delegate Int32 RtlCreateSuspendedProcess_Delegate(
            String InEXEPath,
            String InCommandLine,
            Int32 InProcessCreationFlags,
            out Int32 OutProcessId,
            out Int32 OutThreadId);
        public static Int32 RtlCreateSuspendedProcess(
            String InEXEPath,
            String InCommandLine,
            Int32 InProcessCreationFlags,
            out Int32 OutProcessId,
            out Int32 OutThreadId)
        {
            var method = GetDelegate<RtlCreateSuspendedProcess_Delegate>(nameof(RtlCreateSuspendedProcess));
            return method.Invoke(
                InEXEPath,
                InCommandLine,
                InProcessCreationFlags,
                out OutProcessId,
                out OutThreadId);
        }


        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        delegate Int32 RhInstallDriver_Delegate(
            String InDriverPath,
            String InDriverName);
        public static Int32 RhInstallDriver(
            String InDriverPath,
            String InDriverName)
        {
            var method = GetDelegate<RhInstallDriver_Delegate>(nameof(RhInstallDriver));
            return method.Invoke(
                InDriverPath,
                InDriverName);
        }


        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        delegate Int32 RhInstallSupportDriver_Delegate();
        public static Int32 RhInstallSupportDriver()
        {
            var method = GetDelegate<RhInstallSupportDriver_Delegate>(nameof(RhInstallSupportDriver));
            return method.Invoke();
        }


        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        delegate Boolean RhIsX64System_Delegate();
        public static Boolean RhIsX64System()
        {
            var method = GetDelegate<RhIsX64System_Delegate>(nameof(RhIsX64System));
            return method.Invoke();
        }


        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        delegate IntPtr GacCreateContext_Delegate();
        public static IntPtr GacCreateContext()
        {
            var method = GetDelegate<GacCreateContext_Delegate>(nameof(GacCreateContext));
            return method.Invoke();
        }


        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        delegate void GacReleaseContext_Delegate(ref IntPtr RefContext);
        public static void GacReleaseContext(ref IntPtr RefContext)
        {
            var method = GetDelegate<GacReleaseContext_Delegate>(nameof(GacReleaseContext));
            method.Invoke(ref RefContext);
        }


        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        delegate bool GacInstallAssembly_Delegate(
            IntPtr InContext,
            String InAssemblyPath,
            String InDescription,
            String InUniqueID);
        public static bool GacInstallAssembly(
            IntPtr InContext,
            String InAssemblyPath,
            String InDescription,
            String InUniqueID)
        {
            var method = GetDelegate<GacInstallAssembly_Delegate>(nameof(GacInstallAssembly));
            return method.Invoke(
                InContext,
                InAssemblyPath,
                InDescription,
                InUniqueID);
        }


        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        delegate bool GacUninstallAssembly_Delegate(
            IntPtr InContext,
            String InAssemblyName,
            String InDescription,
            String InUniqueID);
        public static bool GacUninstallAssembly(
            IntPtr InContext,
            String InAssemblyName,
            String InDescription,
            String InUniqueID)
        {
            var method = GetDelegate<GacUninstallAssembly_Delegate>(nameof(GacUninstallAssembly));
            return method.Invoke(
                InContext,
                InAssemblyName,
                InDescription,
                InUniqueID);
        }


        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        delegate int LhGetHookBypassAddress_Delegate(IntPtr handle, out IntPtr address);
        public static int LhGetHookBypassAddress(IntPtr handle, out IntPtr address)
        {
            var method = GetDelegate<LhGetHookBypassAddress_Delegate>(nameof(LhGetHookBypassAddress));
            return method.Invoke(handle, out address);
        }
    }

    public static class NativeAPI
    {
        public const Int32 MAX_HOOK_COUNT = 1024;
        public const Int32 MAX_ACE_COUNT = 128;
        public readonly static Boolean Is64Bit = IntPtr.Size == 8;

        [DllImport("kernel32.dll")]
        public static extern int GetCurrentThreadId();

        [DllImport("kernel32.dll")]
        public static extern void CloseHandle(IntPtr InHandle);

        [DllImport("kernel32.dll")]
        public static extern int GetCurrentProcessId();

        [DllImport("kernel32.dll", CharSet=CharSet.Ansi)]
        public static extern IntPtr GetProcAddress(IntPtr InModule, String InProcName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr LoadLibrary(String InPath);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr GetModuleHandle(String InPath);

        [DllImport("kernel32.dll")]
        public static extern Int16 RtlCaptureStackBackTrace(
            Int32 InFramesToSkip,
            Int32 InFramesToCapture,
            IntPtr OutBackTrace,
            IntPtr OutBackTraceHash);

        public const Int32 STATUS_SUCCESS = unchecked((Int32)0);
        public const Int32 STATUS_INVALID_PARAMETER = unchecked((Int32)0xC000000DL);
        public const Int32 STATUS_INVALID_PARAMETER_1= unchecked((Int32)0xC00000EFL);
        public const Int32 STATUS_INVALID_PARAMETER_2= unchecked((Int32)0xC00000F0L);
        public const Int32 STATUS_INVALID_PARAMETER_3= unchecked((Int32)0xC00000F1L);
        public const Int32 STATUS_INVALID_PARAMETER_4= unchecked((Int32)0xC00000F2L);
        public const Int32 STATUS_INVALID_PARAMETER_5= unchecked((Int32)0xC00000F3L);
        public const Int32 STATUS_NOT_SUPPORTED= unchecked((Int32)0xC00000BBL);

        public const Int32 STATUS_INTERNAL_ERROR= unchecked((Int32)0xC00000E5L);
        public const Int32 STATUS_INSUFFICIENT_RESOURCES= unchecked((Int32)0xC000009AL);
        public const Int32 STATUS_BUFFER_TOO_SMALL= unchecked((Int32)0xC0000023L);
        public const Int32 STATUS_NO_MEMORY= unchecked((Int32)0xC0000017L);
        public const Int32 STATUS_WOW_ASSERTION = unchecked((Int32)0xC0009898L);
        public const Int32 STATUS_ACCESS_DENIED = unchecked((Int32)0xC0000022L);

        private static String ComposeString()
        {
            return String.Format("{0} (Code: {1})", RtlGetLastErrorString(), RtlGetLastError());
        }

        internal static void Force(Int32 InErrorCode)
        {
            switch (InErrorCode)
            {
                case STATUS_SUCCESS: return;
                case STATUS_INVALID_PARAMETER: throw new ArgumentException("STATUS_INVALID_PARAMETER: " + ComposeString());
                case STATUS_INVALID_PARAMETER_1: throw new ArgumentException("STATUS_INVALID_PARAMETER_1: " + ComposeString());
                case STATUS_INVALID_PARAMETER_2: throw new ArgumentException("STATUS_INVALID_PARAMETER_2: " + ComposeString());
                case STATUS_INVALID_PARAMETER_3: throw new ArgumentException("STATUS_INVALID_PARAMETER_3: " + ComposeString());
                case STATUS_INVALID_PARAMETER_4: throw new ArgumentException("STATUS_INVALID_PARAMETER_4: " + ComposeString());
                case STATUS_INVALID_PARAMETER_5: throw new ArgumentException("STATUS_INVALID_PARAMETER_5: " + ComposeString());
                case STATUS_NOT_SUPPORTED: throw new NotSupportedException("STATUS_NOT_SUPPORTED: " + ComposeString());
                case STATUS_INTERNAL_ERROR: throw new ApplicationException("STATUS_INTERNAL_ERROR: " + ComposeString());
                case STATUS_INSUFFICIENT_RESOURCES: throw new InsufficientMemoryException("STATUS_INSUFFICIENT_RESOURCES: " + ComposeString());
                case STATUS_BUFFER_TOO_SMALL: throw new ArgumentException("STATUS_BUFFER_TOO_SMALL: " + ComposeString());
                case STATUS_NO_MEMORY: throw new OutOfMemoryException("STATUS_NO_MEMORY: " + ComposeString());
                case STATUS_WOW_ASSERTION: throw new OutOfMemoryException("STATUS_WOW_ASSERTION: " + ComposeString());
                case STATUS_ACCESS_DENIED: throw new AccessViolationException("STATUS_ACCESS_DENIED: " + ComposeString());

                default: throw new ApplicationException("Unknown error code (" + InErrorCode + "): " + ComposeString());
            }
        }

        public static Int32 RtlGetLastError()
        {
            return NativeAPI_EasyHook.RtlGetLastError();
        }

        public static String RtlGetLastErrorString()
        {
            return NativeAPI_EasyHook.RtlGetLastErrorStringCopy();
        }

        public static void LhUninstallAllHooks()
        {
            NativeAPI_EasyHook.LhUninstallAllHooks();
        }

        public static void LhInstallHook(
            IntPtr InEntryPoint,
            IntPtr InHookProc,
            IntPtr InCallback,
            IntPtr OutHandle)
        {
            Force( NativeAPI_EasyHook.LhInstallHook(InEntryPoint, InHookProc, InCallback, OutHandle));
        }

        public static void LhUninstallHook(IntPtr RefHandle)
        {
            Force( NativeAPI_EasyHook.LhUninstallHook(RefHandle));
        }

        public static void LhWaitForPendingRemovals()
        {
            Force( NativeAPI_EasyHook.LhWaitForPendingRemovals());
        }

        public static void LhIsThreadIntercepted(
                    IntPtr InHandle,
                    Int32 InThreadID,
                    out Boolean OutResult)
        {
            Force(NativeAPI_EasyHook.LhIsThreadIntercepted(InHandle, InThreadID, out OutResult));
        }

        public static void LhSetInclusiveACL(
                    Int32[] InThreadIdList,
                    Int32 InThreadCount,
                    IntPtr InHandle)
        {
            Force( NativeAPI_EasyHook.LhSetInclusiveACL(InThreadIdList, InThreadCount, InHandle));
        }

        public static void LhSetExclusiveACL(
                    Int32[] InThreadIdList,
                    Int32 InThreadCount,
                    IntPtr InHandle)
        {
            Force( NativeAPI_EasyHook.LhSetExclusiveACL(InThreadIdList, InThreadCount, InHandle));
        }

        public static void LhSetGlobalInclusiveACL(
                    Int32[] InThreadIdList,
                    Int32 InThreadCount)
        {
            Force( NativeAPI_EasyHook.LhSetGlobalInclusiveACL(InThreadIdList, InThreadCount));
        }

        public static void LhSetGlobalExclusiveACL(
                    Int32[] InThreadIdList,
                    Int32 InThreadCount)
        {
            Force( NativeAPI_EasyHook.LhSetGlobalExclusiveACL(InThreadIdList, InThreadCount));
        }

        public static void LhBarrierGetCallingModule(out IntPtr OutValue)
        {
            Force(NativeAPI_EasyHook.LhBarrierGetCallingModule(out OutValue));
        }

        public static void LhBarrierGetCallback(out IntPtr OutValue)
        {
            Force( NativeAPI_EasyHook.LhBarrierGetCallback(out OutValue));
        }

        public static void LhBarrierGetReturnAddress(out IntPtr OutValue)
        {
            Force( NativeAPI_EasyHook.LhBarrierGetReturnAddress(out OutValue));
        }

        public static void LhBarrierGetAddressOfReturnAddress(out IntPtr OutValue)
        {
            Force( NativeAPI_EasyHook.LhBarrierGetAddressOfReturnAddress(out OutValue));
        }

        public static void LhBarrierBeginStackTrace(out IntPtr OutBackup)
        {
            Force( NativeAPI_EasyHook.LhBarrierBeginStackTrace(out OutBackup));
        }

        public static void LhBarrierEndStackTrace(IntPtr OutBackup)
        {
            Force( NativeAPI_EasyHook.LhBarrierEndStackTrace(OutBackup));
        }

        public static void LhGetHookBypassAddress(IntPtr handle, out IntPtr address)
        {
            Force(NativeAPI_EasyHook.LhGetHookBypassAddress(handle, out address));
        }

        public static void DbgAttachDebugger()
        {
            Force( NativeAPI_EasyHook.DbgAttachDebugger());
        }

        public static void DbgGetThreadIdByHandle(
            IntPtr InThreadHandle,
            out Int32 OutThreadId)
        {
            Force( NativeAPI_EasyHook.DbgGetThreadIdByHandle(InThreadHandle, out OutThreadId));
        }

        public static void DbgGetProcessIdByHandle(
            IntPtr InProcessHandle,
            out Int32 OutProcessId)
        {
            Force( NativeAPI_EasyHook.DbgGetProcessIdByHandle(InProcessHandle, out OutProcessId));
        }

        public static void DbgHandleToObjectName(
            IntPtr InNamedHandle,
            IntPtr OutNameBuffer,
            Int32 InBufferSize,
            out Int32 OutRequiredSize)
        {
            Force( NativeAPI_EasyHook.DbgHandleToObjectName(InNamedHandle, OutNameBuffer, InBufferSize, out OutRequiredSize));
        }

        public static Int32 EASYHOOK_INJECT_DEFAULT   =      0x00000000;
        public static Int32 EASYHOOK_INJECT_MANAGED = 0x00000001;

        public static Int32 RhInjectLibraryEx(
            Int32 InTargetPID,
            Int32 InWakeUpTID,
            Int32 InInjectionOptions,
            String InLibraryPath_x86,
            String InLibraryPath_x64,
            IntPtr InPassThruBuffer,
            Int32 InPassThruSize)
        {
            return NativeAPI_EasyHook.RhInjectLibrary(InTargetPID, InWakeUpTID, InInjectionOptions,
                InLibraryPath_x86, InLibraryPath_x64, InPassThruBuffer, InPassThruSize);
        }

        public static void RhInjectLibrary(
            Int32 InTargetPID,
            Int32 InWakeUpTID,
            Int32 InInjectionOptions,
            String InLibraryPath_x86,
            String InLibraryPath_x64,
            IntPtr InPassThruBuffer,
            Int32 InPassThruSize)
        {
            Force( NativeAPI_EasyHook.RhInjectLibrary(InTargetPID, InWakeUpTID, InInjectionOptions,
                InLibraryPath_x86, InLibraryPath_x64, InPassThruBuffer, InPassThruSize));
        }

        public static void RtlCreateSuspendedProcess(
           String InEXEPath,
           String InCommandLine,
            Int32 InProcessCreationFlags,
           out Int32 OutProcessId,
           out Int32 OutThreadId)
        {
            Force(NativeAPI_EasyHook.RtlCreateSuspendedProcess(InEXEPath, InCommandLine, InProcessCreationFlags,
                out OutProcessId, out OutThreadId));
        }

        public static void RhIsX64Process(
            Int32 InProcessId,
            out Boolean OutResult)
        {
            Force( NativeAPI_EasyHook.RhIsX64Process(InProcessId, out OutResult));
        }

        public static Boolean RhIsAdministrator()
        {
            return NativeAPI_EasyHook.RhIsAdministrator();
        }

        public static void RhGetProcessToken(Int32 InProcessId, out IntPtr OutToken)
        {
            Force(NativeAPI_EasyHook.RhGetProcessToken(InProcessId, out OutToken));
        }

        public static void RhWakeUpProcess()
        {
            Force(NativeAPI_EasyHook.RhWakeUpProcess());
        }

        public static void RtlInstallService(
            String InServiceName,
            String InExePath,
            String InChannelName)
        {
            Force(NativeAPI_EasyHook.RtlInstallService(InServiceName, InExePath, InChannelName));
        }

        public static void RhInstallDriver(
           String InDriverPath,
           String InDriverName)
        {
            Force(NativeAPI_EasyHook.RhInstallDriver(InDriverPath, InDriverName));
        }
        
        public static void RhInstallSupportDriver()
        {
            Force(NativeAPI_EasyHook.RhInstallSupportDriver());
        }

        public static Boolean RhIsX64System()
        {
            return NativeAPI_EasyHook.RhIsX64System();
        }

        public static void GacInstallAssemblies(
            String[] InAssemblyPaths,
            String InDescription,
            String InUniqueID)
        {
            try
            {
                System.GACManagedAccess.AssemblyCache.InstallAssemblies(
                    InAssemblyPaths,
                    new System.GACManagedAccess.InstallReference(System.GACManagedAccess.InstallReferenceGuid.OpaqueGuid, InUniqueID, InDescription),
                    System.GACManagedAccess.AssemblyCommitFlags.Force);
            }
            catch (Exception e)
            {
                throw new ApplicationException("Unable to install assemblies to GAC, see inner exception for details", e);
            }
        }

        public static void GacUninstallAssemblies(
            String[] InAssemblyNames,
            String InDescription,
            String InUniqueID)
        {
            try
            {
                System.GACManagedAccess.AssemblyCacheUninstallDisposition[] results;
                System.GACManagedAccess.AssemblyCache.UninstallAssemblies(
                    InAssemblyNames,
                    new System.GACManagedAccess.InstallReference(System.GACManagedAccess.InstallReferenceGuid.OpaqueGuid, InUniqueID, InDescription),
                    out results);

// disable warnings Obsolete and Obsolete("message")
#pragma warning disable 612, 618
                for (var i = 0; i < InAssemblyNames.Length; i++)
                    Config.PrintComment("GacUninstallAssembly: Assembly {0}, uninstall result {1}", InAssemblyNames[i], results[i]);
// enable warnings for Obsolete and Obsolete("message")
#pragma warning restore 612, 618
            }
            catch (Exception e)
            {
                throw new ApplicationException("Unable to uninstall assemblies from GAC, see inner exception for details", e);
            }
        }
    }
}
