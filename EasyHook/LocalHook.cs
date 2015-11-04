// EasyHook (File: EasyHook\LocalHook.cs)
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
using System.Threading;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Runtime.ConstrainedExecution;

namespace EasyHook
{
    /// <summary>
    /// Provides a managed interface to the native thread ACLs.
    /// </summary>
    /// <remarks>
    /// Refer to the official guide to learn more about why thread ACLs are useful. 
    /// They can be used to exclude/include dedicated threads from interception or to dynamically
    /// apply different kind of hooks to different threads. Even if you could do this
    /// in managed code, it is not that easy to implement and also EasyHook evaluates
    /// those ACLs in unmanaged code. So if any thread is not intercepted, it will never
    /// enter the manged environment what will speed up things about orders of magnitudes.
    /// </remarks>
    public class HookAccessControl
    {
        private Int32[] m_ACL = new Int32[0];
        private IntPtr m_Handle;
        private Boolean m_IsExclusive;

        /// <summary>
        /// Is this ACL an exclusive one? Refer to <see cref="SetExclusiveACL"/> for more information.
        /// </summary>
        public Boolean IsExclusive { get { return m_IsExclusive; } }
        /// <summary>
        /// Is this ACL an inclusive one? Refer to <see cref="SetInclusiveACL"/> for more information.
        /// </summary>
        public Boolean IsInclusive { get { return !IsExclusive; } }

        /// <summary>
        /// Sets an inclusive ACL. This means all threads that are enumerated through <paramref name="InACL"/>
        /// are intercepted while all others are NOT. Of course this will overwrite the existing ACL.
        /// </summary>
        /// <remarks>
        /// Please note that this is not necessarily the final
        /// negotiation result. Refer to <see cref="LocalHook.IsThreadIntercepted"/> for more information.
        /// In general inclusive ACLs will restrict exclusive ACLs while local ACLs will overwrite the
        /// global ACL.
        /// </remarks>
        /// <param name="InACL">Threads to be explicitly included in negotiation.</param>
        /// <exception cref="ArgumentException">
        /// The limit of 128 access entries is exceeded!
        /// </exception>
        public void SetInclusiveACL(Int32[] InACL)
        {
            if (InACL == null)
                m_ACL = new Int32[0];
            else
                m_ACL = (Int32[])InACL.Clone();

            m_IsExclusive = false;

            if (m_Handle == IntPtr.Zero)
                NativeAPI.LhSetGlobalInclusiveACL(m_ACL, m_ACL.Length);
            else
                NativeAPI.LhSetInclusiveACL(m_ACL, m_ACL.Length, m_Handle);
        }

        /// <summary>
        /// Sets an exclusive ACL. This means all threads that are enumerated through <paramref name="InACL"/>
        /// are NOT intercepted while all others are. Of course this will overwrite the existing ACL.
        /// </summary>
        /// <remarks>
        /// Please note that this is not necessarily the final
        /// negotiation result. Refer to <see cref="LocalHook.IsThreadIntercepted"/> for more information.
        /// In general inclusive ACLs will restrict exclusive ACLs while local ACLs will overwrite the
        /// global ACL.
        /// </remarks>
        /// <param name="InACL">Threads to be explicitly included in negotiation.</param>
        /// <exception cref="ArgumentException">
        /// The limit of 128 access entries is exceeded!
        /// </exception>
        public void SetExclusiveACL(Int32[] InACL)
        {
            if (InACL == null)
                m_ACL = new Int32[0];
            else
                m_ACL = (Int32[])InACL.Clone();

            m_IsExclusive = true;

            if (m_Handle == IntPtr.Zero)
                NativeAPI.LhSetGlobalExclusiveACL(m_ACL, m_ACL.Length);
            else
                NativeAPI.LhSetExclusiveACL(m_ACL, m_ACL.Length, m_Handle);
        }

        /// <summary>
        /// Creates a copy of the internal thread list associated with this ACL. You may freely
        /// modify it without affecting the internal entries.
        /// </summary>
        /// <returns>
        /// A copy of the internal thread entries.
        /// </returns>
        public Int32[] GetEntries()
        {
            return (Int32[])m_ACL.Clone();
        }

