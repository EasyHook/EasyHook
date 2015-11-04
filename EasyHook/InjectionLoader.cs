// EasyHook (File: EasyHook\InjectionLoader.cs)
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
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Runtime.InteropServices;

namespace EasyHook
{
#pragma warning disable 1591
#pragma warning disable 0618
#pragma warning disable 0028

    public class InjectionLoader
    {
        #region Private Classes

        [StructLayout(LayoutKind.Sequential)]
        class REMOTE_ENTRY_INFO : RemoteHooking.IContext
        {
            public Int32 m_HostPID;
            public IntPtr UserData;
            public Int32 UserDataSize;

            #region IContext Members

            public Int32 HostPID { get { return m_HostPID; } }

            #endregion
        };

        /// <summary>
        /// Wraps the data needed for the connection to the host.
        /// </summary>
        private class HostConnectionData
        {

            #region Enums

            public enum ConnectionState
            {
                Invalid = 0,
                NoChannel = 1,
                Valid = Int32.MaxValue
            }

            #endregion

            #region Variables

            private REMOTE_ENTRY_INFO _unmanagedInfo;
            private HelperServiceInterface _helperInterface;
            private ManagedRemoteInfo _remoteInfo;
            private ConnectionState _state;

            #endregion

            #region Properties

            /// <summary>
            /// Gets the state of the current <see cref="HostConnectionData"/>.
            /// </summary>
            public ConnectionState State
            {
                get { return _state; }
            }

            /// <summary>
            /// Gets the unmanaged data containing the pointer to the memory block containing <see cref="RemoteInfo"/>;
            /// </summary>
            public REMOTE_ENTRY_INFO UnmanagedInfo
            {
                get { return _unmanagedInfo; }
            }

            public HelperServiceInterface HelperInterface
            {
                get { return _helperInterface; }
            }

            public ManagedRemoteInfo RemoteInfo
            {
                get { return _remoteInfo; }
            }

            #endregion

            #region Constructors

            private HostConnectionData()
            {
                _state = ConnectionState.Invalid;
                _helperInterface = null;
                _remoteInfo = null;
                _unmanagedInfo = null;
            }

            #endregion

            #region Public Methods

            /// <summary>
            /// Loads <see cref="HostConnectionData"/> from the <see cref="IntPtr"/> specified.
            /// </summary>
            /// <param name="unmanagedInfoPointer"></param>
            public static HostConnectionData LoadData(IntPtr unmanagedInfoPointer)
            {
                var data = new HostConnectionData
                {
                    _state = ConnectionState.Valid,
                    _unmanagedInfo = new REMOTE_ENTRY_INFO()
                };
                try
                {
                    // Get the unmanaged data
                    Marshal.PtrToStructure(unmanagedInfoPointer, data._unmanagedInfo);
                    using (Stream passThruStream = new MemoryStream())
                    {
                        byte[] passThruBytes = new byte[data._unmanagedInfo.UserDataSize];
                        BinaryFormatter format = new BinaryFormatter();
                        // Workaround for deserialization when not using GAC registration
                        format.Binder = new AllowAllAssemblyVersionsDeserializationBinder();
                        Marshal.Copy(data._unmanagedInfo.UserData, passThruBytes, 0, data._unmanagedInfo.UserDataSize);
                        passThruStream.Write(passThruBytes, 0, passThruBytes.Length);
                        passThruStream.Position = 0;
                        data._remoteInfo = (ManagedRemoteInfo)format.Deserialize(passThruStream);
                    }
                    // Connect the HelperServiceInterface
                    data._helperInterface = RemoteHooking.IpcConnectClient<HelperServiceInterface>(data._remoteInfo.ChannelName);
                    // Ensure that the connection is working...
                    data._helperInterface.Ping();
                    if (!_connectedChannels.Contains(data._remoteInfo.ChannelName))
                    {
                        _connectedChannels.Add(data._remoteInfo.ChannelName);
                        return new HostConnectionData { _state = ConnectionState.NoChannel };
                    }
                }
                catch (Exception ExtInfo)
                {
                    Config.PrintError(ExtInfo.ToString());
                    return new HostConnectionData { _state = ConnectionState.Invalid };
                }
                return data;
            }

