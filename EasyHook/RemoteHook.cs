// EasyHook (File: EasyHook\RemoteHook.cs)
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
using System.Reflection;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Security.Policy;
using System.Security;
using System.Security.Permissions;
using System.Runtime.Remoting;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.IO.Compression;
using System.Security.Cryptography;

#pragma warning disable 0419 // XML comment: ambiguous reference

namespace EasyHook
{
    [Serializable]
    internal class ManagedRemoteInfo
    {
        public String ChannelName;
        public Object[] UserParams;
        public String UserLibrary;
        public String UserLibraryName;
        public Int32 HostPID;
        public bool RequireStrongName;
    }

    /// <summary>
    /// EasyHook will search in the injected user library for a class which implements
    /// this interface. You should only have one class exposing this interface, otherwise
    /// it is undefined which one will be choosen. 
    /// </summary>
    /// <remarks>
    /// <para>
    /// To implement this interface is not the only thing to do. The related class shall export
    /// two methods. The first one is <c>Initialize(IContext, ...)</c> which will let you initialize
    /// your library. You should immediately complete this call and only connect to your host
    /// application for further error reporting. This initialization method allows you to redirect
    /// all unhandled exceptions to your host application automatically. So even if all things in
    /// your library initialization would fail, you may still report exceptions! Such
    /// unhandled exceptions will be thrown by <see cref="RemoteHooking.Inject"/> in your host. But
    /// make sure that you are using serializable exception objects only as all standard NET ones are,
    /// but not all custom ones. Otherwise you will only intercept a general exception with no specific
    /// information attached.
    /// </para><para>
    /// The second one is <c>Run(IContext, ...)</c> and should return if you want to unload your injected library. 
    /// Unhandled exceptions WON'T be redirected automatically. As you are expected to connect to
    /// your host in <c>Initialize()</c>, you are now also expected to report errors by yourself.
    /// </para><para>
    /// The parameter list described by <c>(IContext, ...)</c> will always contain a <see cref="RemoteHooking.IContext"/>
    /// instance as first parameter. All further parameters will depend on the arguments passed to <see cref="RemoteHooking.Inject"/>
    /// at your injection host. <c>Initialize()</c> and <c>Run()</c> must have the same custom parameter list
    /// as composed by the one passed to <c>Inject()</c>. Otherwise an exception will be thrown. For example
    /// if you call <see cref="RemoteHooking.Inject"/> with <c>Inject(..., ..., ..., ..., "MyString1", "MyString2")</c>, you
    /// have supplied a custom argument list of the format <c>String, String</c> to <c>Inject</c>. This list
    /// will be converted to an object array and serialized. The injected library stub will later
    /// deserialize this array and pass it to <c>Initialize()</c> and <c>Run()</c>, both expected to have
    /// a parameter of <c>IContext, String, String</c> in our case. So <c>Run</c> will now be called with
    /// <c>(IContext, "MyString1", "MyString2")</c>. I hope this is comprehensively explained ;-).
    /// </para><para>
    /// You shouldn't use static fields or properties within such a class, as this might lead to
    /// bugs in your code when multiple library instances are running in the same target!
    /// </para>
    /// </remarks>
    public interface IEntryPoint { }

    /// <summary>
    /// All supported options that will influence the way your library is injected.
    /// </summary>
    [Flags]
    public enum InjectionOptions
    {
        /// <summary>
        /// Default injection procedure.
        /// </summary>
        Default = 0x0,

        /// <summary>
        /// Use of services is not permitted.
        /// </summary>
        NoService = 0x1,

        /// <summary>
        /// Use of WOW64 bypass is not permitted.
        /// </summary>
        NoWOW64Bypass = 0x2,

        /// <summary>
        /// Allow injection without a strong name (e.g. no GAC registration). This option requires that the full path to injected assembly be provided
        /// </summary>
        DoNotRequireStrongName = 0x4,
    }

    /// <summary>
    /// Provides all things related to library injection, inter-process-communication (IPC) and
    /// helper routines for common remote tasks.
    /// </summary>
    /// <include file='FileMonHost.xml' path='remarks'/>
    public class RemoteHooking
    {
        /// <summary>
        /// A context contains some basic information about the environment
        /// in which your library main method has been invoked. You will always
        /// get an instance of this interface in your library <c>Run</c> method
        /// and your library <c>Initialize</c> method. 
        /// </summary>
        public interface IContext
        {
            /// <summary>
            /// Returns the process ID of the host that has injected this library.
            /// </summary>
            Int32 HostPID { get; }
        }

        private RemoteHooking() { }

        /// <summary>
        /// If the library was injected with <see cref="CreateAndInject"/>, this will
        /// finally start the current process. You should call this method in the library
        /// <c>Run()</c> method after all hooks have been installed.
        /// </summary>
        public static void WakeUpProcess()
        {
            NativeAPI.RhWakeUpProcess();
        }

