using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using System.Security.Principal;
using System.Security;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
using System.Threading;
using EasyHook;

/*
 * This is a very simple GUI that just demonstrates the very basics of what you can do with EasyHook...
 */

namespace ProcessMonitor
{

    public partial class Form1 : Form
    {
        private List<Process> LastProcesses = new List<Process>();
        static internal List<Int32> HookedProcesses = new List<Int32>();
        private String ChannelName = null;
        private IpcServerChannel DemoServer;

        /*
         * Please note that we have obtained this information with system privileges.
         * So if you get client requests with a process ID don't try to open the process
         * as this will fail in some cases. Just search the ID in the following list and
         * extract information that is already there...
         * 
         * Of course you can change the way this list is implemented and the information
         * it contains but you should keep the code semantic.
         */
        internal static List<ProcessInfo> ProcessList = new List<ProcessInfo>();
        private static List<Int32> ActivePIDList = new List<Int32>();
        private static System.Threading.Timer ProcessTimer = null;

        /*
         * In general you shouldn't access the GUI directly from other threads,
         * so we are using an asynchronous approach!
         */
        static internal Queue<MonitorEntry> MonitorQueue = new Queue<MonitorEntry>();
        static internal Boolean IsMonitoring = true;

        static bool _noGAC = false;
        public Form1(bool noGAC)
        {
            _noGAC = noGAC;
            InitializeComponent();

            ProcessTimer = new System.Threading.Timer(new System.Threading.TimerCallback(OnProcessUpdate), null, 0, 5000);

            TIMER_Tick(null, null);

            /*
             * We will create a random named channel. This is where our injected
             * libraries will connect to!
             * 
             * For people who don't know about NET-Remoting, please read some
             * valuable articles. In short, the client libraries will
             * just call the methods in "DemoInterface" and NET will pass
             * them to us, we will process the calls, and finally NET sends
             * the result back. This is a great advantage; imagine how many
             * lines of native C++ code this would require!!!
             */
            DemoServer = RemoteHooking.IpcCreateServer<DemoInterface>(
                ref ChannelName,
                WellKnownObjectMode.Singleton);
        }

        private static void OnProcessUpdate(Object InCallback)
        {
            ProcessTimer.Change(Timeout.Infinite, Timeout.Infinite);

            try
            {
                ProcessInfo[] Array;
                if (_noGAC)
                {
                    Array = EnumProcesses();
                }
                else
                {
                    Array = (ProcessInfo[])RemoteHooking.ExecuteAsService<Form1>("EnumProcesses");
                }
                SortedDictionary<String, ProcessInfo> Result = new SortedDictionary<string, ProcessInfo>();

                // sort by name...
                lock (ProcessList)
                {
                    ActivePIDList.Clear();

                    for (int i = 0; i < Array.Length; i++)
                    {
                        Result.Add(System.IO.Path.GetFileName(Array[i].FileName) + "____" + i, Array[i]);

                        ActivePIDList.Add(Array[i].Id);
                    }

                    Result.Values.CopyTo(Array, 0);

                    ProcessList.Clear();

                    ProcessList.AddRange(Array);
                }
            }
            catch (AccessViolationException)
            {
                MessageBox.Show("This is an administrative task!", "Permission denied...", MessageBoxButtons.OK);

                Process.GetCurrentProcess().Kill();
            }
            finally
            {
                ProcessTimer.Change(5000, 5000);
            }
        }

        [Serializable]
        public class ProcessInfo
        {
            public String FileName;
            public Int32 Id;
            public Boolean Is64Bit;
            public String User;
        }

        public static ProcessInfo[] EnumProcesses()
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
                    Info.User = RemoteHooking.GetProcessIdentity(Proc.Id).Name;

