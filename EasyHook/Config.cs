// EasyHook (File: EasyHook\Config.cs)
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
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;

namespace EasyHook
{
#pragma warning disable 0618 // obsolete

    /// <summary>
    /// Currently only provides a mechanism to register assemblies in the GAC.
    /// </summary>
    /// <include file='FileMonHost.xml' path='remarks'/>
    public class Config
    {
        private static String dependencyPath = "";

        /// <summary>
        /// The path where dependant files, like EasyHook(32|64)Svc.exe are stored.
        /// Defaults to no path being specified.
        /// </summary>
        public static String DependencyPath
        {
            get
            {
                if (dependencyPath.Length > 0 && !dependencyPath.EndsWith("\\"))
                {
                    dependencyPath += "\\";
                }

                return dependencyPath;
            }
            set
            {
                dependencyPath = value;
            }
        }

        /// <summary>
        /// Get the directory name of the current process, ending with a backslash.
        /// </summary>
        /// <returns>Directory name of the current process</returns>
        public static String GetProcessPath()
        {
            return Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName) + "\\";
        }

        /// <summary>
        /// Get the name of the EasyHook SVC executable.
        /// Automatically determine whether to use the 64-bit or the 32-bit version.
        /// </summary>
        /// <returns>Executable name</returns>
        public static String GetSvcExecutableName()
        {
            return "EasyHook" + (NativeAPI.Is64Bit ? "64" : "32") + "Svc.exe";
        }

        /// <summary>
        /// Get the name of the EasyHook SVC executable to use for WOW64 bypass.
        /// If this process is 64-bit, return the 32-bit service executable and vice versa.
        /// </summary>
        /// <returns></returns>
        public static String GetWOW64BypassExecutableName()
        {
            return "EasyHook" + (NativeAPI.Is64Bit ? "32" : "64") + "Svc.exe";
        }

        /// <summary>
        /// Get the EasyHook SVC executable name with the custom dependency path prepended.
        /// </summary>
        /// <returns>Full path to the executable</returns>
        public static String GetDependantSvcExecutableName()
        {
            return DependencyPath + GetSvcExecutableName();
        }

        /// <summary>
        /// REQUIRES ADMIN PRIVILEGES. Installs EasyHook and all given user NET assemblies into the GAC and
        /// ensures that all references are cleaned up if the installing application
        /// is shutdown. Cleanup does not depend on the calling application...
        /// </summary>
        /// <remarks>
        /// <para>
        /// ATTENTION: There are some problems when debugging processes whose libraries
        /// are added to the GAC. Visual Studio won't start the debug session! There is
        /// only one chance for you to workaround this issue if you want to install
        /// libraries AND debug them simultanously. This is simply to debug only one process
        /// which is the default setting of Visual Studio. Because the libraries are added
        /// to the GAC AFTER Visual Studio has initialized the debug session, there won't be
        /// any conflicts; at least so far...
        /// </para><para>
        /// In debug versions of EasyHook, you may also check the "Application" event log, which holds additional information
        /// about the GAC registration, after calling this method. In general this method works
        /// transactionally. This means if something goes wrong, the GAC state of all related libraries
        /// won't be violated!
        /// </para><para>
        /// The problem with NET assemblies is that the CLR only searches the GAC and
        /// directories starting with the application base directory for assemblies.
        /// To get injected assemblies working either all of them have to be located 
        /// under the target base directory (which is not suitable in most cases) or
        /// reside in the GAC. 
        /// </para><para>
        /// EasyHook provides a way to automatically register all of its own assemblies
        /// and custom ones temporarily in the GAC. It also ensures
        /// that all of these assemblies are removed if the installing process exists. 
        /// So you don't need to care about and may write applications according to
        /// the XCOPY standard. If your application ships with an installer, you may
        /// statically install all of your assemblies and the ones of EasyHook into the
        /// GAC. In this case just don't call <see cref="Register"/>. 
        /// </para><para>
        /// Of course EasyHook does also take care of multiple processes using the same
        /// injection libraries. So if two processes are sharing some of those DLLs,
        /// a stable reference counter ensures that the libraries are kept in the GAC
        /// if one process is terminated while the other continues running and so continues
        /// holding a proper GAC reference.
        /// </para><para>
        /// Please note that in order to add your library to the GAC, it has to be a valid
        /// NET assembly and expose a so called "Strong Name". Assemblies without a strong
        /// name will be rejected by this method!
        /// </para>
        /// </remarks>
        /// <param name="InDescription">
        /// A description under which the installed files should be referenced. 
        /// </param>
        /// <param name="InUserAssemblies">
        /// A list of user assemblies as relative or absolute paths. 
        /// </param>
        /// <exception cref="System.IO.FileNotFoundException">
        /// At least one of the files specified could not be found!
        /// </exception>
        /// <exception cref="BadImageFormatException">
        /// Unable to load at least one of the given files for reflection. 
        /// </exception>
        /// <exception cref="ArgumentException">
        /// At least one of the given files does not have a strong name.
        /// </exception>
        public static void Register(
            String InDescription,
            params String[] InUserAssemblies)
        {
            List<Assembly> AsmList = new List<Assembly>();
            String RemovalList = "";
            List<String> InstallList = new List<String>();

            /*
             * Read and validate config file...
             */
            List<String> Files = new List<string>();

            Files.Add(typeof(Config).Assembly.Location);

            Files.AddRange(InUserAssemblies);

            for (int i = 0; i < Files.Count; i++)
            {
                Assembly Entry;
                String AsmPath = Path.GetFullPath(Files[i]);

                if (!File.Exists(AsmPath))
                    throw new System.IO.FileNotFoundException("The given assembly \"" + Files[i] + "\" (\"" + AsmPath + "\") does not exist.");

                // just validate that this is a NET assembly with valid metadata...
                try { Entry = Assembly.ReflectionOnlyLoadFrom(AsmPath); }
                catch (Exception ExtInfo)
                {
                    throw new BadImageFormatException("Unable to load given assembly \"" + Files[i] + "\" (\"" + AsmPath +
                        "\") for reflection. Is this a valid NET assembly?", ExtInfo);
                }

                // is strongly named? (required for GAC)
                AssemblyName Name = AssemblyName.GetAssemblyName(AsmPath);

                if ((Name.Flags & AssemblyNameFlags.PublicKey) == 0)
                    throw new ArgumentException("The given user library \"" + Files[i] + "\" is not strongly named!");

                AsmList.Add(Entry);
                InstallList.Add(AsmPath);

                RemovalList += " \"" + Name.Name + "\"";
            }

            /*
             * Install assemblies into GAC ...
             */

            // create unique installation identifier
            Byte[] IdentData = new Byte[30];

            new RNGCryptoServiceProvider().GetBytes(IdentData);

            // run cleanup service
            InDescription = InDescription.Replace('"', '\'');

            Config.RunCommand(
                "GACRemover", false, false, Config.GetDependantSvcExecutableName(), Process.GetCurrentProcess().Id.ToString() + " \"" +
                    Convert.ToBase64String(IdentData) + "\" \"" + InDescription + "\"" + RemovalList);

            // install assemblies
            NativeAPI.GacInstallAssemblies(
                    InstallList.ToArray(),
                    InDescription,
                    Convert.ToBase64String(IdentData));

        }

#pragma warning disable 1591

