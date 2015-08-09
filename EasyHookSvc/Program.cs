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
