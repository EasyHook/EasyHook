// EasyHook (File: EasyHook\HelperServiceInterface.cs)
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
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Reflection;
using System.Threading;

namespace EasyHook
{
    #pragma warning disable 1591
    
    public class HelperServiceInterface : MarshalByRefObject
    {
        public void InjectEx(
                Int32 InHostPID,
                Int32 InTargetPID,
                Int32 InWakeUpTID,
                Int32 InNativeOptions,
                String InLibraryPath_x86,
                String InLibraryPath_x64,
                Boolean InCanBypassWOW64,
                Boolean InCanCreateService,
                Boolean InRequireStrongName,
                params Object[] InPassThruArgs)
        {
            RemoteHooking.InjectEx(
                InHostPID,
                InTargetPID,
                InWakeUpTID,
                InNativeOptions,
                InLibraryPath_x86, 
                InLibraryPath_x64, 
                InCanBypassWOW64,
                InCanCreateService,
                InRequireStrongName,
                InPassThruArgs);
        }

        public Object ExecuteAsService<TClass>(
                String InMethodName,
                Object[] InParams)
        {
            return typeof(TClass).InvokeMember(InMethodName, BindingFlags.InvokeMethod | BindingFlags.Public |
                BindingFlags.Static, null, null, InParams);
        }

        private class InjectionWait
        {
            public Mutex ThreadLock = new Mutex(false);
            public ManualResetEvent Completion = new ManualResetEvent(false);
            public Exception Error = null;
        }

        private static SortedList<Int32, InjectionWait> InjectionList = new SortedList<Int32, InjectionWait>();

        public static void BeginInjection(Int32 InTargetPID)
        {
            InjectionWait WaitInfo;

            lock (InjectionList)
            {
                if (!InjectionList.TryGetValue(InTargetPID, out WaitInfo))
                {
                    WaitInfo = new InjectionWait();

                    InjectionList.Add(InTargetPID, WaitInfo);
                }
            }

            WaitInfo.ThreadLock.WaitOne();
            WaitInfo.Error = null;
            WaitInfo.Completion.Reset();

            lock (InjectionList)
            {
                if (!InjectionList.ContainsKey(InTargetPID))
                    InjectionList.Add(InTargetPID, WaitInfo);
            }
        }

        public static void EndInjection(Int32 InTargetPID)
        {
            lock (InjectionList)
            {
                InjectionList[InTargetPID].ThreadLock.ReleaseMutex();

                InjectionList.Remove(InTargetPID);
            }
        }

        public static void WaitForInjection(Int32 InTargetPID)
        {
            InjectionWait WaitInfo;

            lock (InjectionList)
            {
                WaitInfo = InjectionList[InTargetPID];
            }

            if (!WaitInfo.Completion.WaitOne(20000, false))
                throw new TimeoutException("Unable to wait for injection completion.");

            if (WaitInfo.Error != null)
                throw WaitInfo.Error;
        }

        public void InjectionException(
            Int32 InClientPID,
            Exception e)
        {
            InjectionWait WaitInfo;

            lock (InjectionList)
            {
                WaitInfo = InjectionList[InClientPID];
            }

            WaitInfo.Error = e;
            WaitInfo.Completion.Set();
        }

        public void InjectionCompleted(Int32 InClientPID)
        {
            InjectionWait WaitInfo;

            lock (InjectionList)
            {
                WaitInfo = InjectionList[InClientPID];
            }

            WaitInfo.Error = null;
            WaitInfo.Completion.Set();
        }

        public void Ping() { }
    }
}
