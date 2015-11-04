// EasyHook (File: EasyHook\WOW64Bypass.cs)
//
// Copyright (c) 2009 Christoph Husse & Copyright (c) 2015 Justin Stenning
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
// Please visit https://easyhook.github.io for more information
// about the project and latest updates.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.IO;

namespace EasyHook
{
    internal class WOW64Bypass
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
                    String ChannelName = RemoteHooking.GenerateName();
                    String SvcExecutablePath = (Config.DependencyPath.Length > 0 ? Config.DependencyPath : Config.GetProcessPath()) + Config.GetWOW64BypassExecutableName();

                    Process Proc = new Process();
                    ProcessStartInfo StartInfo = new ProcessStartInfo(
                            SvcExecutablePath, "\"" + ChannelName + "\"");

                    // create sync objects
                    EventWaitHandle Listening = new EventWaitHandle(
                        false,
                        EventResetMode.ManualReset,
                        "Global\\Event_" + ChannelName);

                    m_TermMutex = new Mutex(true, "Global\\Mutex_" + ChannelName);

                    // start and connect program
                    StartInfo.CreateNoWindow = true;
                    StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

                    Proc.StartInfo = StartInfo;

                    Proc.Start();

                    if (!Listening.WaitOne(5000, true))
                        throw new ApplicationException("Unable to wait for service application due to timeout.");

                    HelperServiceInterface Interface = RemoteHooking.IpcConnectClient<HelperServiceInterface>(ChannelName);

                    Interface.Ping();

                    m_Interface = Interface;
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
                true,
                InRequireStrongName,
                InPassThruArgs);
        }
    }
}
