using System;
using System.Collections.Generic;
using System.Text;
using EasyHook;
using System.Threading;
using System.Runtime.InteropServices;

namespace ComplexParameterInject
{
    [Serializable]
    public class TestComplexParameter
    {
        public string Message;
        public int HostProcessId;
    }

    public class TestInjection : EasyHook.IEntryPoint
    {
        TestInterface _interface;
        public LocalHook CreateFileHook = null;

        public TestInjection(
            RemoteHooking.IContext context,
            String channelName
            , TestComplexParameter parameter
            )
        {
            // connect to host...
            _interface = RemoteHooking.IpcConnectClient<TestInterface>(channelName);

            _interface.Ping();
        }

        public void Run(
            RemoteHooking.IContext context,
            String channelName
            , TestComplexParameter parameter
            )
        {
            try
            {
                CreateFileHook = LocalHook.Create(
                    LocalHook.GetProcAddress("kernel32.dll", "CreateFileW"),
                    new DCreateFile(CreateFile_Hooked),
                    this);

                /*
                 * Don't forget that all hooks will start deactivated...
                 * The following ensures that all threads are intercepted:
                 */
                CreateFileHook.ThreadACL.SetExclusiveACL(new Int32[1]);
            }
            catch (Exception e)
            {
                /*
                    Now we should notice our host process about this error...
                 */
                _interface.ReportException(e);

                return;
            }


            _interface.IsInstalled(RemoteHooking.GetCurrentProcessId());
            _interface.SendMessage(parameter.Message);
            try
            {
                while (true)
                {
                    Thread.Sleep(10);

                    _interface.Ping();
                }
            
            }
            catch
            {
            }
        }

        private void SendMessage(string message)
        {
            _interface.SendMessage(message);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        delegate IntPtr DCreateFile(
            String InFileName,
            UInt32 InDesiredAccess,
            UInt32 InShareMode,
            IntPtr InSecurityAttributes,
            UInt32 InCreationDisposition,
            UInt32 InFlagsAndAttributes,
            IntPtr InTemplateFile);

        // just use a P-Invoke implementation to get native API access from C# (this step is not necessary for C++.NET)
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        static extern IntPtr CreateFile(
            String InFileName,
            UInt32 InDesiredAccess,
            UInt32 InShareMode,
            IntPtr InSecurityAttributes,
            UInt32 InCreationDisposition,
            UInt32 InFlagsAndAttributes,
            IntPtr InTemplateFile);

        // this is where we are intercepting all file accesses!
        static IntPtr CreateFile_Hooked(
            String InFileName,
            UInt32 InDesiredAccess,
            UInt32 InShareMode,
            IntPtr InSecurityAttributes,
            UInt32 InCreationDisposition,
            UInt32 InFlagsAndAttributes,
            IntPtr InTemplateFile)
        {
            try
            {
                TestInjection Self = (TestInjection)HookRuntimeInfo.Callback;

                Self.SendMessage("CreateFile: " + InFileName);
            }
            catch
            {
            }

            // call original API...
            return CreateFile(
                InFileName,
                InDesiredAccess,
                InShareMode,
                InSecurityAttributes,
                InCreationDisposition,
                InFlagsAndAttributes,
                InTemplateFile);
        }
    }

    
}