        [Obsolete("This method is exported for internal use only.")]
        public static void PrintError(String InMessage, params object[] InParams)
        {
            DebugPrint(EventLogEntryType.Error, InMessage, InParams);
        }

        [Obsolete("This method is exported for internal use only.")]
        public static void PrintWarning(String InMessage, params object[] InParams)
        {
            DebugPrint(EventLogEntryType.Warning, InMessage, InParams);
        }

        [Obsolete("This method is exported for internal use only.")]
        public static void PrintComment(String InMessage, params object[] InParams)
        {
            DebugPrint(EventLogEntryType.Information, InMessage, InParams);
        }

        [Obsolete("This method is exported for internal use only.")]
        public static void DebugPrint(EventLogEntryType InType, String InMessage, params object[] InParams)
        {
            String Entry = String.Format(InMessage, InParams);

            switch (InType)
            {
                case EventLogEntryType.Error: Entry = "[error]: " + Entry; break;
                case EventLogEntryType.Information: Entry = "[comment]: " + Entry; break;
                case EventLogEntryType.Warning: Entry = "[warning]: " + Entry; break;
            }

            try
            {
                try
                {
                    // Attempt to create the Event Source
                    if (!EventLog.SourceExists("EasyHook"))
                    {
                        EventLog.CreateEventSource("EasyHook", "Application");
                    }
                }
                catch
                {
                }
                {
#if !DEBUG
                if(InType == EventLogEntryType.Error)
#endif
                    EventLog.WriteEntry("EasyHook", Entry, InType);
                }
            }
            catch
            {
            }

#if DEBUG
            Console.WriteLine(Entry);
#endif
        }

        [Obsolete("This method is exported for internal use only.")]
        public static void RunCommand(
            String InFriendlyName,
            Boolean InWaitForExit,
            Boolean InShellExecute,
            String InPath,
            String InArguments)
        {
            InPath = Path.GetFullPath(InPath);

            Process Proc = new Process();
            ProcessStartInfo StartInfo = new ProcessStartInfo(InPath, InArguments);

            if (InShellExecute && InWaitForExit)
                throw new ArgumentException("It is not supported to execute in shell and wait for termination!");

            StartInfo.RedirectStandardOutput = !InShellExecute;
            StartInfo.UseShellExecute = InShellExecute;
            StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            StartInfo.CreateNoWindow = true;
            StartInfo.WorkingDirectory = Path.GetDirectoryName(InPath);

            Proc.StartInfo = StartInfo;

            try
            {
                if (!Proc.Start())
                    throw new Exception();
            }
            catch (Exception ExtInfo)
            {
                throw new ApplicationException("Unable to run internal command.", ExtInfo);
            }

            if (InWaitForExit)
            {
                if (!Proc.WaitForExit(5000))
                {
                    Proc.Kill();

                    throw new ApplicationException("Unable to run internal command.");
                }

                // save to log entry
                String Output = Proc.StandardOutput.ReadToEnd();

                PrintComment("[" + InFriendlyName + "]: " + Output);
            }
        }

#pragma warning restore 1591

    }
}
