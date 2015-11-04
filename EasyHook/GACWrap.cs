// EasyHook (File: EasyHook\GACWrap.cs)
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

//-------------------------------------------------------------
// GACWrap.cs
//
// This implements managed wrappers to GAC API Interfaces
// Adapted from: http://blogs.msdn.com/b/junfeng/archive/2004/09/14/229649.aspx
// Other fusion API documentation: https://support.microsoft.com/en-us/kb/317540
//-------------------------------------------------------------

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace System.GACManagedAccess
{
    #region Interfaces defined by fusion
    //-------------------------------------------------------------
    // Interfaces defined by fusion
    //-------------------------------------------------------------

    /// <summary>
    /// The IAssemblyCache interface is the top-level interface that provides access to the GAC.
    /// </summary>
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("e707dcde-d1cd-11d2-bab9-00c04f8eceae")]
    internal interface IAssemblyCache
    {
        [PreserveSig()]
        int UninstallAssembly(
                            int flags,
                            [MarshalAs(UnmanagedType.LPWStr)]
                            String assemblyName,
                            InstallReference refData,
                            out AssemblyCacheUninstallDisposition disposition);

        [PreserveSig()]
        int QueryAssemblyInfo(
                            int flags,
                            [MarshalAs(UnmanagedType.LPWStr)]
                            String assemblyName,
                            ref AssemblyInfo assemblyInfo);
        [PreserveSig()]
        int Reserved(
                            int flags,
                            IntPtr pvReserved,
                            out Object ppAsmItem,
                            [MarshalAs(UnmanagedType.LPWStr)]
                            String assemblyName);
        [PreserveSig()]
        int Reserved(out Object ppAsmScavenger);

        [PreserveSig()]
        int InstallAssembly(
                            int flags,
                            [MarshalAs(UnmanagedType.LPWStr)]
                            String assemblyFilePath,
                            InstallReference refData);
    }// IAssemblyCache

    /// <summary>
    /// Represents an assembly name. An assembly name includes a predetermined set of name-value pairs. The assembly name is described in detail in the .NET Framework SDK.
    /// </summary>
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("CD193BC0-B4BC-11d2-9833-00C04FC31D2E")]
    internal interface IAssemblyName
    {
        /// <summary>
        /// The IAssemblyName::SetProperty method adds a name-value pair to the assembly name, or, if a name-value pair with the same name already exists, modifies or deletes the value of a name-value pair.
        /// </summary>
        /// <param name="PropertyId">The ID that represents the name part of the name-value pair that is to be added or to be modified. Valid property IDs are defined in the ASM_NAME enumeration.</param>
        /// <param name="pvProperty">A pointer to a buffer that contains the value of the property.</param>
        /// <param name="cbProperty">The length of the pvProperty buffer in bytes. If cbProperty is zero, the name-value pair is removed from the assembly name.</param>
        /// <returns></returns>
        [PreserveSig()]
        int SetProperty(
                AssemblyNamePropertyIds PropertyId,
                IntPtr pvProperty,
                int cbProperty);

        /// <summary>
        /// The IAssemblyName::GetProperty method retrieves the value of a name-value pair in the assembly name that specifies the name.
        /// </summary>
        /// <param name="PropertyId">The ID that represents the name of the name-value pair whose value is to be retrieved. Specified property IDs are defined in the ASM_NAME enumeration.</param>
        /// <param name="pvProperty">A pointer to a buffer that is to contain the value of the property.</param>
        /// <param name="pcbProperty">The length of the pvProperty buffer, in bytes.</param>
        /// <returns></returns>
        [PreserveSig()]
        int GetProperty(
                AssemblyNamePropertyIds PropertyId,
                IntPtr pvProperty,
                ref int pcbProperty);

        /// <summary>
        /// The IAssemblyName::Finalize method freezes an assembly name. Additional calls to IAssemblyName::SetProperty are unsuccessful after this method has been called.
        /// </summary>
        /// <returns></returns>
        [PreserveSig()]
        int Finalize();

        /// <summary>
        /// The IAssemblyName::GetDisplayName method returns a string representation of the assembly name.
        /// </summary>
        /// <param name="pDisplayName">A pointer to a buffer that is to contain the display name. The display name is returned in Unicode.</param>
        /// <param name="pccDisplayName">The size of the buffer in characters (on input). The length of the returned display name (on return).</param>
        /// <param name="displayFlags">One or more of the bits defined in the ASM_DISPLAY_FLAGS enumeration: <see cref="AssemblyNameDisplayFlags"/></param>
        /// <returns></returns>
        [PreserveSig()]
        int GetDisplayName(
                StringBuilder pDisplayName,
                ref int pccDisplayName,
                int displayFlags);

        [PreserveSig()]
        int Reserved(ref Guid guid,
            Object obj1,
            Object obj2,
            String string1,
            Int64 llFlags,
            IntPtr pvReserved,
            int cbReserved,
            out IntPtr ppv);

        [PreserveSig()]
        int GetName(
                ref int pccBuffer,
                StringBuilder pwzName);

        [PreserveSig()]
        int GetVersion(
                out int versionHi,
                out int versionLow);
        [PreserveSig()]
        int IsEqual(
                IAssemblyName pAsmName,
                int cmpFlags);

        [PreserveSig()]
        int Clone(out IAssemblyName pAsmName);
    }// IAssemblyName

    /// <summary>
    /// Enumerates the assemblies in the GAC.
    /// </summary>
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("21b8916c-f28e-11d2-a473-00c04f8ef448")]
    internal interface IAssemblyEnum
    {
        /// <summary>
        /// Enumerates the assemblies in the GAC.
        /// </summary>
        /// <param name="pvReserved">Must be null</param>
        /// <param name="ppName">Pointer to a memory location that is to receive the interface pointer to the assembly name of the next assembly that is enumerated.</param>
        /// <param name="flags">Must be zero.</param>
        /// <returns></returns>
        [PreserveSig()]
        int GetNextAssembly(
                IntPtr pvReserved,
                out IAssemblyName ppName,
                int flags);
        [PreserveSig()]
        int Reset();
        [PreserveSig()]
        int Clone(out IAssemblyEnum ppEnum);
    }// IAssemblyEnum

    /// <summary>
    /// The IInstallReferenceItem interface represents a reference that has been set on an assembly in the GAC. Instances of IInstallReferenceIteam are returned by the <see cref="IInstallReferenceEnum"/> interface.
    /// </summary>
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("582dac66-e678-449f-aba6-6faaec8a9394")]
    internal interface IInstallReferenceItem
    {
        /// <summary>
        /// Returns a FUSION_INSTALL_REFERENCE structure, <see cref="InstallReference"/>.
        /// </summary>
        /// <param name="pRefData">A pointer to a FUSION_INSTALL_REFERENCE structure. 
        /// The memory is allocated by the GetReference method and is freed when 
        /// <see cref="IInstallReferenceItem"/> is released. Callers must not hold a reference to this 
        /// buffer after the IInstallReferenceItem object is released. 
        /// To avoid allocation issues with the interop layer, the <see cref="InstallReference"/> is not marshaled directly - therefore use of out IntPtr</param>
        /// <param name="flags"></param>
        /// <param name="pvReserced"></param>
        /// <returns></returns>
        [PreserveSig()]
        int GetReference(
                out IntPtr pRefData,
                int flags,
                IntPtr pvReserced);
    }// IInstallReferenceItem

    /// <summary>
    /// <para>The IInstllReferenceEnum interface enumerates all references that are set on an assembly in the GAC.</para>
    /// <para>Note: references that belong to the assembly are locked for changes while those references are being enumerated.</para>
    /// </summary>
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("56b1a988-7c0c-4aa2-8639-c3eb5a90226f")]
    internal interface IInstallReferenceEnum
    {
        /// <summary>
        /// Returns the next reference information for an assembly
        /// </summary>
        /// <param name="ppRefItem">Pointer to a memory location that receives the IInstallReferenceItem pointer.</param>
        /// <param name="flags">Must be zero.</param>
        /// <param name="pvReserced">Must be null.</param>
        /// <returns>Return values are as follows: S_OK: - The next item is returned successfully. S_FALSE: - No more items.</returns>
        [PreserveSig()]
        int GetNextInstallReferenceItem(
                out IInstallReferenceItem ppRefItem,
                int flags,
                IntPtr pvReserced);
    }// IInstallReferenceEnum

    #region Enumerations
    /// <summary>
    /// Install assembly commit flags (mutually exclusive)
    /// </summary>
    public enum AssemblyCommitFlags
    {
        /// <summary>
        /// If the assembly is already installed in the gAC and the file version numbers of the assembly being installed are the same or later, the files are replaced.
        /// </summary>
        Default = 1,
        /// <summary>
        /// The files of an existing assembly are overwritten regardless of their version number.
        /// </summary>
        Force = 2
    }// enum AssemblyCommitFlags

    /// <summary>
    /// The uninstall action taken
    /// </summary>
    public enum AssemblyCacheUninstallDisposition
    {
        /// <summary>
        /// Unknown
        /// </summary>
        Unknown = 0,
        /// <summary>
        /// The assembly files have been removed from the GAC
        /// </summary>
        Uninstalled = 1,
        /// <summary>
        /// An application is using the assembly.
        /// </summary>
        StillInUse = 2,
        /// <summary>
        /// The assembly does not exist in the GAC
        /// </summary>
        AlreadyUninstalled = 3,
        /// <summary>
        /// Not used.
        /// </summary>
        DeletePending = 4,
        /// <summary>
        /// The assembly has not been removed from the GAC because another application reference exists.
        /// </summary>
        HasInstallReference = 5,
        /// <summary>
        /// The <see cref="InstallReference"/> that was specified is not found in the GAC.
        /// </summary>
        ReferenceNotFound = 6
    }

    /// <summary>
    /// The ASM_CACHE_FLAGS enumeration used in <see cref="Utils.CreateAssemblyEnum"/>.
    /// </summary>
    [Flags]
    internal enum AssemblyCacheFlags
    {
        /// <summary>
        /// Enumerates the cache of precompiled assemblies by using Ngen.exe.
        /// </summary>
        ZAP = 0x1,
        /// <summary>
        /// Enumerates the GAC.
        /// </summary>
        GAC = 0x2,
        /// <summary>
        /// Enumerates the assemblies that have been downloaded on-demand or that have been shadow-copied.
        /// </summary>
        DOWNLOAD = 0x4
    }

    /// <summary>
    /// The CREATE_ASM_NAME_OBJ_FLAGS enumeration, used in <see cref="Utils.CreateAssemblyNameObject"/>
    /// </summary>
    internal enum CreateAssemblyNameObjectFlags
    {
        /// <summary>
        /// If this flag is specified, the szAssemblyName parameter is a full assembly name and is parsed to the individual properties. If the flag is not specified, szAssemblyName is the "Name" portion of the assembly name.
        /// </summary>
        CANOF_DEFAULT = 0,
        /// <summary>
        /// If this flag is specified, certain properties, such as processor architecture, are set to their default values.
        /// </summary>
        CANOF_PARSE_DISPLAY_NAME = 1,
    }

    /// <summary>
    /// The ASM_NAME enumeration property ID describes the valid names of the name-value pairs in an assembly name. See the .NET Framework SDK for a description of these properties.
    /// </summary>
    internal enum AssemblyNamePropertyIds : int
    {
        ASM_NAME_PUBLIC_KEY = 0,
        ASM_NAME_PUBLIC_KEY_TOKEN,
        ASM_NAME_HASH_VALUE,
        ASM_NAME_NAME,
        ASM_NAME_MAJOR_VERSION,
        ASM_NAME_MINOR_VERSION,
        ASM_NAME_BUILD_NUMBER,
        ASM_NAME_REVISION_NUMBER,
        ASM_NAME_CULTURE,
        ASM_NAME_PROCESSOR_ID_ARRAY,
        ASM_NAME_OSINFO_ARRAY,
        ASM_NAME_HASH_ALGID,
        ASM_NAME_ALIAS,
        ASM_NAME_CODEBASE_URL,
        ASM_NAME_CODEBASE_LASTMOD,
        ASM_NAME_NULL_PUBLIC_KEY,
        ASM_NAME_NULL_PUBLIC_KEY_TOKEN,
        ASM_NAME_CUSTOM,
        ASM_NAME_NULL_CUSTOM,
        ASM_NAME_MVID,
        ASM_NAME_MAX_PARAMS
    }

    /// <summary>
    /// ASM_DISPLAY_FLAGS: <see cref="IAssemblyName.GetDisplayName"/>.
    /// </summary>
    [Flags]
    internal enum AssemblyNameDisplayFlags : int
    {
        /// <summary>
        /// Includes the version number as part of the display name.
        /// </summary>
        VERSION = 0x01,
        /// <summary>
        /// Includes the culture.
        /// </summary>
        CULTURE = 0x02,
        /// <summary>
        ///  Includes the public key token.
        /// </summary>
        PUBLIC_KEY_TOKEN = 0x04,
        /// <summary>
        ///  Includes the public key.
        /// </summary>
        PUBLIC_KEY = 0x8,
        /// <summary>
        /// Includes the custom part of the assembly name.
        /// </summary>
        CUSTOM = 0x10,
        /// <summary>
        /// Includes the processor architecture.
        /// </summary>
        PROCESSORARCHITECTURE = 0x20,
        /// <summary>
        /// Includes the language ID.
        /// </summary>
        LANGUAGEID = 0x40,
        RETARGETABLE = 0x80,
        /// <summary>
        /// Include all attributes.
        /// </summary>
        ALL = VERSION
                | CULTURE
                | PUBLIC_KEY_TOKEN
                | PUBLIC_KEY
                | CUSTOM
                | PROCESSORARCHITECTURE
                | LANGUAGEID
                | RETARGETABLE
    }
    #endregion

    /// <summary>
    /// The FUSION_INSTALL_REFERENCE structure represents a reference that is made when an application has installed an assembly in the GAC.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public class InstallReference
    {
        /// <summary>
        /// Create a new InstallReference
        /// </summary>
        /// <param name="guid">Possible values for the guidScheme field can be one of the following (<see cref="InstallReferenceGuid"/>):
        ///     <list type="table">
        ///     <item>
        ///         <term>FUSION_REFCOUNT_MSI_GUID</term>
        ///         <description>The assembly is referenced by an application that has been installed by using Windows Installer. The szIdentifier field is set to MSI, and szNonCannonicalData is set to Windows Installer. This scheme must only be used by Windows Installer itself.</description>
        ///     </item>
        ///     <item>
        ///     <term>FUSION_REFCOUNT_UNINSTALL_SUBKEY_GUID</term>
        ///     <description>The assembly is referenced by an application that appears in Add/Remove Programs. The szIdentifier field is the token that is used to register the application with Add/Remove programs.</description>
        ///     </item>
        ///     <item>
        ///         <term>FUSION_REFCOUNT_FILEPATH_GUID</term>
        ///         <description>The assembly is referenced by an application that is represented by a file in the file system. The szIdentifier field is the path to this file.</description>
        ///     </item>
        ///     <item>
        ///         <term>FUSION_REFCOUNT_OPAQUE_STRING_GUID</term>
        ///         <description>The assembly is referenced by an application that is only represented by an opaque string. The szIdentifier is this opaque string. The GAC does not perform existence checking for opaque references when you remove this.</description>
        ///     </item>
        ///     </list>
        /// </param>
        /// <param name="id">A unique string that identifies the application that installed the assembly.</param>
        /// <param name="data">A string that is only understood by the entity that adds the reference. The GAC only stores this string.</param>
        public InstallReference(Guid guid, String id, String data)
        {
            cbSize = (int)(2 * IntPtr.Size + 16 + (id.Length + data.Length) * 2);
            flags = 0;
            // quiet compiler warning 
            if (flags == 0) { }
            guidScheme = guid;
            identifier = id;
            description = data;
        }

        /// <summary>
        /// The entity that adds the reference.
        /// </summary>
        public Guid GuidScheme
        {
            get { return guidScheme; }
        }

        /// <summary>
        /// A unique string that identifies the application that installed the assembly.
        /// </summary>
        public String Identifier
        {
            get { return identifier; }
        }

        /// <summary>
        /// A string that is only understood by the entity that adds the reference. The GAC only stores this string.
        /// </summary>
        public String Description
        {
            get { return description; }
        }

        /// <summary>
        /// The size of the structure in bytes.
        /// </summary>
        int cbSize;
        /// <summary>
        /// Reserved, must be zero.
        /// </summary>
        int flags;
        Guid guidScheme;

        [MarshalAs(UnmanagedType.LPWStr)]
        String identifier;
        [MarshalAs(UnmanagedType.LPWStr)]
        String description;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct AssemblyInfo
    {
        public int cbAssemblyInfo; // size of this structure for future expansion
        public int assemblyFlags;
        public long assemblySizeInKB;
        [MarshalAs(UnmanagedType.LPWStr)]
        public String currentAssemblyPath;
        public int cchBuf; // size of path buf.
    }

    /// <summary>
    /// Possible values for the guidScheme for <see cref="InstallReference"/>.
    /// </summary>
    [ComVisible(false)]
    public class InstallReferenceGuid
    {
        /// <summary>
        /// Ensures that the provided Guid is one of the valid reference guids defined in <see cref="InstallReferenceGuid"/> (excluding <see cref="MsiGuid"/> and <see cref="OsInstallGuid"/>).
        /// </summary>
        /// <param name="guid">The Guid to validate</param>
        /// <returns>True if the Guid is <see cref="UninstallSubkeyGuid"/>, <see cref="FilePathGuid"/>, <see cref="OpaqueGuid"/> or <see cref="Guid.Empty"/>.</returns>
        public static bool IsValidGuidScheme(Guid guid)
        {
            return (guid.Equals(UninstallSubkeyGuid) ||
                    guid.Equals(FilePathGuid) ||
                    guid.Equals(OpaqueGuid) ||
                    guid.Equals(Guid.Empty));
        }

        /// <summary>
        /// FUSION_REFCOUNT_UNINSTALL_SUBKEY_GUID - The assembly is referenced by an application that appears in Add/Remove Programs. The szIdentifier field is the token that is used to register the application with Add/Remove programs.
        /// </summary>
        public readonly static Guid UninstallSubkeyGuid = new Guid("8cedc215-ac4b-488b-93c0-a50a49cb2fb8");
        /// <summary>
        /// FUSION_REFCOUNT_FILEPATH_GUID - The assembly is referenced by an application that is represented by a file in the file system. The szIdentifier field is the path to this file.
        /// </summary>
        public readonly static Guid FilePathGuid = new Guid("b02f9d65-fb77-4f7a-afa5-b391309f11c9");
        /// <summary>
        /// FUSION_REFCOUNT_OPAQUE_STRING_GUID - The assembly is referenced by an application that is only represented by an opaque string. The szIdentifier is this opaque string. The GAC does not perform existence checking for opaque references when you remove this.
        /// </summary>
        public readonly static Guid OpaqueGuid = new Guid("2ec93463-b0c3-45e1-8364-327e96aea856");
        /// <summary>
        /// This GUID cannot be used for installing into GAC. FUSION_REFCOUNT_MSI_GUID - The assembly is referenced by an application that has been installed by using Windows Installer. The szIdentifier field is set to MSI, and szNonCannonicalData is set to Windows Installer. This scheme must only be used by Windows Installer itself.
        /// </summary>
        public readonly static Guid MsiGuid = new Guid("25df0fc1-7f97-4070-add7-4b13bbfd7cb8");
        /// <summary>
        /// This GUID cannot be used for installing into GAC.
        /// </summary>
        public readonly static Guid OsInstallGuid = new Guid("d16d444c-56d8-11d5-882d-0080c847b195");
    }

    #endregion

    /// <summary>
    /// Provides methods to install or remove assemblies from the Global Assembly Cache (GAC)
    /// </summary>
    [ComVisible(false)]
    public static class AssemblyCache
    {
        /// <summary>
        /// Install assembly into GAC
        /// </summary>
        /// <param name="assemblyPath"></param>
        /// <param name="reference"></param>
        /// <param name="flags"></param>
        public static void InstallAssembly(String assemblyPath, InstallReference reference, AssemblyCommitFlags flags)
        {
            if (reference != null)
            {
                if (!InstallReferenceGuid.IsValidGuidScheme(reference.GuidScheme))
                    throw new ArgumentException("Invalid reference guid.", "guid");
            }

            IAssemblyCache ac = null;

            int hr = 0;

            hr = Utils.CreateAssemblyCache(out ac, 0);
            if (hr >= 0)
            {
                hr = ac.InstallAssembly((int)flags, assemblyPath, reference);
            }

            if (hr < 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }
        }

        /// <summary>
        /// Install the provided assemblies to GAC
        /// </summary>
        /// <param name="assemblyPaths"></param>
        /// <param name="reference"></param>
        /// <param name="flags"></param>
        public static void InstallAssemblies(String[] assemblyPaths, InstallReference reference, AssemblyCommitFlags flags)
        {
            if (reference != null)
            {
                if (!InstallReferenceGuid.IsValidGuidScheme(reference.GuidScheme))
                    throw new ArgumentException("Invalid reference guid.", "guid");
            }

            IAssemblyCache ac = null;

            int hr = 0;

            hr = Utils.CreateAssemblyCache(out ac, 0);
            if (hr >= 0)
            {
                foreach (var assemblyPath in assemblyPaths)
                {
                    hr = ac.InstallAssembly((int)flags, assemblyPath, reference);
                    if (hr < 0)
                    {
                        Marshal.ThrowExceptionForHR(hr);
                    }
                }
            }
        }

        /// <summary>
        /// Uninstall an assembly from the GAC. <paramref name="assemblyName"/> has to be fully specified name. E.g., for v1.0/v1.1 assemblies, it should be "name, Version=xx, Culture=xx, PublicKeyToken=xx". For v2.0+ assemblies, it should be "name, Version=xx, Culture=xx, PublicKeyToken=xx, ProcessorArchitecture=xx". If <paramref name="assemblyName"/> is not fully specified, a random matching assembly could be uninstalled.
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <param name="reference"></param>
        /// <param name="disp"></param>
        public static void UninstallAssembly(String assemblyName, InstallReference reference, out AssemblyCacheUninstallDisposition disp)
        {
            AssemblyCacheUninstallDisposition dispResult = AssemblyCacheUninstallDisposition.Uninstalled;
            if (reference != null)
            {
                if (!InstallReferenceGuid.IsValidGuidScheme(reference.GuidScheme))
                    throw new ArgumentException("Invalid reference guid.", "guid");
            }

            IAssemblyCache ac = null;

            int hr = Utils.CreateAssemblyCache(out ac, 0);
            if (hr >= 0)
            {
                hr = ac.UninstallAssembly(0, assemblyName, reference, out dispResult);
            }

            if (hr < 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            disp = dispResult;
        }

        /// <summary>
        /// Uninstall the provided assembly names from the GAC. <paramref name="assemblyNames"/> have to be fully specified names. E.g., for v1.0/v1.1 assemblies, it should be "name, Version=xx, Culture=xx, PublicKeyToken=xx". For v2.0+ assemblies, it should be "name, Version=xx, Culture=xx, PublicKeyToken=xx, ProcessorArchitecture=xx". If <paramref name="assemblyNames"/> is not fully specified, a random matching assembly could be uninstalled.
        /// </summary>
        /// <param name="assemblyNames"></param>
        /// <param name="reference"></param>
        /// <param name="disp"></param>
        public static void UninstallAssemblies(String[] assemblyNames, InstallReference reference, out AssemblyCacheUninstallDisposition[] disp)
        {
            AssemblyCacheUninstallDisposition dispResult = AssemblyCacheUninstallDisposition.Uninstalled;
            if (reference != null)
            {
                if (!InstallReferenceGuid.IsValidGuidScheme(reference.GuidScheme))
                    throw new ArgumentException("Invalid reference guid.", "guid");
            }

            IAssemblyCache ac = null;

            disp = new AssemblyCacheUninstallDisposition[assemblyNames.Length];
            int hr = Utils.CreateAssemblyCache(out ac, 0);
            if (hr >= 0)
            {
                for (var i = 0; i < assemblyNames.Length; i++)
                {
                    hr = ac.UninstallAssembly(0, assemblyNames[i], reference, out dispResult);
                    if (hr < 0)
                    {
                        Marshal.ThrowExceptionForHR(hr);
                    }

                    disp[i] = dispResult;
                }
            }
        }

        /// <summary>
        /// Query an assembly in the GAC. <paramref name="assemblyName"/> has to be a fully specified name. E.g., for v1.0/v1.1 assemblies, it should be "name, Version=xx, Culture=xx, PublicKeyToken=xx". For v2.0+ assemblies, it should be "name, Version=xx, Culture=xx, PublicKeyToken=xx, ProcessorArchitecture=xx". If <paramref name="assemblyName"/> is not fully specified, a random matching assembly may be found.
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <returns></returns>
        public static String QueryAssemblyInfo(String assemblyName)
        {
            if (assemblyName == null)
            {
                throw new ArgumentException("Invalid name", "assemblyName");
            }

            AssemblyInfo aInfo = new AssemblyInfo();

            aInfo.cchBuf = 1024;
            // Get a string with the desired length
            aInfo.currentAssemblyPath = new String('\0', aInfo.cchBuf);

            IAssemblyCache ac = null;
            int hr = Utils.CreateAssemblyCache(out ac, 0);
            if (hr >= 0)
            {
                hr = ac.QueryAssemblyInfo(0, assemblyName, ref aInfo);
            }
            if (hr < 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            return aInfo.currentAssemblyPath;
        }
    }

    /// <summary>
    /// Enumerate assemblies within the Global Assembly Cache (GAC)
    /// </summary>
    [ComVisible(false)]
    public class AssemblyCacheEnum
    {
        /// <summary>
        /// Enumerate assemblies in the GAC
        /// </summary>
        /// <param name="assemblyName">null means enumerate all the assemblies</param>
        public AssemblyCacheEnum(String assemblyName)
        {
            IAssemblyName fusionName = null;
            int hr = 0;

            if (assemblyName != null)
            {
                hr = Utils.CreateAssemblyNameObject(
                        out fusionName,
                        assemblyName,
                        CreateAssemblyNameObjectFlags.CANOF_PARSE_DISPLAY_NAME,
                        IntPtr.Zero);
            }

            if (hr >= 0)
            {
                hr = Utils.CreateAssemblyEnum(
                        out m_AssemblyEnum,
                        IntPtr.Zero,
                        fusionName,
                        AssemblyCacheFlags.GAC,
                        IntPtr.Zero);
            }

            if (hr < 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }
        }

        /// <summary>
        /// Get the next assembly within the enumerator.
        /// </summary>
        /// <returns></returns>
        public String GetNextAssembly()
        {
            int hr = 0;
            IAssemblyName fusionName = null;

            if (done)
            {
                return null;
            }

            // Now get next IAssemblyName from m_AssemblyEnum
            hr = m_AssemblyEnum.GetNextAssembly((IntPtr)0, out fusionName, 0);

            if (hr < 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            if (fusionName != null)
            {
                return GetFullName(fusionName);
            }
            else
            {
                done = true;
                return null;
            }
        }

        private String GetFullName(IAssemblyName fusionAsmName)
        {
            StringBuilder sDisplayName = new StringBuilder(1024);
            int iLen = 1024;

            int hr = fusionAsmName.GetDisplayName(sDisplayName, ref iLen, (int)AssemblyNameDisplayFlags.ALL);
            if (hr < 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            return sDisplayName.ToString();
        }

        private IAssemblyEnum m_AssemblyEnum = null;
        private bool done;
    }// class AssemblyCacheEnum

    /// <summary>
    /// Enumerate referenced assemblies installed in the global assembly cache.
    /// </summary>
    public class AssemblyCacheInstallReferenceEnum
    {
        /// <summary>
        /// Create enumerator for provided assembly name
        /// </summary>
        /// <param name="assemblyName"><paramref name="assemblyName"/> has to be a fully specified name. E.g., for v1.0/v1.1 assemblies, it should be "name, Version=xx, Culture=xx, PublicKeyToken=xx". For v2.0+ assemblies, it should be "name, Version=xx, Culture=xx, PublicKeyToken=xx, ProcessorArchitecture=xx".</param>
        public AssemblyCacheInstallReferenceEnum(String assemblyName)
        {
            IAssemblyName fusionName = null;

            int hr = Utils.CreateAssemblyNameObject(
                        out fusionName,
                        assemblyName,
                        CreateAssemblyNameObjectFlags.CANOF_PARSE_DISPLAY_NAME,
                        IntPtr.Zero);

            if (hr >= 0)
            {
                hr = Utils.CreateInstallReferenceEnum(out refEnum, fusionName, 0, IntPtr.Zero);
            }

            if (hr < 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }
        }

        /// <summary>
        /// Get next reference
        /// </summary>
        /// <returns></returns>
        public InstallReference GetNextReference()
        {
            IInstallReferenceItem item = null;
            int hr = refEnum.GetNextInstallReferenceItem(out item, 0, IntPtr.Zero);
            if ((uint)hr == 0x80070103)
            {   // ERROR_NO_MORE_ITEMS
                return null;
            }

            if (hr < 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            IntPtr refData;
            InstallReference instRef = new InstallReference(Guid.Empty, String.Empty, String.Empty);

            hr = item.GetReference(out refData, 0, IntPtr.Zero);
            if (hr < 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            Marshal.PtrToStructure(refData, instRef);
            return instRef;
        }

        private IInstallReferenceEnum refEnum;
    }

    /// <summary>
    /// Fusion DllImports
    /// </summary>
    internal static class Utils
    {
        /// <summary>
        /// To obtain an instance of the <see cref="IAssemblyEnum"/> API, call the CreateAssemblyEnum API
        /// </summary>
        /// <param name="ppEnum">Pointer to a memory location that contains the IAssemblyEnum pointer.</param>
        /// <param name="pUnkReserved">Must be null.</param>
        /// <param name="pName">An assembly name that is used to filter the enumeration. Can be null to enumerate all assemblies in the GAC.</param>
        /// <param name="flags">Exactly one item from the ASM_CACHE_FLAGS enumeration, <see cref="AssemblyCacheFlags"/>.</param>
        /// <param name="pvReserved">Must be NULL.</param>
        /// <returns></returns>
        [DllImport("fusion.dll")]
        internal static extern int CreateAssemblyEnum(
                out IAssemblyEnum ppEnum,
                IntPtr pUnkReserved,
                IAssemblyName pName,
                AssemblyCacheFlags flags,
                IntPtr pvReserved);

        /// <summary>
        /// An instance of <see cref="IAssemblyName"/> is obtained by calling the CreateAssemblyNameObject API
        /// </summary>
        /// <param name="ppAssemblyNameObj">Pointer to a memory location that receives the IAssemblyName pointer that is created.</param>
        /// <param name="szAssemblyName">A string representation of the assembly name or of a full assembly reference that is determined by dwFlags. The string representation can be null.</param>
        /// <param name="flags">Zero or more of the bits that are defined in the CREATE_ASM_NAME_OBJ_FLAGS enumeration, <see cref="CreateAssemblyNameObjectFlags"/>.</param>
        /// <param name="pvReserved">Must be null.</param>
        /// <returns></returns>
        [DllImport("fusion.dll")]
        internal static extern int CreateAssemblyNameObject(
                out IAssemblyName ppAssemblyNameObj,
                [MarshalAs(UnmanagedType.LPWStr)]
                String szAssemblyName,
                CreateAssemblyNameObjectFlags flags,
                IntPtr pvReserved);

        /// <summary>
        /// To obtain an instance of the <see cref="IAssemblyCache"/> API, call the CreateAssemblyCache API
        /// </summary>
        /// <param name="ppAsmCache">Pointer to return <see cref="IAssemblyCache"/></param>
        /// <param name="reserved">Must be zero.</param>
        /// <returns></returns>
        [DllImport("fusion.dll")]
        internal static extern int CreateAssemblyCache(
                out IAssemblyCache ppAsmCache,
                int reserved);

        /// <summary>
        /// To obtain an instance of the <see cref="IInstallReferenceEnum"/> API, call the CreateInstallReferenceEnum API
        /// </summary>
        /// <param name="ppRefEnum">A pointer to a memory location that receives the IInstallReferenceEnum pointer.</param>
        /// <param name="pName">The assembly name for which the references are enumerated.</param>
        /// <param name="dwFlags">Must be zero.</param>
        /// <param name="pvReserved">Must be null.</param>
        /// <returns></returns>
        [DllImport("fusion.dll")]
        internal static extern int CreateInstallReferenceEnum(
                out IInstallReferenceEnum ppRefEnum,
                IAssemblyName pName,
                int dwFlags,
                IntPtr pvReserved);
    }
}