        /// <summary>
        /// <c>true</c> if we are running with administrative privileges, <c>false</c> otherwise.
        /// </summary>
        /// <remarks>
        /// Due to UAC on Windows Vista, this property in general will be <c>false</c> even if the user is in
        /// the builtin-admin group. As you can't hook without administrator privileges you
        /// should just set the UAC level of your application to <c>requireAdministrator</c>.
        /// </remarks>
        public static bool IsAdministrator { get { return NativeAPI.RhIsAdministrator(); } }

        internal static String GenerateName()
        {
            RNGCryptoServiceProvider Rnd = new RNGCryptoServiceProvider();
            Byte[] Data = new Byte[30];
            StringBuilder Builder = new StringBuilder();

            Rnd.GetBytes(Data);

            for (int i = 0; i < (20 + (Data[0] % 10)); i++)
            {
                Byte b = (Byte)(Data[i] % 62);

                if ((b >= 0) && (b <= 9))
                    Builder.Append((Char)('0' + b));
                if ((b >= 10) && (b <= 35))
                    Builder.Append((Char)('A' + (b - 10)));
                if ((b >= 36) && (b <= 61))
                    Builder.Append((Char)('a' + (b - 36)));
            }

            return Builder.ToString();
        }

        /// <summary>
        /// Creates a globally reachable, managed IPC-Port.
        /// </summary>
        /// <remarks>
        /// Because it is something tricky to get a port working for any constellation of
        /// target processes, I decided to write a proper wrapper method. Just keep the returned
        /// <see cref="IpcChannel"/> alive, by adding it to a global list or static variable,
        /// as long as you want to have the IPC port open.
        /// </remarks>
        /// <typeparam name="TRemoteObject">
        /// A class derived from <see cref="MarshalByRefObject"/> which provides the
        /// method implementations this server should expose.
        /// </typeparam>
        /// <param name="InObjectMode">
        /// <see cref="WellKnownObjectMode.SingleCall"/> if you want to handle each call in an new
        /// object instance, <see cref="WellKnownObjectMode.Singleton"/> otherwise. The latter will implicitly
        /// allow you to use "static" remote variables.
        /// </param>
        /// <param name="RefChannelName">
        /// Either <c>null</c> to let the method generate a random channel name to be passed to 
        /// <see cref="IpcConnectClient{TRemoteObject}"/> or a predefined one. If you pass a value unequal to 
        /// <c>null</c>, you shall also specify all SIDs that are allowed to connect to your channel!
        /// </param>
        /// <param name="ipcInterface">Provide a TRemoteObject object to be made available as a well known type on the server end of the channel.</param>
        /// <param name="InAllowedClientSIDs">
        /// If no SID is specified, all authenticated users will be allowed to access the server
        /// channel by default. You must specify an SID if <paramref name="RefChannelName"/> is unequal to <c>null</c>.
        /// </param>
        /// <returns>
        /// An <see cref="IpcChannel"/> that shall be keept alive until the server is not needed anymore.
        /// </returns>
        /// <exception cref="System.Security.HostProtectionException">
        /// If a predefined channel name is being used, you are required to specify a list of well known SIDs
        /// which are allowed to access the newly created server.
        /// </exception>
        /// <exception cref="RemotingException">
        /// The given channel name is already in use.
        /// </exception>
        public static IpcServerChannel IpcCreateServer<TRemoteObject>(
                ref String RefChannelName,
                WellKnownObjectMode InObjectMode,
                TRemoteObject ipcInterface,
                params WellKnownSidType[] InAllowedClientSIDs) where TRemoteObject : MarshalByRefObject
        {
            String ChannelName = RefChannelName ?? GenerateName();

            ///////////////////////////////////////////////////////////////////
            // create security descriptor for IpcChannel...
            System.Collections.IDictionary Properties = new System.Collections.Hashtable();

            Properties["name"] = ChannelName;
            Properties["portName"] = ChannelName;

            DiscretionaryAcl DACL = new DiscretionaryAcl(false, false, 1);

            if (InAllowedClientSIDs.Length == 0)
            {
                if (RefChannelName != null)
                    throw new System.Security.HostProtectionException("If no random channel name is being used, you shall specify all allowed SIDs.");

                // allow access from all users... Channel is protected by random path name!
                DACL.AddAccess(
                    AccessControlType.Allow,
                    new SecurityIdentifier(
                        WellKnownSidType.WorldSid,
                        null),
                    -1,
                    InheritanceFlags.None,
                    PropagationFlags.None);
            }
            else
            {
                for (int i = 0; i < InAllowedClientSIDs.Length; i++)
                {
                    DACL.AddAccess(
                        AccessControlType.Allow,
                        new SecurityIdentifier(
                            InAllowedClientSIDs[i],
                            null),
                        -1,
                        InheritanceFlags.None,
                        PropagationFlags.None);
                }
            }

            CommonSecurityDescriptor SecDescr = new CommonSecurityDescriptor(false, false,
                ControlFlags.GroupDefaulted |
                ControlFlags.OwnerDefaulted |
                ControlFlags.DiscretionaryAclPresent,
                null, null, null,
                DACL);

            //////////////////////////////////////////////////////////
            // create IpcChannel...
            BinaryServerFormatterSinkProvider BinaryProv = new BinaryServerFormatterSinkProvider();
            BinaryProv.TypeFilterLevel = TypeFilterLevel.Full;

            IpcServerChannel Result = new IpcServerChannel(Properties, BinaryProv, SecDescr);

            ChannelServices.RegisterChannel(Result, false);

            if (ipcInterface == null)
            {
                RemotingConfiguration.RegisterWellKnownServiceType(
                    typeof(TRemoteObject),
                    ChannelName,
                    InObjectMode);
            }
            else
            {
                RemotingServices.Marshal(ipcInterface, ChannelName);
            }

            RefChannelName = ChannelName;

            return Result;
        }

