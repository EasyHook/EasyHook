/*
    EasyHook - The reinvention of Windows API hooking
 
    Copyright (C) 2009 Christoph Husse

    This library is free software; you can redistribute it and/or
    modify it under the terms of the GNU Lesser General Public
    License as published by the Free Software Foundation; either
    version 2.1 of the License, or (at your option) any later version.

    This library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
    Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public
    License along with this library; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301  USA

    Please visit http://www.codeplex.com/easyhook for more information
    about the project and latest updates.
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.IO;
using System.ServiceProcess;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Security.Policy;
using System.Security;
using System.Security.Permissions;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;
using EasyHook;

namespace EasyHookSvc
{
    public partial class InjectionService : ServiceBase
    {

        public InjectionService(String InServiceName)
        {
            InitializeComponent();
            
            ServiceName = InServiceName;
        }

        private void DebugPrint(String InMessage)
        {
#if DEBUG
            this.EventLog.WriteEntry(
                "[EasyHookSvc]: " + InMessage,
                EventLogEntryType.Information);
#endif
        }

        private void LogError(String InMessage)
        {
            this.EventLog.WriteEntry(
                "[EasyHookSvc]: " + InMessage,
                EventLogEntryType.Error);
        }

        protected override void OnStart(string[] args)
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(OnExecute), args);
        }

        public void OnExecute(Object InParam)
        {
            String[] args = (String[])InParam;

            try
            {
                String ChannelName = args[0];
                Mutex TermMutex = Mutex.OpenExisting("Global\\Mutex_" + ChannelName);
                WellKnownSidType SidType = WellKnownSidType.BuiltinAdministratorsSid;

                if (!RemoteHooking.IsAdministrator)
                    SidType = WellKnownSidType.WorldSid;

                IpcServerChannel Channel = RemoteHooking.IpcCreateServer<HelperServiceInterface>(
                                                ref ChannelName,
                                                WellKnownObjectMode.SingleCall,
                                                SidType);

                // signal that service is listening
                EventWaitHandle Listening = EventWaitHandle.OpenExisting("Global\\Event_" + ChannelName);

                Listening.Set();

                try
                {
                    TermMutex.WaitOne();
                }
                catch
                {
                }

                // cleanup library...
                Channel.StopListening(null);
                Channel = null;

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

            }
            catch (Exception e)
            {
                LogError(e.ToString());
            }
            finally
            {
                Stop();
            }
        }

        protected override void OnStop()
        {
        }
    }
}