            #endregion
        }

        /// <summary>
        /// When not using the GAC, the BinaryFormatter fails to recognise the InParam
        /// when attempting to deserialise. 
        /// 
        /// A custom DeserializationBinder works around this (see http://spazzarama.com/2009/06/25/binary-deserialize-unable-to-find-assembly/)
        /// </summary>
        private sealed class AllowAllAssemblyVersionsDeserializationBinder :
            System.Runtime.Serialization.SerializationBinder
        {
            private Assembly _assembly;
            public AllowAllAssemblyVersionsDeserializationBinder()
                : this(Assembly.GetExecutingAssembly())
            {
            }

            public AllowAllAssemblyVersionsDeserializationBinder(Assembly assembly)
            {
                _assembly = assembly;
            }

            public override Type BindToType(string assemblyName, string typeName)
            {
                Type typeToDeserialize = null;

                try
                {
                    // 1. First try to bind without overriding assembly
                    typeToDeserialize = Type.GetType(String.Format("{0}, {1}", typeName, assemblyName));
                }
                catch
                {
                    // 2. Failed to find assembly or type, now try with overridden assembly
                    typeToDeserialize = _assembly.GetType(typeName);
                }

                return typeToDeserialize;
            }
        }

        #endregion

        private static readonly List<String> _connectedChannels = new List<String>();

        #region Public Methods

        public static int Main(String inParam)
        {
            if (inParam == null) return 0;
            var ptr = (IntPtr)Int64.Parse(inParam, System.Globalization.NumberStyles.HexNumber);
            var connection = HostConnectionData.LoadData(ptr);
            if (connection.State != HostConnectionData.ConnectionState.Valid)
                return (int)connection.State;

            // Adjust host PID in case of WOW64 bypass and service help...
            connection.UnmanagedInfo.m_HostPID = connection.RemoteInfo.HostPID;

            try
            {
                // Prepare parameter array.
                var paramArray = new object[1 + connection.RemoteInfo.UserParams.Length];
                // The next type cast is not redundant because the object needs to be an explicit IContext
                // when passed as a parameter to the IEntryPoint constructor and Run() methods.
                paramArray[0] = (RemoteHooking.IContext)connection.UnmanagedInfo;
                for (int i = 0; i < connection.RemoteInfo.UserParams.Length; i++)
                    paramArray[i + 1] = connection.RemoteInfo.UserParams[i];
                // Note: at this point all but the first parameter are still binary encoded.
                // Load the user library and initialize the IEntryPoint.
                return LoadUserLibrary(connection.RemoteInfo.UserLibraryName, connection.RemoteInfo.UserLibrary, paramArray, connection.HelperInterface);
            }
            catch (Exception ExtInfo)
            {
                Config.PrintWarning(ExtInfo.ToString());
                return -1;
            }
            finally
            {
                if (_connectedChannels.Contains(connection.RemoteInfo.ChannelName))
                    _connectedChannels.Remove(connection.RemoteInfo.ChannelName);
            }
        }

        #endregion

        #region Private Methods

        private static void Release(Type InEntryPoint)
        {
            if (InEntryPoint == null)
                return;

            LocalHook.Release();
        }