        /// <summary>
        /// Creates a globally reachable, managed IPC-Port.
        /// </summary>
        /// <remarks>
        /// Because it is something tricky to get a port working for any constellation of
        /// target processes, I decided to write a proper wrapper method. Just keep the returned
        /// <see cref="IpcChannel"/> alive, by adding it to a global list or static variable,
        /// as long as you want to have the IPC port open.
        /// </remarks>
        /// <typeparam name="TRemoteObject">
        /// A class derived from <see cref="MarshalByRefObject"/> which provides the
        /// method implementations this server should expose.
        /// </typeparam>
        /// <param name="InObjectMode">
        /// <see cref="WellKnownObjectMode.SingleCall"/> if you want to handle each call in an new
        /// object instance, <see cref="WellKnownObjectMode.Singleton"/> otherwise. The latter will implicitly
        /// allow you to use "static" remote variables.
        /// </param>
        /// <param name="RefChannelName">
        /// Either <c>null</c> to let the method generate a random channel name to be passed to 
        /// <see cref="IpcConnectClient{TRemoteObject}"/> or a predefined one. If you pass a value unequal to 
        /// <c>null</c>, you shall also specify all SIDs that are allowed to connect to your channel!
        /// </param>
        /// <param name="InAllowedClientSIDs">
        /// If no SID is specified, all authenticated users will be allowed to access the server
        /// channel by default. You must specify an SID if <paramref name="RefChannelName"/> is unequal to <c>null</c>.
        /// </param>
        /// <returns>
        /// An <see cref="IpcChannel"/> that shall be keept alive until the server is not needed anymore.
        /// </returns>
        /// <exception cref="System.Security.HostProtectionException">
        /// If a predefined channel name is being used, you are required to specify a list of well known SIDs
        /// which are allowed to access the newly created server.
        /// </exception>
        /// <exception cref="RemotingException">
        /// The given channel name is already in use.
        /// </exception>
        public static IpcServerChannel IpcCreateServer<TRemoteObject>(
            ref String RefChannelName,
            WellKnownObjectMode InObjectMode,
            params WellKnownSidType[] InAllowedClientSIDs) where TRemoteObject : MarshalByRefObject
        {
            return IpcCreateServer<TRemoteObject>(ref RefChannelName, InObjectMode, null, InAllowedClientSIDs);
        }

        /// <summary>
        /// Connects to a globally reachable, managed IPC port.
        /// </summary>
        /// <remarks>
        /// All requests have to be made through the returned object instance.
        /// Please note that even if you might think that managed IPC is quiet slow,
        /// this is not usually the case. Internally a mechanism is being used to
        /// directly continue execution within the server process, so that even if 
        /// your thread does nothing while dispatching the request, no CPU time is lost,
        /// because the server thread seemlessly takes over exection. And to be true,
        /// the rare conditions in which you will need high-speed IPC ports are not
        /// worth the effort to break with NET's exciting IPC capabilities. In times
        /// of Quad-Cores, managed marshalling isn't that slow anymore.
        /// </remarks>
        /// <typeparam name="TRemoteObject">
        /// An object derived from <see cref="MarshalByRefObject"/> which provides the
        /// method implementations this server should provide. Note that only calls through the
        /// returned object instance will be redirected to the server process! ATTENTION: Static fields
        /// and members are always processed locally only...
        /// </typeparam>
        /// <param name="InChannelName">
        /// The name of the channel to connect to, usually obtained with <see cref="IpcCreateServer{TRemoteObject}"/>.
        /// </param>
        /// <returns>
        /// An remote object instance which member accesses will be redirected to the server.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Unable to create remote object or invalid channel name...
        /// </exception>
        public static TRemoteObject IpcConnectClient<TRemoteObject>(String InChannelName) where TRemoteObject : MarshalByRefObject
        { 
            // connect to bypass service
            TRemoteObject Interface = (TRemoteObject)Activator.GetObject(
                typeof(TRemoteObject),
                "ipc://" + InChannelName + "/" + InChannelName);

            if (Interface == null)
		        throw new ArgumentException("Unable to create remote interface.");

	        return Interface;
        }