        internal HookAccessControl(IntPtr InHandle)
        {
            if (InHandle == IntPtr.Zero)
                m_IsExclusive = true;
            else
                m_IsExclusive = false;

            m_Handle = InHandle;
        }
    }

    /// <summary>
    /// This class is intended to be used within hook handlers,
    /// to access associated runtime information.
    /// </summary>
    /// <remarks>
    /// Other hooking libraries on the market require that you keep track of
    /// such information yourself, what can be a burden.
    /// </remarks>
    public class HookRuntimeInfo
    {
        private static ProcessModule[] ModuleArray = new ProcessModule[0];
        private static Int64 LastUpdate = 0;

        /// <summary>
        ///	Is the current thread within a valid hook handler? This is only the case
        ///	if your handler was called through the hooked entry point...
        ///	Executes in max. one micro secound.
        /// </summary>
        public static Boolean IsHandlerContext
        {
            get
            {
                IntPtr Callback;

                if (NativeAPI.Is64Bit)
                    return NativeAPI_x64.LhBarrierGetCallback(out Callback) == NativeAPI.STATUS_SUCCESS;
                else
                    return NativeAPI_x86.LhBarrierGetCallback(out Callback) == NativeAPI.STATUS_SUCCESS;
            }
        }

        ///	<summary>
        ///	The user callback initially passed to either <see cref="LocalHook.Create"/> or <see cref="LocalHook.CreateUnmanaged"/>.
        /// Executes in max. one micro secound.
        ///	</summary>
        ///	<exception cref="NotSupportedException"> The current thread is not within a valid hook handler. </exception>
        public static Object Callback 
        { 
            get 
            {
                return Handle.Callback;
            } 
        }

        ///	<summary>
        ///	The hook handle initially returned by either <see cref="LocalHook.Create"/> or <see cref="LocalHook.CreateUnmanaged"/>.
        /// Executes in max. one micro secound.
        ///	</summary>
        ///	<exception cref="NotSupportedException"> The current thread is not within a valid hook handler. </exception>
        public static LocalHook Handle
        {
            get
            {
                IntPtr Callback;

                NativeAPI.LhBarrierGetCallback(out Callback);

                if (Callback == IntPtr.Zero)
                    return null;

                return (LocalHook)GCHandle.FromIntPtr(Callback).Target;
            }
        }

        /// <summary>
        /// Allows you to explicitly update the unmanaged module list which is required for
        /// <see cref="CallingUnmanagedModule"/>, <see cref="UnmanagedStackTrace"/> and <see cref="PointerToModule"/>. 
        /// Normally this is not necessary, but if you hook a process that frequently loads/unloads modules, you
        /// may call this method in a <c>LoadLibrary</c> hook to always operate on the latest module list.
        /// </summary>
        public static void UpdateUnmanagedModuleList()
        {
            List<ProcessModule> ModList = new List<ProcessModule>();

            foreach (ProcessModule Module in Process.GetCurrentProcess().Modules)
            {
                ModList.Add(Module);
            }

            ModuleArray = ModList.ToArray();

            LastUpdate = DateTime.Now.Ticks;
        }

        /// <summary>
        /// Retrives the unmanaged module that contains the given pointer. If no module can be
        /// found, <c>null</c> is returned. This method will automatically update the unmanaged
        /// module list from time to time.
        /// Executes in less than one micro secound.
        /// </summary>
        /// <param name="InPointer"></param>
        /// <returns></returns>
        public static ProcessModule PointerToModule(IntPtr InPointer)
        {
            Int64 Pointer = InPointer.ToInt64();

            if ((Pointer == 0) || (Pointer == ~0))
                return null;

        TRY_AGAIN:
            for (int i = 0; i < ModuleArray.Length; i++)
            {
                if ((Pointer >= ModuleArray[i].BaseAddress.ToInt64()) &&
                    (Pointer <= ModuleArray[i].BaseAddress.ToInt64() + ModuleArray[i].ModuleMemorySize))
                    return ModuleArray[i];
            }

            if ((DateTime.Now.Ticks - LastUpdate) > 1000 * 1000 * 10 /* 1000 ms*/)
            {
                UpdateUnmanagedModuleList();

                goto TRY_AGAIN;
            }

            return null;
        }

