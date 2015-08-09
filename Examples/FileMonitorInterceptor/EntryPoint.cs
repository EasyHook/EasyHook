using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using EasyHook;
using FileMonitorInterface;

namespace FileMonitorInterceptor
{
    /// <summary>
    /// Our program entry point after DLL injection.
    /// </summary>
    /// <remarks>
    /// EasyHook searches all exported types of your assembly for a class that implements IEntryPoint, and then instantiates the class and invokes Run().
    /// The number of parameters for this class' constructor must match those of Run() *and* those you passed in RemoteHooking.Inject() (in the FileMonitorController project).
    /// If the number of parameters do not match for any of these, EasyHook will throw an exception (you'll be notified of it) and fail.
    /// </remarks>
    public class EntryPoint : IEntryPoint
    {
        public IpcInterface IpcInterface { get; set; }
        public LocalHook CreateFileHook { get; set; }
        public bool keepRunning = true;

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        private delegate IntPtr CreateFileDelegate(string filePath, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition, uint flags, IntPtr templateFile);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr CreateFile(string filePath, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition, uint flags, IntPtr templateFile);

        /// <summary>
        /// This constructor is the first method that will be called in this entire injected DLL.
        /// </summary>
        public EntryPoint(RemoteHooking.IContext context, string channelName)
        {
            // Connect our IPC channel
            this.IpcInterface = RemoteHooking.IpcConnectClient<IpcInterface>(channelName);
            // We can now communicate freely with FileMonitorController
            // But let's ping FileMonitorController just to make sure
            this.IpcInterface.Ping();
        }

        public void Run(RemoteHooking.IContext context, string channelName)
        {
            Load();
            Main();
            Unload();
        }

        public void Load()
        {
            try
            {
                CreateFileHook = LocalHook.Create(
                    LocalHook.GetProcAddress("kernel32.dll", "CreateFileW"),
                    new CreateFileDelegate(OnCreateFile),
                    this);

                // All hooks start de-activated
                // The following ensures that this hook can be intercepted from all threads of this process
                CreateFileHook.ThreadACL.SetExclusiveACL(new Int32[1]);
            }
            catch (Exception ex)
            {
                IpcInterface.PostException(ex);
            }
        }

        /// <summary>
        /// This callback is invoked whenever this process calls CreateFile(). This is where we can modify parameters and other cool things.
        /// </summary>
        /// <remarks>
        /// The method signature must match the original CreateFile().
        /// </remarks>
        private IntPtr OnCreateFile(string filePath, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition, uint flags, IntPtr templateFile)
        {
            try
            {
                /* 
                 * Note that we can do whatever we want in this callback. We could change the file path, return an access denied (pretend we're an antivirus program),
                 * but we won't do that in this program. This program only monitors file access from processes. 
                 */

                var fileEntry = new FileEntry() {FullPath = filePath, Timestamp = DateTime.Now };
                var processId = Process.GetCurrentProcess().Id;

                IpcInterface.AddFileEntry(processId, fileEntry);
            }
            catch (Exception ex)
            {
                IpcInterface.PostException(ex);
            }

            // The process had originally intended to call CreateFile(), so let's actually call Windows' original CreateFile()
            return CreateFile(filePath, desiredAccess, shareMode, securityAttributes, creationDisposition, flags, templateFile);
        }

        public void Main()
        {
            try
            {
                // All of our program's main execution takes place within the OnCreateFile() hook callback. So we don't really do anything here.
                // Except Ping() our FileMonitorController program to make sure it's still alive; if it's closed, we should also close
                while (! IpcInterface.TerminatingProcesses.Contains (Process.GetCurrentProcess().Id))
                {
                    Thread.Sleep(0);
                    IpcInterface.Ping();
                }

                // When this method returns (and, consequently, Run()), our injected DLL terminates and that's the end of our program (though Unload() will be called first)
            }
            catch (Exception ex)
            {
                IpcInterface.PostException(ex);
            }
        }

        public void Unload()
        {
            try
            {
                // We're exiting our program now
                IpcInterface.TerminatingProcesses.Remove (Process.GetCurrentProcess().Id);
                CreateFileHook.Dispose();
            }
            catch (Exception ex)
            {
                IpcInterface.PostException(ex);
            }
        }
    }
}
