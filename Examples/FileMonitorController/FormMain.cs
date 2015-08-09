using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels.Ipc;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;
using EasyHook;
using FileMonitorInterface;

namespace FileMonitorController
{
    public partial class FormMain : Form
    {
        private IpcInterface IpcInterface { get; set; }
        private List<ProcessEntry> ProcessEntries { get; set; }
        private string channelName = String.Format("FileMonitorControll - {0} {1}", DateTime.Now.ToLongDateString(), DateTime.Now.ToLongTimeString());
        private IpcServerChannel channel;

        public FormMain()
        {
            InitializeComponent();

            //  Prevent ListView flickering
            ListViewHelper.EnableDoubleBuffer(ListViewLog);
            ListViewHelper.EnableDoubleBuffer(ListViewProcesses);

            IpcInterface = new IpcInterface();
            IpcInterface.OnMessagePosted += IpcInterface_OnMessagePosted;
            ProcessEntries = new List<ProcessEntry>();
        }

        void IpcInterface_OnMessagePosted(object sender, MessageEventArgs e)
        {
            MessageBox.Show(e.Message);
        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            channel = RemoteHooking.IpcCreateServer(
                ref channelName,
                WellKnownObjectMode.Singleton,
                IpcInterface,
                WellKnownSidType.WorldSid);
        }

        private void PushFileEntryEvent(ProcessEntry whichProcess, List<FileEntry> fileEntry)
        {
            fileEntry.ForEach((entry) => this.ListViewLog.Items.Add(new ListViewItem(new[]
                {
                    whichProcess.ImageName,
                    entry.Timestamp.ToLongTimeString(),
                    entry.FullPath
                })));
        }

        private void Timer_UpdateMonitorLog_Tick(object sender, EventArgs e)
        {
            var selectedIndices = this.ListViewProcesses.SelectedIndices;

            if (selectedIndices.Count == 0)
            {
                return; /* There are no selected processes to view */
            }

            this.Timer_UpdateMonitorLog.Enabled = false;

            this.ListViewLog.Items.Clear();

            foreach (int index in selectedIndices)
            {
                var associatedProcess = ProcessEntries[index];

                foreach (var processId in IpcInterface.FileEntries.Keys)
                {
                    if (processId == associatedProcess.Id)
                    {
                        PushFileEntryEvent(associatedProcess, IpcInterface.FileEntries[processId]);
                    }
                }
            }

            this.Timer_UpdateMonitorLog.Enabled = true;
        }

        private void UpdateAvailableProcesses()
        {
            // Remember which item is currently selected, such that we can restore the scrollbar position
            var selectedIndicies = this.ListViewProcesses.SelectedIndices;
            var lastSelectedIndex = selectedIndicies.Count > 0 ? selectedIndicies[selectedIndicies.Count - 1] : 0;

            this.ProcessEntries.Clear();
            this.ProcessEntries.AddRange((ProcessEntry[]) RemoteHooking.ExecuteAsService<FormMain>("EnumerateProcesses"));

            this.ListViewProcesses.BeginUpdate();
            this.ListViewProcesses.Items.Clear();

            foreach (var process in this.ProcessEntries)
            {
                this.ListViewProcesses.Items.Add(new ListViewItem(new[]
                    {
                        process.Id.ToString(),
                        process.IsX64 ? "64-bit" : "32-bit",
                        process.ImageName,  
                        process.Owner,
                        process.FullPath
                    }));
            }

            // Restore the scrollbar position
            var realLastIndex = (this.ListViewProcesses.Items.Count >= lastSelectedIndex)
                                    ? lastSelectedIndex
                                    : this.ListViewProcesses.Items.Count - 1;
            this.ListViewProcesses.EnsureVisible(realLastIndex);
            this.ListViewProcesses.EndUpdate();
        }

        public static ProcessEntry[] EnumerateProcesses()
        {
            var processEntries = new List<ProcessEntry>();

            var processes = Process.GetProcesses();

            foreach (var process in processes)
            {
                try
                {
                    processEntries.Add(new ProcessEntry()
                        {
                            FullPath = process.MainModule.FileName,
                            ImageName = process.ProcessName,
                            Id = process.Id,
                            IsX64 = RemoteHooking.IsX64Process(process.Id),
                            Owner = RemoteHooking.GetProcessIdentity(process.Id).Name
                        });
                }
                catch
                {
                    // Access will be denied to some processes even though we're running as a service
                    continue;
                }
            }

            return processEntries.ToArray();
        }

        private void ListViewProcesses_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            var process = ProcessEntries[e.Item.Index];

            if (e.Item.Checked)
            {
                try
                {
                    //hookedProcessIds.Add(process.Id);
                    RemoteHooking.Inject(process.Id, 
                        "FileMonitorInterceptor.dll", /* the 32-bit dll */
                        "FileMonitorInterceptor.dll", /* the 64-bit dll ; notice both are identical because the dll is compiled as AnyCPU */
                        channelName); /* the optional parameter list ; the length and type of which must be identical to FileMonitorInterceptor's ctor() and Run() */
                }
                catch (Exception ex)
                {
                    //hookedProcessIds.Remove(processId);
                    e.Item.Checked = false;
                    MessageBox.Show(ex.ToString(), "Error Injecting to Process", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                IpcInterface.TerminatingProcesses.Add(process.Id);
            }
        }

        private void ButtonRefreshList_Click(object sender, EventArgs e)
        {
            UpdateAvailableProcesses();
        }

        private void ButtonClearEventLog_Click(object sender, EventArgs e)
        {
            this.ListViewLog.Items.Clear();
        }
    }
}