        /// <summary>
        /// Determines the first unmanaged module on the current call stack. This is always the module
        /// that invoked the hook. 
        /// Executes in max. 15 micro secounds.
        /// </summary>
        /// <remarks>
        /// The problem is that if the calling module is a NET assembly
        /// and invokes the hook through a P-Invoke binding, you will get
        /// "mscorwks.dll" as calling module and not the NET assembly. This is only an example 
        /// but I think you got the idea. To solve this issue, refer to <see cref="UnmanagedStackTrace"/>
        /// and <see cref="ManagedStackTrace"/>!
        /// </remarks>
        public static ProcessModule CallingUnmanagedModule
        {
            get
            {
                return PointerToModule(ReturnAddress);
            }
        }

        /// <summary>
        /// Determines the first managed module on the current call stack. This is always the module
        /// that invoked the hook. 
        /// Executes in max. 40 micro secounds.
        /// </summary>
        /// <remarks>
        /// Imagine your hook targets CreateFile. A NET assembly will now invoke this hook through
        /// FileStream, for example. But because System.IO.FileStream invokes the hook, you will
        /// get "System.Core" as calling module and not the desired assembly.
        /// To solve this issue, refer to <see cref="UnmanagedStackTrace"/>
        /// and <see cref="ManagedStackTrace"/>!
        /// </remarks>
        public static System.Reflection.Assembly CallingManagedModule
        {
            get
            {
                IntPtr Backup;

                NativeAPI.LhBarrierBeginStackTrace(out Backup);

                try
                {
                    return System.Reflection.Assembly.GetCallingAssembly();
                }
                finally
                {
                    NativeAPI.LhBarrierEndStackTrace(Backup);
                }
            }
        }

        /// <summary>
        /// Returns the address where execution is continued, after you hook has
        /// been completed. This is always the instruction behind the hook invokation.
        /// Executes in max. one micro secound.
        /// </summary>
        public static IntPtr ReturnAddress
        {
            get
            {
                IntPtr RetAddr;

                NativeAPI.LhBarrierGetReturnAddress(out RetAddr);

                return RetAddr;
            }
        }

        /// <summary>
        /// A stack address pointing to <see cref="ReturnAddress"/>.
        /// Executes in max. one micro secound.
        /// </summary>
        public static IntPtr AddressOfReturnAddress
        {
            get
            {
                IntPtr AddrOfRetAddr;

                NativeAPI.LhBarrierGetAddressOfReturnAddress(out AddrOfRetAddr);

                return AddrOfRetAddr;
            }
        }

        private class StackTraceBuffer : CriticalFinalizerObject
        {
            public IntPtr Unmanaged;
            public IntPtr[] Managed;
            public ProcessModule[] Modules;

            public StackTraceBuffer()
            {
                if ((Unmanaged = Marshal.AllocCoTaskMem(64 * IntPtr.Size)) == IntPtr.Zero)
                    throw new OutOfMemoryException();

                Managed = new IntPtr[64];
                Modules = new ProcessModule[64];
            }

            public void Synchronize(Int32 InCount)
            {
                Marshal.Copy(Unmanaged, Managed, 0, Math.Min(64, InCount));
            }

            ~StackTraceBuffer()
            {
                if (Unmanaged != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(Unmanaged);

                Unmanaged = IntPtr.Zero;
            }
        }

        [ThreadStatic]
        private static StackTraceBuffer StackBuffer = null;

