using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using EasyHook;
using System.Diagnostics;
using System.IO;

namespace Examples
{
    public class LHTest
    {
        static DMethodA LHTestHookA = new DMethodA(MethodAHooked);
        static DMethodB LHTestHookB = new DMethodB(MethodBHooked);
        static DMethodA LHTestMethodADelegate;
        static DMethodB LHTestMethodBDelegate;
        static IntPtr LHTestMethodA;
        static IntPtr LHTestMethodB;


        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate bool DMethodA(Int32 InParam1);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate Int64 DMethodB(Int32 InParam1, Int32 InParam2, String InParam3);

        static void CheckRuntimeInfo()
        {/*
            ProcessModule Mod = HookRuntimeInfo.CallingUnmanagedModule;
            System.Reflection.Assembly asm = HookRuntimeInfo.CallingManagedModule;
            Stopwatch w = new Stopwatch();
            System.Reflection.Module[] StackTraceA = HookRuntimeInfo.ManagedStackTrace;
            ProcessModule[] StackTraceB = HookRuntimeInfo.UnmanagedStackTrace;
            Object Callback = HookRuntimeInfo.Callback;
            IntPtr ReturnAddr = HookRuntimeInfo.ReturnAddress;
            IntPtr AddrOfReturnAddr = HookRuntimeInfo.AddressOfReturnAddress;
            Boolean IsHandler = HookRuntimeInfo.IsHandlerContext;

            w.Start();

            Mod = HookRuntimeInfo.CallingUnmanagedModule;

            w.Stop();
            Int64 t1 = w.ElapsedTicks;

            w.Reset();
            
            w.Start();

            Callback = HookRuntimeInfo.Callback;
            ReturnAddr = HookRuntimeInfo.ReturnAddress;
            AddrOfReturnAddr = HookRuntimeInfo.AddressOfReturnAddress;
            IsHandler = HookRuntimeInfo.IsHandlerContext;

            w.Stop();
            Int64 t2 = w.ElapsedTicks;*/
        }

        static bool MethodAHooked(Int32 InParam1)
        {
            CheckRuntimeInfo();

            LHTestMethodBDelegate.Invoke(0, 1, "Hallo");

            Interlocked.Increment(ref LHTestCounterMAH);

            return true;
        }

        static Int64 MethodBHooked(Int32 InParam1, Int32 InParam2, String InParam3)
        {
            CheckRuntimeInfo();

            LHTestMethodADelegate.Invoke(0);

            Interlocked.Increment(ref LHTestCounterMBH);

            return 0;
        }

        static bool MethodA(Int32 InParam1)
        {
            Interlocked.Increment(ref LHTestCounterMA);

            return true;
        }


        static Int64 MethodB(Int32 InParam1, Int32 InParam2, String InParam3)
        {
            LHTestMethodADelegate.Invoke(0);

            Interlocked.Increment(ref LHTestCounterMB);

            return 0;
        }

        static void LHTestThread()
        {
            for (int x = 0; x < 10000; x++)
            {
                LHTestMethodBDelegate.Invoke(0, 0, "");
            }

            Interlocked.Increment(ref LHTestThreadCounter);


            if (LHTestThreadCounter == LHTestThreadCount)
                LHTestCompleted.Set();
        }

        static Int32 LHTestThreadCounter = 0;
        static Int32 LHTestCounterMA = 0;
        static Int32 LHTestCounterMB = 0;
        static Int32 LHTestCounterMAH = 0;
        static Int32 LHTestCounterMBH = 0;
        static ManualResetEvent LHTestCompleted = new ManualResetEvent(false);

        const Int32 LHTestThreadCount = 30;


        public static void Run()
        {
            DMethodA MethodADelegate = new DMethodA(MethodA);
            DMethodB MethodBDelegate = new DMethodB(MethodB);

            GC.KeepAlive(MethodADelegate);
            GC.KeepAlive(MethodBDelegate);

            LHTestMethodA = Marshal.GetFunctionPointerForDelegate(MethodADelegate);
            LHTestMethodB = Marshal.GetFunctionPointerForDelegate(MethodBDelegate);

            LocalHook.EnableRIPRelocation();

            // install hooks
            LocalHook[] MyHooks = new LocalHook[]
            {
                LocalHook.Create(
                   LHTestMethodA,
                   LHTestHookA,
                   1),

                LocalHook.Create(
                    LHTestMethodB,
                    LHTestHookB,
                    2),
            };


            LHTestMethodADelegate = (DMethodA)Marshal.GetDelegateForFunctionPointer(LHTestMethodA, typeof(DMethodA));
            LHTestMethodBDelegate = (DMethodB)Marshal.GetDelegateForFunctionPointer(LHTestMethodB, typeof(DMethodB));

            // we want to intercept all threads...
            MyHooks[0].ThreadACL.SetInclusiveACL(new Int32[1]);
            MyHooks[1].ThreadACL.SetInclusiveACL(new Int32[1]);

           // LHTestMethodBDelegate.Invoke(0, 0, "");

            MyHooks[0].ThreadACL.SetExclusiveACL(new Int32[1]);
            MyHooks[1].ThreadACL.SetExclusiveACL(new Int32[1]);

           // LHTestMethodBDelegate.Invoke(0, 0, "");

            /*
             * This is just to make sure that all related objects are referenced.
             * At the beginning there were several objects like delegates that have
             * been collected during execution! The NET-Framework will produce bugchecks
             * in such cases...
             */
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            IntPtr t = Marshal.GetFunctionPointerForDelegate(LHTestHookA);

            Int64 t1 = System.Diagnostics.Stopwatch.GetTimestamp();

            for (int i = 0; i < LHTestThreadCount; i++)
            {
                new Thread(new ThreadStart(LHTestThread)).Start();

            }

            LHTestCompleted.WaitOne();

            t1 = ((System.Diagnostics.Stopwatch.GetTimestamp() - t1) * 1000) / System.Diagnostics.Stopwatch.Frequency;

            // verify results
            if ((LHTestCounterMA != LHTestCounterMAH) || (LHTestCounterMAH != LHTestCounterMB) ||
                    (LHTestCounterMB != LHTestCounterMBH) || (LHTestCounterMB != LHTestThreadCount * 10000))
                throw new Exception("LocalHook test failed.");

            Console.WriteLine("Localhook test passed in {0} ms.", t1);
        }
    }
}