                    Result.Add(Info);
                }
                catch
                {
                }
            }

            return Result.ToArray();
        }

        private void TIMER_Tick(object sender, EventArgs e)
        {
            TIMER.Stop();

            try
            {
                /*
                 * Add new monitoring entries...
                 */
                LIST_Accesses.BeginUpdate();

                lock (MonitorQueue)
                {
                    while (MonitorQueue.Count > 0)
                    {
                        MonitorEntry Entry = MonitorQueue.Dequeue();
                        ListViewItem Item = LIST_Accesses.Items.Insert(0, Entry.ClientPID.ToString());

                        Item.SubItems.Add(Entry.Timestamp.ToString("hh:mm:ss." + ((Entry.Timestamp.Ticks / 10000) % 1000).ToString()));
                        Item.SubItems.Add(Entry.Access);
                    }
                }

                LIST_Accesses.EndUpdate();

                /*
                 * Update internal process list...
                 */
                lock (ProcessList)
                {
                    Boolean HasChanged = false;

                    if (ProcessList.Count == LIST_Processes.Items.Count)
                    {
                        for (int i = 0; i < ProcessList.Count; i++)
                        {
                            if (ProcessList[i].Id != Int32.Parse(LIST_Processes.Items[i].SubItems[1].Text))
                            {
                                HasChanged = true;

                                break;
                            }
                        }
                    }
                    else
                        HasChanged = true;

                    if (HasChanged)
                    {
                        Int32 SelIndex = 0;

                        if (LIST_Processes.SelectedIndices.Count > 0)
                            SelIndex = LIST_Processes.SelectedIndices[0];

                        LIST_Processes.BeginUpdate();
                        LIST_Processes.Items.Clear();

                        for (int i = 0; i < ProcessList.Count; i++)
                        {
                            ProcessInfo Proc = ProcessList[i];
                            ListViewItem Item = LIST_Processes.Items.Add("");

                            Item.SubItems.Add(Proc.Id.ToString());

                            if (Proc.Is64Bit)
                                Item.SubItems.Add("x64");
                            else
                                Item.SubItems.Add("x86");

                            Item.SubItems.Add(System.IO.Path.GetFileName(Proc.FileName));
                            Item.SubItems.Add(Proc.User);
                            Item.SubItems.Add(Proc.FileName);

                            Item.Checked = HookedProcesses.Contains(Proc.Id);
                        }

                        if (SelIndex >= LIST_Processes.Items.Count)
                            SelIndex = LIST_Processes.Items.Count - 1;

                        LIST_Processes.Items[SelIndex].Selected = true;
                        LIST_Processes.Items[SelIndex].EnsureVisible();

                        LIST_Processes.EndUpdate();
                    }


                    /*
                     * Update list of hooked processes...
                     */
                    for (int i = 0; i < HookedProcesses.Count; i++)
                    {
                        if (!ActivePIDList.Contains(HookedProcesses[i]))
                            HookedProcesses.RemoveAt(i--);
                    }
                }
            }
            finally
            {
                TIMER.Start();
            }
        }

        private void BTN_Clear_Click(object sender, EventArgs e)
        {
            LIST_Accesses.Items.Clear();
        }

        private void LIST_Processes_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (e.Item.SubItems.Count < 2)
                return;

            Int32 PID = Int32.Parse(e.Item.SubItems[1].Text);

            if (e.Item.Checked)
            {
                // was newly checked?
                if (HookedProcesses.Contains(PID))
                    return;

                // inject library...
                try
                {
                    HookedProcesses.Add(PID); // this will ensure that Ping() returns true...

                    RemoteHooking.Inject(
                        PID,
                        (_noGAC ? InjectionOptions.DoNotRequireStrongName : InjectionOptions.Default), // if not using GAC allow assembly without strong name
                        System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(DemoInterface).Assembly.Location), "ProcMonInject.dll"), // 32-bit version (the same because AnyCPU)
                        System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(DemoInterface).Assembly.Location), "ProcMonInject.dll"), //"ProcMonInject.dll", // 64-bit version (the same because AnyCPU)
                        // the optional parameter list...
                        ChannelName);
                }
                catch(Exception Msg)
                {
                    HookedProcesses.Remove(PID);

                    e.Item.Checked = false;

                    MessageBox.Show(Msg.Message, "An error has occurred...", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                }
            }
            else
            {
                // was newly unchecked?
                if (!HookedProcesses.Contains(PID))
                    return;

                /*
                 * Uninjection will be done by client-side pinging. Next time the related
                 * client is sending a ping, we will notify him that he should uninject itself.
                 */
                HookedProcesses.Remove(PID);
            }
        }

        private void CHK_IsMonitoring_CheckedChanged(object sender, EventArgs e)
        {
            IsMonitoring = CHK_IsMonitoring.Checked;
        }
    }

    
}