        /// <summary>
        /// Injects the given user library into the target process. No memory leaks are left
        /// in the target, even if injection fails for unknown reasons. 
        /// </summary>
        /// <remarks>
        /// <para>
        /// There are two possible user library paths. The first one should map to
        /// a 32-bit library, and the second one should map to 64-bit library. If your
        /// code has been compiled for "AnyCPU", like it's the default for C#, you may
        /// even specify one library path for both parameters. Please note that your
        /// library including all of it's dependencies must be registered in the
        /// Global Assembly Cache (GAC). Refer to <see cref="Config.Register"/> for more
        /// information about how to get them there.
        /// </para><para>
        /// If you inject a library into any target process please keep in mind that
        /// your working directory will be switched. EasyHook will automatically add
        /// the directory of the injecting application as first directory of the target's PATH environment
        /// variable. So make sure that all required dependencies are either located
        /// within the injecting application's directory, a system directory or any directory defaultly
        /// contained in the PATH variable. As all managed assemblies have to be in the GAC
        /// there is no need for them being in any of those directories!
        /// </para> <para>
        /// EasyHook provides extensive error information during injection. Any kind of failure is
        /// being catched and thrown as an exception by this method. If for example your library
        /// does not expose a class implementing <see cref="IEntryPoint"/>, an exception will be
        /// raised in the target process during injection. The exception will be redirected to this method
        /// and you can catch it in a try-catch statement around <see cref="Inject"/>.
        /// </para> <para>
        /// You will often have to pass parameters to your injected library. <see cref="IpcChannel"/> 
        /// names are common, but also any other kind of data can be passed. You may add a custom list
        /// of objects marked with the <see cref="SerializableAttribute"/>. All common NET classes
        /// will be serializable by default, but if you are using your own classes you might have to provide
        /// serialization by yourself. The custom parameter list will be passed unchanged to your injected
        /// library entry points <c>Run</c> and <c>Initialize</c>. Verify that all required type libraries to deserialize
        /// your parameter list are in the GAC.
        /// </para><para>
        /// It is supported to inject code into 64-bit processes from within 32-bit processes and
        /// vice versa. It is also supported to inject code into other terminal sessions. Of course
        /// this will require additional processes and services to be created, but as they are managed
        /// internally, you won't notice them! There will be some delays when injecting the first library.
        /// Further injections are completed much faster!
        /// </para><para>
        /// Even if it would technically be possible to inject a library for debugging purposes into
        /// the current process, it will throw an exception. This is because it heavily depends on
        /// your injected library whether the current process will be damaged. Any kind of communication
        /// may lead into deadlocks if you hook the wrong APIs. Just use the capability of Visual Studio
        /// to debug more than one process simultanously which will allow you to debug your library
        /// as if it would be injected into the current process without running into any side-effects.
        /// </para>
        /// <para>
        /// The given exceptions are those which are thrown by EasyHook code. The NET framework might throw
        /// any other exception not listed here. Don't rely on the exception type. If you passed valid parameters,
        /// the only exceptions you should explicitly check for are <see cref="NotSupportedException"/> and
        /// <see cref="AccessViolationException"/>. All others
        /// shall be catched together and threaded as bad environment or invalid parameter error.
        /// </para>
        /// </remarks>
        /// <param name="InTargetPID">
        /// The target process ID.
        /// </param>
        /// <param name="InOptions">
        /// A valid combination of options.
        /// </param>
        /// <param name="InLibraryPath_x86">
        /// A partially qualified assembly name or a relative/absolute file path of the 32-bit version of your library. 
        /// For example "MyAssembly, PublicKeyToken=248973975895496" or ".\Assemblies\\MyAssembly.dll". 
        /// </param>
        /// <param name="InLibraryPath_x64">
        /// A partially qualified assembly name or a relative/absolute file path of the 64-bit version of your library. 
        /// For example "MyAssembly, PublicKeyToken=248973975895496" or ".\Assemblies\\MyAssembly.dll". 
        /// </param>
        /// <param name="InPassThruArgs">
        /// A serializable list of parameters being passed to your library entry points <c>Run()</c> and
        /// <c>Initialize()</c>.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// It is unstable to inject libraries into the same process. This exception is disabled in DEBUG mode.
        /// </exception>
        /// <exception cref="AccessViolationException">
        /// Access to target process denied or the current user is not an administrator.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// The given process does not exist or unable to serialize/deserialize one or more pass thru arguments.
        /// </exception>
        /// <exception cref="System.IO.FileNotFoundException">
        /// The given user library could not be found.
        /// </exception>
        /// <exception cref="OutOfMemoryException">
        /// Unable to allocate unmanaged memory in current or target process.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// It is not supported to inject into the target process. This is common on Windows Vista and Server 2008.
        /// </exception>
        /// <exception cref="TimeoutException">
        /// Unable to wait for user library to be initialized. Check your library <c>Initialize()</c> handler.
        /// </exception>
        /// <exception cref="EntryPointNotFoundException">
        /// The given user library does not export a class implementing the <see cref="IEntryPoint"/> interface.
        /// </exception>
        public static void Inject(
            Int32 InTargetPID,
            InjectionOptions InOptions,
            String InLibraryPath_x86,
            String InLibraryPath_x64,
            params Object[] InPassThruArgs)
        {
            InjectEx(
                GetCurrentProcessId(),
                InTargetPID,
                0, 
                0,
                InLibraryPath_x86,
                InLibraryPath_x64,
                ((InOptions & InjectionOptions.NoWOW64Bypass) == 0),
                ((InOptions & InjectionOptions.NoService) == 0),
                ((InOptions & InjectionOptions.DoNotRequireStrongName) == 0),
                InPassThruArgs);
        }

