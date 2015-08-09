using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Security.Principal;
using EasyHook;

namespace Examples
{
    public class TestInjection : EasyHook.IEntryPoint
    {
        public TestInjection(RemoteHooking.IContext InContext, Int32 InValue)
        {

        }

        public void Run(RemoteHooking.IContext InContext, Int32 InValue)
        {
            System.Windows.Forms.MessageBox.Show("Hello from injected library!");

            //RemoteHooking.WakeUpProcess();

            Console.WriteLine("Hello world!");
        }
    }

    public class RHTest
    {
        [Serializable]
        public class ProcessInfo
        {
            public String FileName;
            public Int32 Id;
            public Boolean Is64Bit;
            public String Identity;
        }

        public static ProcessInfo[] Enum()
        {
            List<ProcessInfo> Result = new List<ProcessInfo>();
            Process[] ProcList = Process.GetProcesses();

            for (int i = 0; i < ProcList.Length; i++)
            {
                Process Proc = ProcList[i];

                try
                {
                    ProcessInfo Info = new ProcessInfo();

                    Info.FileName = Proc.MainModule.FileName;
                    Info.Id = Proc.Id;
                    Info.Is64Bit = RemoteHooking.IsX64Process(Proc.Id);
                    Info.Identity = RemoteHooking.GetProcessIdentity(Proc.Id).Owner.ToString();
                    
                    Result.Add(Info);
                }
                catch
                {
                }
            }

            return Result.ToArray();
        }

        public static void Run()
        {
            Config.Register(
                "A simple ProcessMonitor based on EasyHook!",
                "ProcMonInject.dll",
                "ProcessMonitor.exe");
            /*
            Config.Register("EasyHook managed test application",
                "..\\x64\\ManagedTest.exe");
            
            /*
            RemoteHooking.CreateAndInject(
                @"..\x86\ManagedTarget.exe",
                "",
                /*DETACHED_PROCESS(8)*0x10,
                "..\\x86\\ManagedTest.exe",
                "..\\x64\\ManagedTest.exe",
                out ProcessId,
                0x12345678);*/
            
          
          //  ProcessInfo[] Result = (ProcessInfo[])RemoteHooking.ExecuteAsService<RHTest>("Enum");
            
            RemoteHooking.Inject(
                RemoteHooking.GetCurrentProcessId(),
                "..\\x86\\ManagedTest.exe",
                "..\\x64\\ManagedTest.exe", 
                0x12345678);
        }
    }
}