        /// <summary>
        /// Creates a call stack trace of the unmanaged code path that finally
        /// lead to your hook. To detect whether the desired module is within the
        /// call stack you will have to walk through the whole list!
        /// Executes in max. 20 micro secounds.
        /// </summary>
        /// <remarks>
        /// This method is not supported on Windows 2000 and will just return the
        /// calling unmanaged module wrapped in an array on that platform.
        /// </remarks>
        public static ProcessModule[] UnmanagedStackTrace
        {
            get
            {
                // not supported on windows 2000
                if ((Environment.OSVersion.Version.Major == 5) && (Environment.OSVersion.Version.Minor == 0))
                {
                    ProcessModule[] Module = new ProcessModule[1];

                    Module[0] = CallingUnmanagedModule;

                    return Module;
                }

                IntPtr Backup;

                NativeAPI.LhBarrierBeginStackTrace(out Backup);

                try
                {
                    if(StackBuffer == null)
                        StackBuffer = new StackTraceBuffer();
                    
                    Int16 Count = NativeAPI.RtlCaptureStackBackTrace(0, 32, StackBuffer.Unmanaged, IntPtr.Zero);
                    ProcessModule[] Result = new ProcessModule[Count];

                    StackBuffer.Synchronize(Count);

                    for (int i = 0; i < Count; i++)
                    {
                        Result[i] = PointerToModule(StackBuffer.Managed[i]);
                    }

                    return Result;
                }
                finally
                {
                    NativeAPI.LhBarrierEndStackTrace(Backup);
                }
            }
        }

        /// <summary>
        /// Creates a call stack trace of the managed code path that finally
        /// lead to your hook. To detect whether the desired module is within the
        /// call stack you will have to walk through the whole list!
        /// Executes in max. 80 micro secounds.
        /// </summary>
        public static System.Reflection.Module[] ManagedStackTrace
        {
            get
            {
                IntPtr Backup;

                NativeAPI.LhBarrierBeginStackTrace(out Backup);

                try
                {
                    StackFrame[] Frames = new StackTrace().GetFrames();
                    System.Reflection.Module[] Result = new System.Reflection.Module[Frames.Length];

                    for (int i = 0; i < Frames.Length; i++)
                    {
                        Result[i] = Frames[i].GetMethod().Module;
                    }

                    return Result;
                }
                finally
                {
                    NativeAPI.LhBarrierEndStackTrace(Backup);
                }
            }
        }
    }

    /// <summary>
    /// This class will provide various static members to be used with local hooking and
    /// is also the instance class of a hook.
    /// </summary>
    /// <include file='FileMonInject.xml' path='remarks'/>
    public partial class LocalHook : CriticalFinalizerObject, IDisposable
    {
        private Object m_ThreadSafe = new Object();
        private IntPtr m_Handle = IntPtr.Zero;
        private GCHandle m_SelfHandle;
        private Delegate m_HookProc;
        private Object m_Callback;
        private HookAccessControl m_ThreadACL;
        private static HookAccessControl m_GlobalThreadACL = new HookAccessControl(IntPtr.Zero);

        /// <summary>
        /// Ensures that each instance is always terminated with <see cref="Dispose"/>.
        /// </summary>
        ~LocalHook()
        {
            Dispose();
        }

        private LocalHook() { }

        /// <summary>
        /// The callback passed to <see cref="Create"/>.
        /// </summary>
        public Object Callback { get { return m_Callback; } }

        /// <summary>
        /// Returns the thread ACL associated with this hook. Refer to <see cref="IsThreadIntercepted"/>
        /// for more information about access negotiation.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// The underlying hook is already disposed.
        /// </exception>
        public HookAccessControl ThreadACL {
            get
            {
                if (IntPtr.Zero == m_Handle)
                    throw new ObjectDisposedException(typeof(LocalHook).FullName);

                return m_ThreadACL;
            }
        }

        /// <summary>
        /// Checks whether a given thread ID will be intercepted by the underlying hook.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method provides an interface to the internal negotiation algorithm.
        /// You may use it to check whether your ACL provides expected results.
        /// </para><para>
        /// The following is a pseudo code of how this method is implemented:
        /// <code>
        /// if(InThreadID == 0)
        ///     InThreadID = GetCurrentThreadId();
        /// 
        /// if(GlobalACL.Contains(InThreadID))
        /// {
        ///     if(LocalACL.Contains(InThreadID))
        /// 	{
        /// 		if(LocalACL.IsExclusive)
        /// 			return false;
        /// 	}
        /// 	else
        /// 	{
        /// 		if(GlobalACL.IsExclusive)
        /// 			return false;
        /// 
        /// 		if(!LocalACL.IsExclusive)
        /// 			return false;
        /// 	}
        /// }
        /// else
        /// {
        /// 	if(LocalACL.Contains(InThreadID))
        /// 	{
        /// 		if(LocalACL.IsExclusive)
        /// 			return false;
        /// 	}
        /// 	else
        /// 	{
        /// 		if(!GlobalACL.IsExclusive)
        /// 			return false;
        /// 
        /// 		if(!LocalACL.IsExclusive)
        /// 			return false;
        /// 	}
        /// }
        /// 
        /// return true;
        /// </code>
        /// </para>
        /// </remarks>
        /// <param name="InThreadID">A native OS thread ID; or zero if you want to check the current thread.</param>
        /// <returns><c>true</c> if the thread is intercepted, <c>false</c> otherwise.</returns>
        /// <exception cref="ObjectDisposedException">
        /// The underlying hook is already disposed.
        /// </exception>
        public bool IsThreadIntercepted(Int32 InThreadID)
        {
            Boolean Result;

            if (IntPtr.Zero == m_Handle)
                throw new ObjectDisposedException(typeof(LocalHook).FullName);

            NativeAPI.LhIsThreadIntercepted(m_Handle, InThreadID, out Result);

            return Result;
        }