        /// <summary>
        /// See <see cref="Inject(Int32, InjectionOptions, String, String, Object[])"/> for more information.
        /// </summary>
        /// <param name="InTargetPID">
        /// The target process ID.
        /// </param>
        /// <param name="InLibraryPath_x86">
        /// A partially qualified assembly name or a relative/absolute file path of the 32-bit version of your library. 
        /// For example "MyAssembly, PublicKeyToken=248973975895496" or ".\Assemblies\\MyAssembly.dll". 
        /// </param>
        /// <param name="InLibraryPath_x64">
        /// A partially qualified assembly name or a relative/absolute file path of the 64-bit version of your library. 
        /// For example "MyAssembly, PublicKeyToken=248973975895496" or ".\Assemblies\\MyAssembly.dll". 
        /// </param>
        /// <param name="InPassThruArgs">
        /// A serializable list of parameters being passed to your library entry points <c>Run()</c> and
        /// <c>Initialize()</c>.
        /// </param>
        public static void Inject(
            Int32 InTargetPID,
            String InLibraryPath_x86,
            String InLibraryPath_x64,
            params Object[] InPassThruArgs)
        {
            InjectEx(
                GetCurrentProcessId(),
                InTargetPID, 
                0,
                0x20000000,
                InLibraryPath_x86, 
                InLibraryPath_x64, 
                true,
                true,
                true,
                InPassThruArgs);
        }

        private static GCHandle PrepareInjection(
            ManagedRemoteInfo InRemoteInfo,
            ref String InLibraryPath_x86,
            ref String InLibraryPath_x64,
            MemoryStream InPassThruStream)
        {
            if (String.IsNullOrEmpty(InLibraryPath_x86) && String.IsNullOrEmpty(InLibraryPath_x64))
                throw new ArgumentException("At least one library for x86 or x64 must be provided");

            // ensure full path information in case of file names...
            if ((InLibraryPath_x86 != null) && File.Exists(InLibraryPath_x86))
                InLibraryPath_x86 = Path.GetFullPath(InLibraryPath_x86);

            if ((InLibraryPath_x64 != null) && File.Exists(InLibraryPath_x64))
                InLibraryPath_x64 = Path.GetFullPath(InLibraryPath_x64);

            /*
                validate assembly name...
             */
            InRemoteInfo.UserLibrary = InLibraryPath_x86;

            if (NativeAPI.Is64Bit)
                InRemoteInfo.UserLibrary = InLibraryPath_x64;

            if (File.Exists(InRemoteInfo.UserLibrary))
            {
                // translate to assembly name
                InRemoteInfo.UserLibraryName = AssemblyName.GetAssemblyName(InRemoteInfo.UserLibrary).FullName;
            }
            else
            {
                throw new FileNotFoundException(String.Format("The given assembly could not be found. {0}", InRemoteInfo.UserLibrary), InRemoteInfo.UserLibrary);
            }

            // Attempt to load the library by its FullName and if that fails, by its original library filename
            Assembly UserAsm = null;
            try
            {
                if (!String.IsNullOrEmpty(InRemoteInfo.UserLibraryName))
                {
                    UserAsm = Assembly.ReflectionOnlyLoad(InRemoteInfo.UserLibraryName);
                }
            }
            catch (FileNotFoundException)
            {
                // We already know the file exists at this point so try to load from original library filename instead
                UserAsm = null;
            }
            if (UserAsm == null && (UserAsm = Assembly.ReflectionOnlyLoadFrom(InRemoteInfo.UserLibrary)) == null)
                throw new DllNotFoundException(String.Format("The given assembly could not be found. {0}", InRemoteInfo.UserLibrary));

            // Check for a strong name if necessary
            if (InRemoteInfo.RequireStrongName && (Int32)(UserAsm.GetName().Flags & AssemblyNameFlags.PublicKey) == 0)
                throw new ArgumentException("The given assembly has no strong name.");

            /*
                Convert managed arguments to binary stream...
             */

            BinaryFormatter Format = new BinaryFormatter();
            IpcServerChannel Channel = IpcCreateServer<HelperServiceInterface>(
                ref InRemoteInfo.ChannelName,
                WellKnownObjectMode.Singleton);

            Format.Serialize(InPassThruStream, InRemoteInfo);

            return GCHandle.Alloc(InPassThruStream.GetBuffer(), GCHandleType.Pinned);
        }

