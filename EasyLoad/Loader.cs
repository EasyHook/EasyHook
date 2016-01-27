// EasyLoad (File: EasyLoad\Loader.cs)
//
// Copyright (c) 2015 Justin Stenning
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
using System.Text;
using RGiesecke.DllExport;
using System.Runtime.InteropServices;
using System.Reflection;
using System.IO;

namespace EasyLoad
{
    /// <summary>
    /// Proxy class for calling the EasyHook injection loader within the child AppDomain
    /// </summary>
    public class LoadEasyHookProxy : MarshalByRefObject
    {
        /// <summary>
        /// Call EasyHook.InjectionLoader.Main within the child AppDomain
        /// </summary>
        /// <param name="inParam"></param>
        /// <returns>0 for success, -1 for fail</returns>
        public int Load(String inParam)
        {
            return EasyHook.InjectionLoader.Main(inParam);
        }
    }

    /// <summary>
    /// Static EasyHook Loader class
    /// </summary>
    public static class Loader
    {
        static object _lock = new object();
        static AppDomain _easyHookDomain;
        /// <summary>
        /// Keep track of injections to determine when it is safe to unload the AppDomain
        /// </summary>
        static int _injectCount = 0;

        static Loader()
        {
            // Custom AssemblyResolve is necessary to load assembly from same directory as EasyLoad
            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.AssemblyResolve += new ResolveEventHandler(LoadFromSameFolder);
        }

        /// <summary>
        /// Custom AssemblyResolve is necessary to load assembly from same directory as EasyLoad
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        static Assembly LoadFromSameFolder(object sender, ResolveEventArgs args)
        {
            string folderPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string assemblyPath = Path.Combine(folderPath, new AssemblyName(args.Name).Name + ".dll");
            if (File.Exists(assemblyPath) == false) return null;
            Assembly assembly = Assembly.LoadFrom(assemblyPath);
            return assembly;
        }

        /// <summary>
        /// Loads EasyHook and commences the loading of user supplied assembly. This method is exported as a DllExport, making it consumable from native code.
        /// </summary>
        /// <param name="inParam"></param>
        /// <returns>0 if successfully loaded into new AppDomain, 1 if could not use new AppDomain but successfully loaded into default, or -1 for fail</returns>
        [DllExport("Load", System.Runtime.InteropServices.CallingConvention.StdCall)]
        public static int Load([MarshalAs(UnmanagedType.LPWStr)]String inParam)
        {
            try
            {
                lock (_lock)
                {
                    try
                    {
                        _injectCount++;
                        if (_easyHookDomain == null)
                        {
                            System.Security.PermissionSet ps = new System.Security.PermissionSet(System.Security.Permissions.PermissionState.Unrestricted);
                            ps.AddPermission(new System.Security.Permissions.SecurityPermission(System.Security.Permissions.SecurityPermissionFlag.AllFlags));
                            // Evidence of null means use the current appdomain evidence
                            _easyHookDomain = AppDomain.CreateDomain("EasyHook", null, new AppDomainSetup()
                            {
                                ApplicationBase = Path.GetDirectoryName(typeof(Loader).Assembly.Location),
                                // ShadowCopyFiles = "true", // copies assemblies from ApplicationBase into cache, leaving originals unlocked and updatable
                            }, ps);
                        }
                    }
                    catch (OutOfMemoryException ome)
                    {
                        // Creation of AppDomain failed, so fall back to using default domain (means it cannot be unloaded)
                        
                        // The reason is there could be an issue with the target application's stack commit size.
                        // The default stack commit size must be <= 253952 (or 0x3E000) - due to bug in .NET Framework, 
                        // this can be checked with dumpbin.exe and edited with editbin.exe.

                        // Load EasyHook and the target assembly
                        LoadEasyHookProxy lp = new LoadEasyHookProxy();
                        lp.Load(inParam);
                        return 1;
                    }
                }

                int result = -1;

                // Load the EasyHook assembly in the child AppDomain by using the proxy class
                Type proxyType = typeof(LoadEasyHookProxy);
                if (proxyType.Assembly != null)
                {
                    // This is where the currentDomain.AssemblyResolve that we setup within the static 
                    // constructor is required.
                    var proxy = (LoadEasyHookProxy)_easyHookDomain.CreateInstanceFrom(
                                    proxyType.Assembly.Location,
                                    proxyType.FullName).Unwrap();

                    // Loads EasyHook.dll into the AppDomain, which in turn loads the target assembly
                    result = proxy.Load(inParam);
                }

                //result = new LoadEasyHookProxy().Load(inParam);
                return result;
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("Exception occurred within Loader.Load: " + e.ToString());
            }
            finally
            {
                lock (_lock)
                {
                    _injectCount--;
                }
            }

            // Failed
            return -1;
        }

        /// <summary>
        /// Attempts to terminate the AppDomain that is hosting the injected assembly. This method is exported as a DllExport, making it consumable from native code.
        /// </summary>
        [DllExport("Close", System.Runtime.InteropServices.CallingConvention.StdCall)]
        public static void Close()
        {
            lock (_lock)
            {
                if (_easyHookDomain != null && _injectCount == 0)
                {
                    try
                    {
                        // Unload the AppDomain
                        AppDomain.Unload(_easyHookDomain);
                    }
                    catch (System.CannotUnloadAppDomainException)
                    {
                        // Usually means that one or more threads within the AppDomain haven't finished exiting (e.g. still within a finalize)
                        var i = 0;
                        while (i < 3) // try up to 3 times to unload the AppDomain
                        {
                            System.Threading.Thread.Sleep(1000);
                            try
                            {
                                AppDomain.Unload(_easyHookDomain);
                            }
                            catch
                            {
                                i++;
                                continue;
                            }
                            break;
                        }
                    }
                    _easyHookDomain = null;
                }
            }
        }
    }
}