        /// <summary>
        /// Returns the gloabl thread ACL associated with ALL hooks. Refer to <see cref="IsThreadIntercepted"/>
        /// for more information about access negotiation.
        /// </summary>
        public static HookAccessControl GlobalThreadACL { get { return m_GlobalThreadACL; } }

        /// <summary>
        /// If you want to immediately uninstall a hook, the only way is to dispose it. A disposed
        /// hook is guaranteed to never invoke your handler again but may still consume
        /// memory even for process life-time! 
        /// </summary>
        /// <remarks>
        /// As we are living in a manged world, you don't have to dispose a hook because the next 
        /// garbage collection will do it for you, assuming that your code does not reference it
        /// anymore. But there are times when you want to uninstall it excplicitly, with no delay.
        /// If you dispose a disposed or not installed hook, nothing will happen!
        /// </remarks>
        public void Dispose()
        {
            lock (m_ThreadSafe)
            {
                if (IntPtr.Zero == m_Handle)
                    return;

                NativeAPI.LhUninstallHook(m_Handle);

                Marshal.FreeCoTaskMem(m_Handle);

                m_Handle = IntPtr.Zero;
                m_Callback = null;
                m_HookProc = null;

                m_SelfHandle.Free();
            }
        }

        /// <summary>
        /// Installs a managed hook. After this you'll have to activate it by setting a proper <see cref="ThreadACL"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Note that not all entry points are hookable! In general methods like <c>CreateFileW</c>
        /// won't cause any trouble. But there might be methods that are not hookable because their
        /// entry point machine code is not eligable to be hooked. You should test all hooks on
        /// common environments like "Windows XP x86/x64 SP2/SP3" and "Windows Vista x86/x64 (SP1)".
        /// This is the only way to ensure that your application will work well on most machines.
        /// </para><para>
        /// Your handler delegate has to use the <see cref="UnmanagedFunctionPointerAttribute"/> and
        /// shall map to the same native method signature, otherwise the application will crash! The best
        /// way is to use predefined delegates used in related P-Invoke implementations usually found with Google.
        /// If you know how to write such native delegates you won't need internet resources of course.
        /// I recommend using C++.NET which allows you to just copy the related windows API to your managed
        /// class and thread it as delegate without any changes. This will also speed up the whole thing
        /// because no unnecessary marshalling is required! C++.NET is also better in most cases because you
        /// may access the whole native windows API from managed code without any effort what significantly eases
        /// writing of hook handlers.
        /// </para>
        /// <para>
        /// The given delegate is automatically prevented from being garbage collected until the hook itself
        /// is collected...
        /// </para>
        /// </remarks>
        /// <param name="InTargetProc">A target entry point that should be hooked.</param>
        /// <param name="InNewProc">A handler with the same signature as the original entry point
        /// that will be invoked for every call that has passed the Fiber Deadlock Barrier and various integrity checks.</param>
        /// <param name="InCallback">An uninterpreted callback that will later be available through <see cref="HookRuntimeInfo.Callback"/>.</param>
        /// <returns>
        /// A handle to the newly created hook.
        /// </returns>
        /// <exception cref="OutOfMemoryException">
        /// Not enough memory available to complete the operation. On 64-Bit this may also indicate
        /// that no memory can be allocated within a 31-Bit boundary around the given entry point.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// The given function pointer does not map to executable memory (valid machine code) or 
        /// you passed <c>null</c> as delegate.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The given entry point contains machine code that can not be hooked.
        /// </exception>
        /// <exception cref="InsufficientMemoryException">
        /// The maximum amount of hooks has been installed. This is currently set to MAX_HOOK_COUNT (1024).
        /// </exception>
        public static LocalHook Create(
            IntPtr InTargetProc,
            Delegate InNewProc,
            Object InCallback)
        {
            LocalHook Result = new LocalHook();

            Result.m_Callback = InCallback;
            Result.m_HookProc = InNewProc;
            Result.m_Handle = Marshal.AllocCoTaskMem(IntPtr.Size);
            Result.m_SelfHandle = GCHandle.Alloc(Result, GCHandleType.Weak);

            Marshal.WriteIntPtr(Result.m_Handle, IntPtr.Zero);

            try
            {
                NativeAPI.LhInstallHook(
                    InTargetProc,
                    Marshal.GetFunctionPointerForDelegate(Result.m_HookProc),
                    GCHandle.ToIntPtr(Result.m_SelfHandle),
                    Result.m_Handle);
            }
            catch(Exception e)
            {
                Marshal.FreeCoTaskMem(Result.m_Handle);
                Result.m_Handle = IntPtr.Zero;

                Result.m_SelfHandle.Free();

                throw e;
            }

            Result.m_ThreadACL = new HookAccessControl(Result.m_Handle);

            return Result;
        }