        /// <summary>
        /// Loads the user library (trying the strong name first, then the file name),
        /// creates an instance for the <see cref="IEntryPoint"/> specified in the library
        /// and invokes the Run() method specified in that instance.
        /// </summary>
        /// <param name="userLibraryStrongName">The assembly strong name provided by the user, located in the global assembly cache.</param>
        /// <param name="userLibraryFileName">The assembly file name provided by the user to be loaded.</param>
        /// <param name="paramArray">Array of parameters to use with the constructor and with the Run() method. Note that all but the first parameter should be binary encoded.</param>
        /// <param name="helperServiceInterface"><see cref="HelperServiceInterface"/> to use for reporting to the host side.</param>
        /// <returns>The exit code to be returned by the main() method.</returns>
        private static int LoadUserLibrary(string userLibraryStrongName, string userLibraryFileName, object[] paramArray, HelperServiceInterface helperServiceInterface)
        {
            Type entryPoint = null;
            MethodInfo runMethod;
            object instance;

            // 1. Load the assembly and find the EasyHook.IEntryPoint and matching Run() method
            try
            {
                // Load the given assembly and find the first EasyHook.IEntryPoint
                entryPoint = FindEntryPoint(userLibraryStrongName, userLibraryFileName);

                // Only attempt to deserialise parameters after we have loaded the userAssembly
                // this allows types from the userAssembly to be passed as parameters
                BinaryFormatter format = new BinaryFormatter();
                format.Binder = new AllowAllAssemblyVersionsDeserializationBinder(entryPoint.Assembly);
                for (int i = 1; i < paramArray.Length; i++)
                {
                    using (MemoryStream ms = new MemoryStream((byte[])paramArray[i]))
                    {
                        paramArray[i] = format.Deserialize(ms);
                    }
                }
                
                // Determine if a Run() method is defined with matching parameters, before initializing an instance for the type.
                runMethod = FindMatchingMethod(entryPoint, "Run", paramArray);
                if (runMethod == null)
                    throw new MissingMethodException(ConstructMissingMethodExceptionMessage("Run", paramArray));
                // Initialize an object for the entrypoint.
                instance = InitializeInstance(entryPoint, paramArray);
                if (instance == null)
                    throw new MissingMethodException(ConstructMissingMethodExceptionMessage(entryPoint.Name, paramArray));
                // Notify the host about successful injection.
                helperServiceInterface.InjectionCompleted(RemoteHooking.GetCurrentProcessId());
            }
            catch (Exception e)
            {
                // Provide error information on host side.
                try
                {
                    helperServiceInterface.InjectionException(RemoteHooking.GetCurrentProcessId(), e);
                }
                finally
                {
                    Release(entryPoint);
                }
                return -1;
            }

            // 2. Invoke the Run() method
            try
            {
                // After this it is safe to enter the Run() method, which will block until assembly unloading...
                // From now on the user library has to take care about error reporting!
                runMethod.Invoke(instance, BindingFlags.Public | BindingFlags.Instance | BindingFlags.ExactBinding |
                                           BindingFlags.InvokeMethod, null, paramArray, null);
            }
            finally
            {
                Release(entryPoint);
            }
            return 0;
        }

