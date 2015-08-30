using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace TestFuncHooks
{
    class Program
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct TEST_FUNC_HOOKS_OPTIONS
        {
            [MarshalAs(UnmanagedType.LPStr)]
            public string Filename;
            [MarshalAs(UnmanagedType.LPStr)]
            public string FilterByName;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct TEST_FUNC_HOOKS_RESULT
        {
            [MarshalAs(UnmanagedType.LPStr)]
            public string FnName;
            [MarshalAs(UnmanagedType.LPStr)]
            public string ModuleRedirect;
            [MarshalAs(UnmanagedType.LPStr)]
            public string FnRedirect;
            IntPtr FnAddress;
            IntPtr RelocAddress;
            [MarshalAs(UnmanagedType.LPStr)]
            public string EntryDisasm;
            [MarshalAs(UnmanagedType.LPStr)]
            public string RelocDisasm;
            [MarshalAs(UnmanagedType.LPStr)]
            public string Error;
        }

        public static class NativeAPI_Pub_x86
        {
            private const String DllName = "EasyHook32.dll";

            [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
            public static extern Int32 TestFuncHooks(
                int pId,
                [param: MarshalAs(UnmanagedType.LPStr)]
                string module,
                [param: MarshalAs(UnmanagedType.Struct)]
                TEST_FUNC_HOOKS_OPTIONS options,
                out IntPtr results, out int resultCount);

            [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
            public static extern Int32 ReleaseTestFuncHookResults(IntPtr results, int resultCount);
        }

        public static class NativeAPI_Pub_x64
        {
            private const String DllName = "EasyHook64.dll";

            [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
            public static extern Int32 TestFuncHooks(
                int pId,
                [param: MarshalAs(UnmanagedType.LPStr)]
                string module,
                [param: MarshalAs(UnmanagedType.Struct)]
                TEST_FUNC_HOOKS_OPTIONS options,
                out IntPtr results, out int resultCount);

            [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
            public static extern Int32 ReleaseTestFuncHookResults(IntPtr results, int resultCount);
        }

        static bool IsDisamSame(string disam1, string disam2)
        {
            var d1 = disam1.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var d2 = disam2.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            if (d1.Length != d2.Length)
                return false;
            else
            {
                for (var i = 0; i < d1.Length; i++)
                {
                    d1[i] = d1[i].Substring(0, d1[i].IndexOf("IP:"));
                    d2[i] = d2[i].Substring(0, d2[i].IndexOf("IP:"));

                    if (d1[i] != d2[i])
                        return false;
                }
            }

            return true;
        }

        static void PrintResults(string moduleName, TEST_FUNC_HOOKS_RESULT[] results)
        {
            int errorCount = 0;
            int containAddress = 0;
            int noRedirect = 0;

            foreach (var item in results)
            {
                if (!String.IsNullOrEmpty(item.Error))
                    errorCount++;
                else if (IsDisamSame(item.EntryDisasm, item.RelocDisasm))
                    containAddress++;
                else
                    noRedirect++;
            }

            Console.WriteLine(String.Format("{0,-25}{1,15}{2,12}{3,12}{4,15}", moduleName, errorCount, containAddress, noRedirect, (errorCount + containAddress + noRedirect)));
        }

        static bool TryGetProcessById(int pid, out System.Diagnostics.Process process)
        {
            process = null;
            try
            {
                process = System.Diagnostics.Process.GetProcessById(pid);
                return true;
            }
            catch (Exception)
            {
            }
            return false;
        }

        static void Main(string[] args)
        {
            int targetPID = 0;
            bool is64 = false;

            System.Diagnostics.Process p = null;
            if ((args.Length != 1) || !Int32.TryParse(args[0], out targetPID))
            {
                Console.WriteLine();
                Console.WriteLine("Usage: TestFuncHooks [processId]");
                Console.WriteLine();
                if (EasyHook.RemoteHooking.IsX64Process(EasyHook.RemoteHooking.GetCurrentProcessId()))
                {
                    Console.WriteLine("Current process is 64-bit");
                    is64 = true;
                }
                else
                    Console.WriteLine("Current process is 32-bit");
                Console.Write("Please enter the target PID (blank for current process): ");
                if (!Int32.TryParse(Console.ReadLine(), out targetPID))
                {
                    targetPID = EasyHook.RemoteHooking.GetCurrentProcessId();
                    Console.WriteLine("Using current process: " + targetPID);
                    TryGetProcessById(targetPID, out p);
                }
                else if (!TryGetProcessById(targetPID, out p))
                {
                    Console.WriteLine("Unable to open process: " + targetPID);
                    Console.WriteLine("Press any key to exit");
                    Console.ReadKey(true);
                    return;
                }
            }

            Console.WriteLine();
            
            if (is64 != EasyHook.RemoteHooking.IsX64Process(p.Id))
            {
                Console.WriteLine("Target process must be " + (is64 ? "64-bit" : "32-bit") + " like current process");
                Console.WriteLine("Press any key to exit");
                Console.ReadKey(true);
                return;
            }

            Console.WriteLine("Test hooking of DLL exports within process: " + targetPID + " - " + p.ProcessName);

            Console.WriteLine("-------------------------------------------------------------------------------");
            Console.WriteLine(String.Format("{0,-25}{1,15}{2,12}{3,12}{4,15}", "Module", "Unsupported", "Modified", "Unchanged", "Total"));
            Console.WriteLine("-------------------------------------------------------------------------------");

            TEST_FUNC_HOOKS_RESULT[] results;
            IntPtr resultsPtr = IntPtr.Zero;
            int resultCount = 0;
            TEST_FUNC_HOOKS_OPTIONS options = new TEST_FUNC_HOOKS_OPTIONS();

            #region 64-bit
            if (EasyHook.RemoteHooking.IsX64Process(EasyHook.RemoteHooking.GetCurrentProcessId()))
            {
                if (!Directory.Exists("EntryPoints64"))
                {
                    Directory.CreateDirectory("EntryPoints64");
                }

                foreach (System.Diagnostics.ProcessModule module in p.Modules)
                {
                    options.FilterByName = null;
                    options.Filename = @"EntryPoints64\_" + Path.GetFileNameWithoutExtension(module.FileName) + ".txt";
                    var moduleName = Path.GetFileName(module.FileName);
                    
                    NativeAPI_Pub_x64.TestFuncHooks(targetPID, moduleName, options, out resultsPtr, out resultCount);
                    if (resultCount > 0)
                    {
                        results = new TEST_FUNC_HOOKS_RESULT[resultCount];
                        for (var i = 0; i < resultCount; i++)
                        {
                            results[i] = (TEST_FUNC_HOOKS_RESULT)Marshal.PtrToStructure(new IntPtr(resultsPtr.ToInt64() + i * Marshal.SizeOf(typeof(TEST_FUNC_HOOKS_RESULT))), typeof(TEST_FUNC_HOOKS_RESULT));
                        }

                        NativeAPI_Pub_x64.ReleaseTestFuncHookResults(resultsPtr, resultCount);

                        PrintResults(moduleName, results);
                    }
                }
            }
            #endregion

            #region 32-bit
            else
            {
                if (!Directory.Exists("EntryPoints32"))
                {
                    Directory.CreateDirectory("EntryPoints32");
                }
                foreach (System.Diagnostics.ProcessModule module in p.Modules)
                {
                    options.FilterByName = null;
                    options.Filename = @"EntryPoints32\_" + Path.GetFileNameWithoutExtension(module.FileName) + ".txt";
                    var moduleName = Path.GetFileName(module.FileName);
                    NativeAPI_Pub_x86.TestFuncHooks(targetPID, moduleName, options, out resultsPtr, out resultCount);
                    if (resultCount > 0)
                    {
                        results = new TEST_FUNC_HOOKS_RESULT[resultCount];
                        for (var i = 0; i < resultCount; i++)
                        {
                            results[i] = (TEST_FUNC_HOOKS_RESULT)Marshal.PtrToStructure(new IntPtr(resultsPtr.ToInt32() + i * Marshal.SizeOf(typeof(TEST_FUNC_HOOKS_RESULT))), typeof(TEST_FUNC_HOOKS_RESULT));
                        }

                        NativeAPI_Pub_x86.ReleaseTestFuncHookResults(resultsPtr, resultCount);

                        PrintResults(moduleName, results);
                    }
                }
            }
            #endregion

            Console.WriteLine("-------------------------------------------------------------------------------");
            Console.WriteLine(" Unsupported = # methods not hookable by EasyHook");
            Console.WriteLine(" Modified    = # methods hookable by modifying one or more instructions");
            Console.WriteLine(" Unchanged   = # methods hookable by copying instructions unchanged");
            Console.WriteLine("-------------------------------------------------------------------------------");

            Console.WriteLine("Complete - press any key to exit");
            Console.ReadKey(true);
        }
    }
}