        /// <summary>
        /// Installs an unmanaged hook. After this you'll have to activate it by setting a proper <see cref="ThreadACL"/>.
        /// <see cref="HookRuntimeInfo"/> WON'T be supported! Refer to the native "LhBarrierXxx" APIs to
        /// access unmanaged hook runtime information.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Note that not all entry points are hookable! In general methods like <c>CreateFileW</c>
        /// won't cause any trouble. But there may be methods that are not hookable because their
        /// entry point machine code is not eligable to be hooked. You should test all hooks on
        /// common environments like "Windows XP x86/x64 SP1/SP2/SP3" and "Windows Vista x86/x64 (SP1)".
        /// This is the only way to ensure that your application will work well on most machines.
        /// </para><para>
        /// Unmanaged hooks will require a native DLL which handles the requests. This way
        /// you will get a high-performance interface, because
        /// a switch from unmanaged to managed code seems to be rather time consuming without doing anything
        /// useful (at least nothing visible); so a hook omitting this switch will be handled one or two
        /// orders of magnitudes faster until finally your handler gains execution. But as a managed hook is still executed
        /// within at last 1000 nano-seconds, even the "slow" managed implementation will be fast enough in most
        /// cases. With C++.NET you would be able to provide such native high-speed hooks for frequently
        /// called API methods, while still using managed ones for usual API methods, within a single assembly!
        /// A pure unmanaged, empty hook executes in approx. 70 nano-seconds, which is incredible fast
        /// considering the thread deadlock barrier and thread ACL negotiation that are already included in this benchmark!
        /// </para>
        /// </remarks>
        /// <param name="InTargetProc">A target entry point that should be hooked.</param>
        /// <param name="InNewProc">A handler with the same signature as the original entry point
        /// that will be invoked for every call that has passed the Thread Deadlock Barrier and various integrity checks.</param>
        /// <param name="InCallback">An uninterpreted callback that will later be available through <c>LhBarrierGetCallback()</c>.</param>
        /// <returns>
        /// A handle to the newly created hook.
        /// </returns>
        /// <exception cref="OutOfMemoryException">
        /// Not enough memory available to complete the operation. On 64-Bit this may also indicate
        /// that no memory can be allocated within a 31-Bit boundary around the given entry point.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// The given function pointer does not map to executable memory (valid machine code) or 
        /// you passed <c>null</c> as delegate.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The given entry point contains machine code that can not be hooked.
        /// </exception>
        /// <exception cref="InsufficientMemoryException">
        /// The maximum amount of hooks has been installed. This is currently set to MAX_HOOK_COUNT (1024).
        /// </exception>
        public static LocalHook CreateUnmanaged(
            IntPtr InTargetProc,
            IntPtr InNewProc,
            IntPtr InCallback)
        {
            LocalHook Result = new LocalHook();

            Result.m_Callback = InCallback;
            Result.m_Handle = Marshal.AllocCoTaskMem(IntPtr.Size);
            Result.m_SelfHandle = GCHandle.Alloc(Result, GCHandleType.Weak);

            // workitem/13695 & workitem/25580
            Marshal.WriteIntPtr(Result.m_Handle, IntPtr.Zero);

            try
            {
                NativeAPI.LhInstallHook(
                    InTargetProc,
                    InNewProc,
                    InCallback,
                    Result.m_Handle);
            }
            catch (Exception e)
            {
                Marshal.FreeCoTaskMem(Result.m_Handle);
                Result.m_Handle = IntPtr.Zero;

                Result.m_SelfHandle.Free();

                throw e;
            }

            Result.m_ThreadACL = new HookAccessControl(Result.m_Handle);

            return Result;
        }

