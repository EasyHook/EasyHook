// EasyHook (File: EasyHook\COMClassInfo.cs)
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
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace EasyHook
{
    /// <summary>
    /// <para>A helper class for determining the address of COM object functions for hooking given a COM class id (CLSID) and COM interface id (IID), or COM class type and COM interface type.</para>
    /// </summary>
    /// <example>
    /// The following three examples result in the same output:
    /// <code>
    /// // 1. Use imported Class and Interface Types
    /// COMClassInfo cci1 = new COMClassInfo(typeof(CLSID_DirectInputDevice8), typeof(IID_IDirectInputDevice8W), "GetCapabilities");
    /// // 2. Use Guid from class and interface types
    /// COMClassInfo cci2 = new COMClassInfo(typeof(CLSID_DirectInputDevice8).GUID, typeof(IID_IDirectInputDevice8W).GUID, 3);
    /// // 3. Use class and interface Guids directly (no need to have class and interface types defined)
    /// COMClassInfo cci3 = new COMClassInfo(new Guid("25E609E5-B259-11CF-BFC7-444553540000"), new Guid("54D41081-DC15-4833-A41B-748F73A38179"), 3);
    /// 
    /// // Will output False if dinput8.dll is not already loaded
    /// Console.WriteLine(cci1.IsModuleLoaded());
    /// cci1.Query();
    /// cci2.Query();
    /// cci3.Query();
    /// // Will output True as dinput8.dll will be loaded by .Query() if not already
    /// Console.WriteLine(cci1.IsModuleLoaded());
    /// 
    /// // Output the function pointers we queried
    /// Console.WriteLine(cci1.FunctionPointers[0]);
    /// Console.WriteLine(cci2.FunctionPointers[0]);
    /// Console.WriteLine(cci3.FunctionPointers[0]);
    /// 
    /// ...
    /// 
    /// [ComVisible(true)]
    /// [Guid("25E609E5-B259-11CF-BFC7-444553540000")]
    /// public class CLSID_DirectInputDevice8
    /// {
    /// }
    /// 
    /// [ComVisible(true)]
    /// [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    /// [Guid("54D41081-DC15-4833-A41B-748F73A38179")]
    /// public interface IID_IDirectInputDevice8W
    /// {
    ///     /*** IDirectInputDevice8W methods ***/
    ///     int GetCapabilities(IntPtr deviceCaps); // fourth method due to IUnknown methods QueryInterface, AddRef and Release
    ///     // other methods...
    /// }
    /// </code>
    /// </example>
    public class COMClassInfo
    {
        /// <summary>
        /// Creates a new COMClassInfo using the COM class and interface types. The function names to retrieve the addresses for should be provided as strings
        /// </summary>
        /// <param name="classtype">The COM object's class type</param>
        /// <param name="interfacetype">The COM object's interface type</param>
        /// <param name="methodNames">The methods to retrieve addresses for</param>
        public COMClassInfo(Type classtype, Type interfacetype, params string[] methodNames)
        {
            if (methodNames == null || methodNames.Length == 0)
                throw new ArgumentException("At least one method name must be provided", "methodNames");

            ClassType = classtype;
            InterfaceType = interfacetype;
            Methods = new MethodInfo[methodNames.Length];
            MethodPointers = new IntPtr[methodNames.Length];

            for (var i = 0; i < methodNames.Length; i++)
                Methods[i] = InterfaceType.GetMethod(methodNames[i]);
        }

        /// <summary>
        /// Creates a new COMClassInfo instance using the COM class and interface Guids. The function indexes to retrieve the addresses for as defined by the order of the methods in the COM interface.
        /// </summary>
        /// <param name="clsid">The class id (CLSID) of the COM object</param>
        /// <param name="iid">The interface id (IID) of the COM interface. This interface MUST inherit from IUnknown.</param>
        /// <param name="vTableIndexes">One or more method indexes to retrieve the address for. Index 0 == QueryInterface, 1 == AddRef, 2 == Release, 3 == first method and so on, i.e. the order that the methods appear in the interface's C++ header file.</param>
        public COMClassInfo(Guid clsid, Guid iid, params int[] vTableIndexes)
        {
            if (vTableIndexes == null || vTableIndexes.Length == 0)
                throw new ArgumentException("At least one VTable index must be provided", "vTableIndexes");

            ClassId = clsid;
            InterfaceId = iid;
            VTableIndexes = vTableIndexes;
            MethodPointers = new IntPtr[vTableIndexes.Length];
        }

        #region CLSID / IID / VTable indexes approach
        private Guid ClassId { get; set; }
        private Guid InterfaceId { get; set; }
        private int[] VTableIndexes { get; set; }
        #endregion

        #region ClassType / InterfaceType / Method names approach

        private Type ClassType { get; set; }
        private Type InterfaceType { get; set; }
        private MethodInfo[] Methods { get; set; }

        #endregion

        /// <summary>
        /// Will contain the method addresses after a call to <see cref="Query"/>. The index corresponds to the order method names / indexes are passed into <see cref="COMClassInfo(Type,Type,string[])"/> or <see cref="COMClassInfo(Guid,Guid,int[])"/>.
        /// </summary>
        public IntPtr[] MethodPointers { get; private set; }

        internal System.Diagnostics.ProcessModule m_ModuleHandle;
        /// <summary>
        /// Retrieve the module for the COM class. Only available after a call to <see cref="Query"/>.
        /// </summary>
        public System.Diagnostics.ProcessModule LibraryOfFunction
        {
            get
            {
                if (m_ModuleHandle == null)
                {
                    DetermineModuleHandle();
                }
                return m_ModuleHandle;
            }
        }

        internal void DetermineModuleHandle()
        {
            System.Diagnostics.Process pr = System.Diagnostics.Process.GetCurrentProcess();
            foreach (System.Diagnostics.ProcessModule pm in pr.Modules)
            {
                if (MethodPointers[0].ToInt64() >= pm.BaseAddress.ToInt64() &&
                    MethodPointers[0].ToInt64() <= pm.BaseAddress.ToInt64() + pm.ModuleMemorySize)
                {
                    m_ModuleHandle = pm;
                    return;
                }
            }
            m_ModuleHandle = null;
        }

        /// <summary>
        /// Query the COM class for the specified method addresses. If not already loaded the COM module will be loaded.
        /// </summary>
        /// <returns>True if the COM class exists, False otherwise.</returns>
        /// <exception cref="AccessViolationException">Thrown if the method index extends beyond the interface and into protected memory.</exception>
        /// <exception cref="ArgumentException">If the provided interface type is not an interface, or the class type is not visible to COM.</exception>
        /// <exception cref="InvalidCastException">Thrown if the class instance does not support the specified interface.</exception>
        [System.Security.Permissions.RegistryPermission(System.Security.Permissions.SecurityAction.Assert)]
        [System.Security.Permissions.SecurityPermission(System.Security.Permissions.SecurityAction.Assert, UnmanagedCode = true)]
        public unsafe bool Query()
        {
            Guid classGuid = Guid.Empty;
            Guid interfaceGuid = Guid.Empty;
            Guid classFactoryGuid = typeof(IClassFactory).GUID;
            Guid classFactory2Guid = typeof(IClassFactory2).GUID;
            int[] vTableOffsets = null;
            object classInstance = null;

            if (ClassType != null)
            {
                classGuid = ClassType.GUID;
                interfaceGuid = InterfaceType.GUID;
                // get com-slot-number (vtable-index) of function X
                vTableOffsets = new int[MethodPointers.Length];
                for (var i = 0; i < Methods.Length; i++)
                    vTableOffsets[i] = Marshal.GetComSlotForMethodInfo(Methods[i]);
            }
            else
            {
                classGuid = ClassId;
                interfaceGuid = InterfaceId;
                // get com-slot-number (vtable-index) of function N
                vTableOffsets = VTableIndexes;
            }

            classInstance = GetClassInstance(classGuid, interfaceGuid, classFactoryGuid, classFactory2Guid);
            if (classInstance == null)
                return false;

            IntPtr interfaceIntPtr = IntPtr.Zero;
            if (InterfaceType != null)
                interfaceIntPtr = Marshal.GetComInterfaceForObject(classInstance, InterfaceType);
            else
            {
                interfaceIntPtr = Marshal.GetIUnknownForObject(classInstance);
                Marshal.QueryInterface(interfaceIntPtr, ref interfaceGuid, out interfaceIntPtr);
            }

            try
            {
                int*** interfaceRawPtr = (int***)interfaceIntPtr.ToPointer();
                // get vtable
                int** vTable = *interfaceRawPtr;
                // get function-addresses from vtable
                for (var i = 0; i < vTableOffsets.Length; i++)
                {
                    int* faddr = vTable[vTableOffsets[i]];
                    MethodPointers[i] = new IntPtr(faddr);
                }
            }
            finally
            {
                // release intptr
                if (interfaceIntPtr != IntPtr.Zero)
                    Marshal.Release(interfaceIntPtr);
                Marshal.FinalReleaseComObject(classInstance);
            }

            return true;
        }

        /// <summary>
        /// Determines if the module containing the COM class is already loaded
        /// </summary>
        /// <returns>True if the module is loaded, otherwise False</returns>
        [System.Security.Permissions.RegistryPermission(System.Security.Permissions.SecurityAction.Assert)]
        [System.Security.Permissions.SecurityPermission(System.Security.Permissions.SecurityAction.Assert, UnmanagedCode = true)]
        public bool IsModuleLoaded()
        {
            Guid classGuid = Guid.Empty;

            if (ClassType != null)
                classGuid = ClassType.GUID;
            else
                classGuid = ClassId;

            Microsoft.Win32.RegistryKey rk = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey("CLSID\\{" + classGuid.ToString() + "}\\InprocServer32");
            if (rk == null)
                return false;
            string classdllname = rk.GetValue(null).ToString();
            IntPtr libH = KERNEL32.GetModuleHandle(classdllname);
            if (libH == IntPtr.Zero)
                return false;

            return true;
        }

        private delegate int DllGetClassObjectDelegate(ref Guid ClassId, ref Guid InterfaceId, [Out, MarshalAs(UnmanagedType.Interface)] out object ppunk);

        [System.Security.Permissions.RegistryPermission(System.Security.Permissions.SecurityAction.Assert)]
        [System.Security.Permissions.SecurityPermission(System.Security.Permissions.SecurityAction.Assert, UnmanagedCode = true)]
        private object GetClassInstance(Guid classguid, Guid interfguid, Guid classfactoryguid, Guid classfactory2guid)
        {
            object classinstance = null;

            // create instance via raw dll functions
            // ensures we get the real vtable
            try
            {
                if (classinstance == null)
                {
                    // Single do..while loop - to support "break;"
                    do
                    {
                        Microsoft.Win32.RegistryKey rk = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey("CLSID\\{" + classguid.ToString() + "}\\InprocServer32");
                        if (rk == null)
                            break;
                        string classdllname = rk.GetValue(null).ToString();
                        IntPtr libH = KERNEL32.LoadLibrary(classdllname);
                        if (libH == IntPtr.Zero)
                            break;
                        IntPtr factoryFunc = KERNEL32.GetProcAddress(libH, "DllGetClassObject");
                        if (factoryFunc == IntPtr.Zero)
                            break;
                        DllGetClassObjectDelegate factoryDel = (DllGetClassObjectDelegate)Marshal.GetDelegateForFunctionPointer(factoryFunc, typeof(DllGetClassObjectDelegate));
                        object classfactoryO;
                        // Try with IClassFactory first
                        factoryDel(ref classguid, ref classfactoryguid, out classfactoryO);
                        if (classfactoryO != null)
                        {
                            IClassFactory classfactory = (IClassFactory)classfactoryO;
                            classfactory.CreateInstance(null, ref interfguid, out classinstance);
                            Marshal.FinalReleaseComObject(classfactory);
                        }
                        else
                        {
                            // Now try with IClassFactory2
                            factoryDel(ref classguid, ref classfactory2guid, out classfactoryO);
                            if (classfactoryO == null)
                                break;
                            IClassFactory2 classfactory = (IClassFactory2)classfactoryO;
                            classinstance = classfactory.CreateInstance(null, interfguid);
                            Marshal.FinalReleaseComObject(classfactory);
                        }
                    } while (false);
                }
            }
            catch { }

            return classinstance;
        }

        private static class KERNEL32
        {
            [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true)]
            public static extern IntPtr LoadLibrary(string lpFileName);

            [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true)]
            public static extern IntPtr GetModuleHandle(string lpModuleName);

            [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
            public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
        }
    }

    #region IClassFactory / IClassFactory2
    [ComImport()]
    [Guid("00000001-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IClassFactory
    {
        [return: MarshalAs(UnmanagedType.I4)]
        [PreserveSig]
        int CreateInstance(
            [In, MarshalAs(UnmanagedType.Interface)] object pUnkOuter,
            ref Guid riid,
            [Out, MarshalAs(UnmanagedType.Interface)] out object obj);

        [return: MarshalAs(UnmanagedType.I4)]
        [PreserveSig]
        int LockServer(
            [In] bool fLock);
    }

    [ComImport()]
    [Guid("B196B28F-BAB4-101A-B69C-00AA00341D07")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IClassFactory2
    {
        [return: MarshalAs(UnmanagedType.Interface)]
        Object CreateInstance(
          [In, MarshalAs(UnmanagedType.Interface)] Object unused,
          [In, MarshalAs(UnmanagedType.LPStruct)] Guid iid);

        void LockServer(Int32 fLock);

        IntPtr GetLicInfo(); // TODO : an enum called LICINFO

        [return: MarshalAs(UnmanagedType.BStr)]
        String RequestLicKey(
          [In, MarshalAs(UnmanagedType.U4)] int reserved);

        [return: MarshalAs(UnmanagedType.Interface)]
        Object CreateInstanceLic(
          [In, MarshalAs(UnmanagedType.Interface)] object pUnkOuter,
          [In, MarshalAs(UnmanagedType.Interface)] object pUnkReserved,
          [In, MarshalAs(UnmanagedType.LPStruct)] Guid iid,
          [In, MarshalAs(UnmanagedType.BStr)] string bstrKey);
    }
    #endregion
}