        internal static void InjectEx(
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
            MemoryStream PassThru = new MemoryStream();

            HelperServiceInterface.BeginInjection(InTargetPID);
            try
            {
                ManagedRemoteInfo RemoteInfo = new ManagedRemoteInfo();
                RemoteInfo.HostPID = InHostPID;
				// We first serialise parameters so that they can be deserialised AFTER the UserLibrary is loaded
                BinaryFormatter format = new BinaryFormatter();
                List<object> args = new List<object>();
                if (InPassThruArgs != null)
                {
                    foreach (var arg in InPassThruArgs)
                    {
                        using(MemoryStream ms = new MemoryStream())
						{
                            format.Serialize(ms, arg);
                            args.Add(ms.ToArray());
						}
                    }
                }
                RemoteInfo.UserParams = args.ToArray();

				RemoteInfo.RequireStrongName = InRequireStrongName;

                GCHandle hPassThru = PrepareInjection(
                    RemoteInfo,
                    ref InLibraryPath_x86, 
                    ref InLibraryPath_x64,
                    PassThru);

                /*
                    Inject library...
                 */
                try
                {
                    Int32 NtStatus;
                    switch (NtStatus = NativeAPI.RhInjectLibraryEx(
                            InTargetPID,
                            InWakeUpTID,
                            NativeAPI.EASYHOOK_INJECT_MANAGED | InNativeOptions,
                            // TODO: allow initial loaded assembly to be configurable
                            Path.Combine(Path.GetDirectoryName(typeof(Config).Assembly.Location), "EasyLoad32.dll"),
                            Path.Combine(Path.GetDirectoryName(typeof(Config).Assembly.Location), "EasyLoad64.dll"),
                            hPassThru.AddrOfPinnedObject(),
                            (int)PassThru.Length))
                    {
                        case NativeAPI.STATUS_WOW_ASSERTION:
                            {
                                // Use helper application to bypass WOW64...
                                if (InCanBypassWOW64)
                                    WOW64Bypass.Inject(
                                        InHostPID,
                                        InTargetPID,
                                        InWakeUpTID,
                                        InNativeOptions,
                                        InLibraryPath_x86,
                                        InLibraryPath_x64,
                                        InRequireStrongName,
                                        InPassThruArgs);
                                else
                                    throw new AccessViolationException("Unable to inject library into target process.");

                            } break;

                        case NativeAPI.STATUS_ACCESS_DENIED:
                            {
                                // Use service and try again...
                                if (InCanCreateService)
                                    ServiceMgmt.Inject(
                                        InHostPID,
                                        InTargetPID,
                                        InWakeUpTID,
                                        InNativeOptions,
                                        InLibraryPath_x86,
                                        InLibraryPath_x64,
                                        InRequireStrongName,
                                        InPassThruArgs);
                                else
                                    NativeAPI.Force(NtStatus);
                            } break;

                        case NativeAPI.STATUS_SUCCESS:
                            {
                                // wait for injection completion
                                HelperServiceInterface.WaitForInjection(InTargetPID);
                            } break;

                        default:
                            {
                                NativeAPI.Force(NtStatus);
                            } break;
                    }
                }
                finally
                {
                    hPassThru.Free();
                }
            }
            finally
            {
                HelperServiceInterface.EndInjection(InTargetPID);
            }
        }

        /// <summary>
        /// Determines if the target process is 64-bit or not. This will work only
        /// if the current process has <c>PROCESS_QUERY_INFORMATION</c> access to the target. 
        /// </summary>
        /// <remarks>
        /// A typical mistake is to enumerate processes under system privileges and 
        /// calling this method later when required. This won't work in most cases because
        /// you'll also need system privileges to run this method on processes in other sessions!
        /// </remarks>
        /// <param name="InTargetPID">The PID of the target process.</param>
        /// <returns><c>true</c> if the given process is 64-bit, <c>false</c> otherwise.</returns>
        /// <exception cref="AccessViolationException">
        /// The given process is not accessible.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// The given process does not exist.
        /// </exception>
        public static bool IsX64Process(Int32 InTargetPID)
        {
            Boolean Result;

            NativeAPI.RhIsX64Process(InTargetPID, out Result);

            return Result;
        }

        /// <summary>
        /// Returns the <see cref="WindowsIdentity"/> of the user the target process belongs to.
        /// You need <c>PROCESS_QUERY_INFORMATION</c> access to the target.
        /// </summary>
        /// <param name="InTargetPID">An accessible target process ID.</param>
        /// <returns>The identity of the target owner.</returns>
        /// <exception cref="AccessViolationException">
        /// The given process is not accessible.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// The given process does not exist.
        /// </exception>
        public static WindowsIdentity GetProcessIdentity(Int32 InTargetPID)
        {
            IntPtr hToken;
            WindowsIdentity Result;

            NativeAPI.RhGetProcessToken(InTargetPID, out hToken);

            try
            {
                Result = new WindowsIdentity(hToken);
            }
            finally
            {
                NativeAPI.CloseHandle(hToken);
            }

            return Result;
        }

        /// <summary>
        /// Returns the current native system process ID.
        /// </summary>
        /// <returns>The native system process ID.</returns>
        public static Int32 GetCurrentProcessId()
        { return NativeAPI.GetCurrentProcessId(); }

