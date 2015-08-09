using System;
using System.Collections.Generic;
using System.Text;
using EasyHook;
using System.Runtime.Remoting;

namespace ComplexParameterTest
{
    class Program
    {
        static void Main(string[] args)
        {
            int targetPID;

            if ((args.Length != 1) || !Int32.TryParse(args[0], out targetPID))
            {
                Console.WriteLine();
                Console.WriteLine("Usage: ComplexParameterTest %ProcessID%");
                Console.WriteLine();
                Console.Write("Please enter the target PID: ");
                if (!Int32.TryParse(Console.ReadLine(), out targetPID))
                {
                    Console.WriteLine("Invalid PID");
                    return;
                }
            }

            try
            {
                bool noGAC = true;
                //try
                //{
                //    Config.Register(
                //        "Test passing an object parameter",
                //        "ComplexParameterInject.dll");
                //}
                //catch (ApplicationException ae)
                //{
                //    Console.WriteLine("This is an administrative task! Attempting without GAC...");

                //    noGAC = true;
                //}
                string channelName = null;
                RemoteHooking.IpcCreateServer<ComplexParameterInject.TestInterface>(ref channelName, WellKnownObjectMode.SingleCall);

                InjectionOptions options = InjectionOptions.Default;
                if (noGAC)
                {
                    options |= InjectionOptions.DoNotRequireStrongName;
                }

                ComplexParameterInject.TestComplexParameter parameter = new ComplexParameterInject.TestComplexParameter
                {
                    Message = "All your binary serializations are belong to us",
                    HostProcessId = RemoteHooking.GetCurrentProcessId()
                };
                RemoteHooking.Inject(
                    targetPID,
                    options,
                    typeof(ComplexParameterInject.TestComplexParameter).Assembly.Location, //"ComplexParameterInject.dll",
                    typeof(ComplexParameterInject.TestComplexParameter).Assembly.Location, //"ComplexParameterInject.dll",
                    channelName
                    , parameter
                );

            }
            catch (Exception e)
            {
                Console.WriteLine("Injection failed: {0}:{1}", e.GetType().FullName, e.Message);
            }

            Console.WriteLine("Press <enter> to continue");
            Console.ReadLine();
        }
    }
}
