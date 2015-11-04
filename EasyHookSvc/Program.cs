// EasyHook (File: EasyHookSvc\Program.cs)
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
using System.ServiceProcess;
using System.Text;
using System.Diagnostics;
using System.IO;
using EasyHook;

namespace EasyHookSvc
{
#pragma warning disable 0618

    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(String[] args)
        {
            String ServiceName = Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName);

            if (args.Length >= 3)
            {
                /*
                 * This will provide automated GAC removal...
                 */

                // wait for process exit...
                try
                {
                    Process Proc = Process.GetProcessById(Int32.Parse(args[0]));

                    Proc.WaitForExit();
                }
                catch(Exception Info)
                {
                    Config.PrintError("Unable to wait for host termination...\r\n" + Info.ToString());
                }

                // remove assemblies from GAC...
                try
                {
                    List<string> assemblies = new List<string>();
                    for (int i = 3; i < args.Length; i++)
                    {
                        assemblies.Add(args[i]);
                    }

                    NativeAPI.GacUninstallAssemblies(assemblies.ToArray(), args[2], args[1]);
                }
                catch (Exception Info)
                {
                    Config.PrintError("Unable to cleanup GAC...\r\n" + Info.ToString());
                }
            }
            else if (args.Length == 1)
            {
                /*
                 * This will provide the service interface under admin privileges which
                 * is used to bypass WOW64. Services can't be used because they are only
                 * able to hook services but not applications under latest Vista x64!
                 */

                // we can reuse the service code...
                new InjectionService(ServiceName).OnExecute(args);
            }
            else
            {
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[] 
			    { 
				    new InjectionService(ServiceName) 
			    };

                ServiceBase.Run(ServicesToRun);
            }
        }
    }
}