        /// <summary>
        /// Will return the address for a given DLL export symbol. The specified
        /// module has to be loaded into the current process space and also export
        /// the given method.
        /// </summary>
        /// <remarks>
        /// If you wonder how to get native entry points in a managed environment,
        /// this is the anwser. You will only be able to hook native code from a managed
        /// environment if you have access to a method like this, returning the native
        /// entry point. Please note that you will also hook any managed code, which
        /// of course ultimately relies on the native windows API!
        /// </remarks>
        /// <param name="InModule">A system DLL name like "kernel32.dll" or a full qualified path to any DLL.</param>
        /// <param name="InSymbolName">An exported symbol name like "CreateFileW".</param>
        /// <returns>The entry point for the given API method.</returns>
        /// <exception cref="DllNotFoundException">
        /// The given module is not loaded into the current process.
        /// </exception>
        /// <exception cref="MissingMethodException">
        /// The given module does not export the desired method.
        /// </exception>
        public static IntPtr GetProcAddress(
            String InModule,
            String InSymbolName)
        {
            IntPtr Module = NativeAPI.GetModuleHandle(InModule);

            if (Module == IntPtr.Zero)
                throw new DllNotFoundException("The given library is not loaded into the current process.");

            IntPtr Method = NativeAPI.GetProcAddress(Module, InSymbolName);

            if (Method == IntPtr.Zero)
                throw new MissingMethodException("The given method does not exist.");

            return Method;
        }

        /// <summary>
        /// Will return a delegate for a given DLL export symbol. The specified
        /// module has to be loaded into the current process space and also export
        /// the given method.
        /// </summary>
        /// <remarks><para>
        /// This method is usually not useful to hook something but it allows you
        /// to dynamically load native API methods into your managed environment instead
        /// of using the static P-Invoke approach provided by <see cref="DllImportAttribute"/>.
        /// </para></remarks>
        /// <typeparam name="TDelegate">A delegate using the <see cref="UnmanagedFunctionPointerAttribute"/> and
        /// exposing the same method signature as the specified native symbol.</typeparam>
        /// <param name="InModule">A system DLL name like "kernel32.dll" or a full qualified path to any DLL.</param>
        /// <param name="InSymbolName">An exported symbol name like "CreateFileW".</param>
        /// <returns>The managed delegate wrapping around the given native symbol.</returns>
        /// <exception cref="DllNotFoundException">
        /// The given module is not loaded into the current process.
        /// </exception>
        /// <exception cref="MissingMethodException">
        /// The given module does not export the given method.
        /// </exception>
        public static TDelegate GetProcDelegate<TDelegate>(
            String InModule,
            String InSymbolName)
        {
            return (TDelegate)(Object)Marshal.GetDelegateForFunctionPointer(GetProcAddress(InModule, InSymbolName), typeof(TDelegate));
        }

        /// <summary>
        /// Processes any pending hook removals. Warning! This method can be quite slow (1 second) under certain circumstances.
        /// </summary>
        /// <see cref="NativeAPI.LhWaitForPendingRemovals()"/>
        public static void Release()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            NativeAPI.LhWaitForPendingRemovals(); 
        }
    }
}