        /// <summary>
        /// Returns the current native system thread ID. 
        /// </summary>
        /// <remarks>
        /// Even if currently each dedicated managed
        /// thread (not a thread from a <see cref="ThreadPool"/>) exactly maps to one native
        /// system thread, this behavior may change in future versions. 
        /// If you would like to have unintercepted threads, you should make sure that they are
        /// dedicated ones, e.g. derived from <see cref="Thread"/>.
        /// </remarks>
        /// <returns>The native system thread ID.</returns>
        public static Int32 GetCurrentThreadId()
        { return NativeAPI.GetCurrentThreadId(); }

        /// <summary>
        /// Will execute the given static method under system privileges. 
        /// </summary>
        /// <remarks>
        /// <para>
        /// For some tasks it is necessary to have unrestricted access to the windows API.
        /// For example if you want to enumerate all running processes in all sessions. But
        /// keep in mind that you only can access these information within the given static
        /// method and only if it is called through this service.
        /// </para><para>
        /// To accomplish this task, your assembly is loaded into a system service which
        /// executes the given static method in a remoted manner. This implies that the
        /// return type shall be marked with <see cref="SerializableAttribute"/>. All
        /// handles or other process specific things obtained in the service, will be invalid 
        /// in your application after the call is completed! Also the service will use
        /// a new instance of your class, so you should only rely on the given parameters
        /// and avoid using any external variables.. Your method shall be threaded as isolated!
        /// </para><para>
        /// The next thing to mention is that all assemblies required for executing the method
        /// shall either be in the GAC or in the directory of the related EasyHook-Library.
        /// Otherwise the service won't be able to use your assembly!
        /// </para><para>
        /// All unhandled exceptions will be rethrown by the local <see cref="ExecuteAsService"/>.
        /// </para>
        /// </remarks>
        /// <typeparam name="TClass">A class containing the given static method.</typeparam>
        /// <param name="InMethodName">A public static method exposed by the given public class.</param>
        /// <param name="InParams">A list of serializable parameters being passed to your static method.</param>
        /// <returns>The same value your method is returning or <c>null</c> if a <c>void</c> method is called.</returns>
        /// <exception cref="AccessViolationException">
        /// The current user is not an administrator.
        /// </exception>
        /// <include file='ExecuteAsService.xml' path='example'/>
        public static Object ExecuteAsService<TClass>(
                String InMethodName,
                params Object[] InParams)
        {
            return ServiceMgmt.ExecuteAsService<TClass>(InMethodName, InParams);
        }

        /// <summary>
        /// Creates a new process which is started suspended until you call <see cref="WakeUpProcess"/>
        /// from within your injected library <c>Run()</c> method. This allows you to hook the target
        /// BEFORE any of its usual code is executed. In situations where a target has debugging and
        /// hook preventions, you will get a chance to block those mechanisms for example...
        /// </summary>
        /// <remarks>
        /// <para>
        /// Please note that this method might fail when injecting into managed processes, especially
        /// when the target is using the CLR hosting API and takes advantage of AppDomains. For example,
        /// the Internet Explorer won't be hookable with this method. In such a case your only options
        /// are either to hook the target with the unmanaged API or to hook it after (non-supended) creation 
        /// with the usual <see cref="Inject"/> method.
        /// </para>
        /// <para>
        /// See <see cref="Inject"/> for more information. The exceptions listed here are additional
        /// to the ones listed for <see cref="Inject"/>.
        /// </para>
        /// </remarks>
        /// <param name="InEXEPath">
        /// A relative or absolute path to the desired executable.
        /// </param>
        /// <param name="InCommandLine">
        /// Optional command line parameters for process creation.
        /// </param>
        /// <param name="InProcessCreationFlags">
        /// Internally CREATE_SUSPENDED is already passed to CreateProcess(). With this
        /// parameter you can add more flags like DETACHED_PROCESS, CREATE_NEW_CONSOLE or
        /// whatever!
        /// </param>
        /// <param name="InOptions">
        /// A valid combination of options.
        /// </param>
        /// <param name="InLibraryPath_x86">
        /// A partially qualified assembly name or a relative/absolute file path of the 32-bit version of your library. 
        /// For example "MyAssembly, PublicKeyToken=248973975895496" or ".\Assemblies\\MyAssembly.dll". 
        /// </param>
        /// <param name="InLibraryPath_x64">
        /// A partially qualified assembly name or a relative/absolute file path of the 64-bit version of your library. 
        /// For example "MyAssembly, PublicKeyToken=248973975895496" or ".\Assemblies\\MyAssembly.dll". 
        /// </param>
        /// <param name="OutProcessId">
        /// The process ID of the newly created process.
        /// </param>
        /// <param name="InPassThruArgs">
        /// A serializable list of parameters being passed to your library entry points <c>Run()</c> and
        /// <c>Initialize()</c>.
        /// </param>
        /// <exception cref="ArgumentException">
        /// The given EXE path could not be found.
        /// </exception>
        public static void CreateAndInject(
            String InEXEPath,
            String InCommandLine,
            Int32 InProcessCreationFlags,
            InjectionOptions InOptions,
            String InLibraryPath_x86,
            String InLibraryPath_x64,
            out Int32 OutProcessId,
            params Object[] InPassThruArgs)
        {
            Int32 RemotePID;
            Int32 RemoteTID;

            // create suspended process...
            NativeAPI.RtlCreateSuspendedProcess(
                InEXEPath,
                InCommandLine,
                InProcessCreationFlags,
                out RemotePID,
                out RemoteTID);

            try
            {
                InjectEx(
                    NativeAPI.GetCurrentProcessId(),
                    RemotePID,
                    RemoteTID,
                    0x20000000,
                    InLibraryPath_x86,
                    InLibraryPath_x64,
                    ((InOptions & InjectionOptions.NoWOW64Bypass) == 0),
                    ((InOptions & InjectionOptions.NoService) == 0),
                    ((InOptions & InjectionOptions.DoNotRequireStrongName) == 0),
                    InPassThruArgs);

                OutProcessId = RemotePID;
            }
            catch (Exception e)
            {
                try
                {
                    Process.GetProcessById(RemotePID).Kill();
                }
                catch (Exception) { }

                throw e;
            }
        }

