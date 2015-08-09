using System;
using System.Collections.Generic;
using System.Runtime.Remoting;
using System.Text;
using System.IO;
using EasyHook;
using System.Windows.Forms;

namespace FileMon
{
    public class FileMonInterface : MarshalByRefObject
    {
        public void IsInstalled(Int32 InClientPID)
        {
            Console.WriteLine("FileMon has been installed in target {0}.\r\n", InClientPID);
        }

        public void OnCreateFile(Int32 InClientPID, String[] InFileNames)
        {
            for (int i = 0; i < InFileNames.Length; i++)
            {
                Console.WriteLine(InFileNames[i]);
            }
        }

        public void ReportException(Exception InInfo)
        {
            Console.WriteLine("The target process has reported an error:\r\n" + InInfo.ToString());
        }

        public void Ping()
        {
        }
    }

    class Program
    {
        static String ChannelName = null;

        static void Main(string[] args)
        {
            Int32 TargetPID = 0;
            string targetExe = null;

            // Load the parameter
            while ((args.Length != 1) || !Int32.TryParse(args[0], out TargetPID) || !File.Exists(args[0]))
            {
                if (TargetPID > 0)
                {
                    break;
                }
                if (args.Length != 1 || !File.Exists(args[0]))
                {
                    Console.WriteLine();
                    Console.WriteLine("Usage: FileMon %PID%");
                    Console.WriteLine("   or: FileMon PathToExecutable");
                    Console.WriteLine();
                    Console.Write("Please enter a process Id or path to executable: ");

                    args = new string[] { Console.ReadLine() };

                    if (String.IsNullOrEmpty(args[0])) return;
                }
                else
                {
                    targetExe = args[0];
                    break;
                }
            }

            try
            {
                RemoteHooking.IpcCreateServer<FileMonInterface>(ref ChannelName, WellKnownObjectMode.SingleCall);

                string injectionLibrary = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "FileMonInject.dll");
                if (String.IsNullOrEmpty(targetExe))
                {
                    RemoteHooking.Inject(
                        TargetPID,
                        injectionLibrary,
                        injectionLibrary,
                        ChannelName);

                    Console.WriteLine("Injected to process {0}", TargetPID);
                }
                else
                {
                    RemoteHooking.CreateAndInject(targetExe, "", 0, InjectionOptions.DoNotRequireStrongName, injectionLibrary, injectionLibrary, out TargetPID, ChannelName);
                    Console.WriteLine("Created and injected process {0}", TargetPID);
                }
                Console.WriteLine("<Press any key to exit>");
                Console.ReadKey();
            }
            catch (Exception ExtInfo)
            {
                Console.WriteLine("There was an error while connecting to target:\r\n{0}", ExtInfo.ToString());
                Console.WriteLine("<Press any key to exit>");
                Console.ReadKey();
            }
        }
    }
}