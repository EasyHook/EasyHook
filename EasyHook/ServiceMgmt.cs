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
using System.Threading;
using System.IO;
using System.Reflection;

namespace EasyHook
{
    internal class ServiceMgmt
    {
        private static Mutex m_TermMutex = null;
        private static HelperServiceInterface m_Interface = null;
        private static Object ThreadSafe = new Object();

        private static void Install()
        {
            lock (ThreadSafe)
            {
                // Ensure we create a new one if the existing
                // channel cannot be pinged
                try
                {
                    if (m_Interface != null)
                        m_Interface.Ping();
                }
                catch
                {
                    m_Interface = null;
                }

                if (m_Interface == null)
                {
                    // create sync objects
                    String ChannelName = RemoteHooking.GenerateName();
                    EventWaitHandle Listening = new EventWaitHandle(
                        false,
                        EventResetMode.ManualReset,
                        "Global\\Event_" + ChannelName);
                    Mutex TermMutex = new Mutex(true, "Global\\Mutex_" + ChannelName);

                    using (TermMutex)
                    {
                        // install and start service
                        NativeAPI.RtlInstallService(
                            "EasyHook" + (NativeAPI.Is64Bit?"64":"32") + "Svc",
                            Path.GetFullPath(Config.GetDependantSvcExecutableName()),
                            ChannelName);

                        if (!Listening.WaitOne(5000, true))
                            throw new ApplicationException("Unable to wait for service startup.");

                        HelperServiceInterface Interface = RemoteHooking.IpcConnectClient<HelperServiceInterface>(ChannelName);

                        Interface.Ping();

                        // now we can be sure that all things are fine...
                        m_Interface = Interface;
                        m_TermMutex = TermMutex;
                    }
                }
            }
        }

        public static void Inject(
            Int32 InHostPID,
            Int32 InTargetPID,
            Int32 InWakeUpTID,
            Int32 InNativeOptions,
            String InLibraryPath_x86,
            String InLibraryPath_x64,
            Boolean InRequireStrongName,
            params Object[] InPassThruArgs)
        {
            Install();

            m_Interface.InjectEx(
                InHostPID,
                InTargetPID, 
                InWakeUpTID,
                InNativeOptions,
                InLibraryPath_x86, 
                InLibraryPath_x64, 
                false,
                false,
                InRequireStrongName,
                InPassThruArgs);
        }

        public static Object ExecuteAsService<TClass>(
                String InMethodName,
                params Object[] InParams)
        {
            Install();

            return m_Interface.ExecuteAsService<TClass>(InMethodName, InParams);
        }
    }
}