        /// <summary>
        /// Creates a new process which is started suspended until you call <see cref="WakeUpProcess"/>
        /// from within your injected library <c>Run()</c> method. This allows you to hook the target
        /// BEFORE any of its usual code is executed. In situations where a target has debugging and
        /// hook preventions, you will get a chance to block those mechanisms for example...
        /// </summary>
        /// <remarks>
        /// <para>
        /// Please note that this method might fail when injecting into managed processes, especially
        /// when the target is using the CLR hosting API and takes advantage of AppDomains. For example,
        /// the Internet Explorer won't be hookable with this method. In such a case your only options
        /// are either to hook the target with the unmanaged API or to hook it after (non-supended) creation 
        /// with the usual <see cref="Inject"/> method.
        /// </para>
        /// <para>
        /// See <see cref="Inject"/> for more information. The exceptions listed here are additional
        /// to the ones listed for <see cref="Inject"/>.
        /// </para>
        /// </remarks>
        /// <param name="InEXEPath">
        /// A relative or absolute path to the desired executable.
        /// </param>
        /// <param name="InCommandLine">
        /// Optional command line parameters for process creation.
        /// </param>
        /// <param name="InProcessCreationFlags">
        /// Internally CREATE_SUSPENDED is already passed to CreateProcess(). With this
        /// parameter you can add more flags like DETACHED_PROCESS, CREATE_NEW_CONSOLE or
        /// whatever!
        /// </param>
        /// <param name="InLibraryPath_x86">
        /// A partially qualified assembly name or a relative/absolute file path of the 32-bit version of your library. 
        /// For example "MyAssembly, PublicKeyToken=248973975895496" or ".\Assemblies\\MyAssembly.dll". 
        /// </param>
        /// <param name="InLibraryPath_x64">
        /// A partially qualified assembly name or a relative/absolute file path of the 64-bit version of your library. 
        /// For example "MyAssembly, PublicKeyToken=248973975895496" or ".\Assemblies\\MyAssembly.dll". 
        /// </param>
        /// <param name="OutProcessId">
        /// The process ID of the newly created process.
        /// </param>
        /// <param name="InPassThruArgs">
        /// A serializable list of parameters being passed to your library entry points <c>Run()</c> and
        /// <c>Initialize()</c>.
        /// </param>
        /// <exception cref="ArgumentException">
        /// The given EXE path could not be found.
        /// </exception>
        public static void CreateAndInject(
            String InEXEPath,
            String InCommandLine,
            Int32 InProcessCreationFlags,
            String InLibraryPath_x86,
            String InLibraryPath_x64,
            out Int32 OutProcessId,
            params Object[] InPassThruArgs)
        {
            CreateAndInject(
                InEXEPath, 
                InCommandLine, 
                InProcessCreationFlags, 
                InjectionOptions.NoService, 
                InLibraryPath_x86, 
                InLibraryPath_x64, 
                out OutProcessId, 
                InPassThruArgs);
        }

        /// <summary>
        /// Returns <c>true</c> if the operating system is 64-Bit Windows, <c>false</c> otherwise.
        /// </summary>
        public static Boolean IsX64System { get { return NativeAPI.RhIsX64System(); } }

        /// <summary>
        /// Installs the EasyHook support driver. After this step you may use
        /// <see cref="InstallDriver"/> to install your kernel mode hooking component.
        /// </summary>
        public static void InstallSupportDriver() { NativeAPI.RhInstallSupportDriver(); }

        /// <summary>
        /// Loads the given driver into the kernel and immediately marks it for deletion.
        /// The installed driver will be registered with the service control manager under the
        /// <paramref name="InDriverName"/> you specify.
        /// Please note that you should use <see cref="IsX64System"/> to find out which
        /// driver to load. Even if your process is running on 32-Bit this does not mean,
        /// that the OS kernel is running on 32-Bit!
        /// </summary>
        /// <param name="InDriverPath"></param>
        /// <param name="InDriverName"></param>
        public static void InstallDriver(
            String InDriverPath,
            String InDriverName) 
        { 
            NativeAPI.RhInstallDriver(InDriverPath, InDriverName); 
        }
    }
}