        /// <summary>
        /// Finds the <see cref="IEntryPoint"/> in the specified <see cref="Assembly"/>.
        /// </summary>
        /// <exception cref="EntryPointNotFoundException">
        /// An <see cref="EntryPointNotFoundException"/> is thrown if the given user library does not export a proper type implementing
        /// the <see cref="IEntryPoint"/> interface.
        /// </exception>
        /// <param name="userAssemblyStrongName">The strong name of the assembly provided by the user.</param>
        /// <param name="userAssemblyFileName">The file name of the assembly provided by the user.</param>
        /// <returns>The <see cref="Type"/> functioning as <see cref="IEntryPoint"/> for the user provided <see cref="Assembly"/>.</returns>
        private static Type FindEntryPoint(string userAssemblyStrongName, string userAssemblyFileName)
        {
            Assembly userAssembly = null;
            StringBuilder errors = new StringBuilder();

            // First try to find the assembly using a strong name (if provided)
            if (!String.IsNullOrEmpty(userAssemblyStrongName))
            {
                try
                {
                    userAssembly = Assembly.Load(userAssemblyStrongName);
                    Config.PrintComment("SUCCESS: Assembly.Load({0})", userAssemblyStrongName);
                }
                catch (Exception e)
                {
                    Config.PrintComment("FAIL: Assembly.Load({0}) - {1}", userAssemblyStrongName, e.ToString());
                    errors.AppendFormat("Failed to load assembly using strong name {0} - {1}\r\n", userAssemblyStrongName, e.ToString());
                }
            }

            // If loading by strong name failed, attempt to load the assembly from its file location
            if (userAssembly == null)
            {
                try
                {
                    userAssembly = Assembly.LoadFrom(userAssemblyFileName);
                    Config.PrintComment("SUCCESS: Assembly.LoadFrom({0})", userAssemblyFileName);
                }
                catch (Exception e)
                {
                    Config.PrintComment("FAIL: Assembly.LoadFrom({0}) - {1}", userAssemblyFileName, e.ToString());
                    errors.AppendFormat("Failed to load assembly from file {0} - {1}", userAssemblyFileName, e.ToString());
                }
            }

            // If both attempts fail, raise an exception with the failed attempts.
            if (userAssembly == null)
            {
                Config.PrintError("Could not load assembly {0}, {1}", userAssemblyFileName, userAssemblyStrongName);
                throw new Exception(errors.ToString());
            }

            // Find the first EasyHook.IEntryPoint
            var exportedTypes = userAssembly.GetExportedTypes();
            for (int i = 0; i < exportedTypes.Length; i++)
            {
                if (exportedTypes[i].GetInterface("EasyHook.IEntryPoint") != null)
                    return exportedTypes[i];
            }
            throw new EntryPointNotFoundException("The given library does not include a public class implementing the 'EasyHook.IEntryPoint' interface.");
        }

        /// <summary>
        /// Finds a user defined Run() method in the specified <see cref="Type"/> matching the specified <paramref name="paramArray"/>.
        /// </summary>
        /// <param name="methodName">Name of the method to search.</param>
        /// <param name="objectType"><see cref="Type"/> to extract the method from.</param>
        /// <param name="paramArray">Array of parameters to match to the method's defined parameters.</param>
        /// <returns><see cref="MethodInfo"/> for the matching method, if any.</returns>
        private static MethodInfo FindMatchingMethod(Type objectType, string methodName, object[] paramArray)
        {
            var methods = objectType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            foreach (var method in methods)
            {
                if (method.Name == methodName
                    && MethodMatchesParameters(method, paramArray))
                    return method;
            }
            return null;
        }

        /// <summary>
        /// Initializes an instance from the specified <see cref="Type"/> using the specified <paramref name="parameters"/>.
        /// </summary>
        /// <param name="objectType"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        private static object InitializeInstance(Type objectType, object[] parameters)
        {
            var constructors = objectType.GetConstructors();
            foreach (var constructor in constructors)
            {
                if (MethodMatchesParameters(constructor, parameters))
                    return constructor.Invoke(parameters);
            }
            return null;
        }

        /// <summary>
        /// Returns whether the specified <paramref name="paramArray"/> can be used as parameters when invoking the specified <paramref name="method"/>.
        /// </summary>
        /// <param name="method"></param>
        /// <param name="paramArray"></param>
        /// <returns></returns>
        private static bool MethodMatchesParameters(MethodBase method, object[] paramArray)
        {
            var parameters = method.GetParameters();
            if (parameters.Length != paramArray.Length) return false;
            for (int i = 0; i < paramArray.Length; i++)
            {
                if (!parameters[i].ParameterType.IsInstanceOfType(paramArray[i]))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Constructs a message for a <see cref="MissingMethodException"/> containing more specific information about the expected paramaters.
        /// </summary>
        /// <param name="methodName">Name of the missing method.</param>
        /// <param name="paramArray">Array of the expected parameters.</param>
        /// <returns></returns>
        private static string ConstructMissingMethodExceptionMessage(string methodName, object[] paramArray)
        {
            var msg = new StringBuilder("The given user library does not export a proper " + methodName + "(");
            foreach (var param in paramArray)
                msg.Append(param.GetType() + ", ");
            return msg.ToString(0, msg.Length - 2) + ") method in the 'EasyHook.IEntryPoint' interface.";
        }
        #endregion
    }
}
