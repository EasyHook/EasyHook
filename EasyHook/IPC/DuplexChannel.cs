/*
    EasyHook - The reinvention of Windows API hooking
 
    Copyright (C) 2009-2010 EasyHook

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
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
using System.Runtime.Serialization.Formatters;
using System.Security.AccessControl;
using System.Security.Principal;

namespace EasyHook.IPC
{
  /// <summary>
  /// <see cref="DuplexChannel{TRemoteEndPoint}"/> provides a full duplex channel
  /// for communication between different processes and/or application domains.
  /// </summary>
  /// <typeparam name="TRemoteEndPoint"></typeparam>
  internal class DuplexChannel<TRemoteEndPoint>
    where TRemoteEndPoint : DuplexChannelEndPointObject
  {

    #region Variables

    /// <summary>
    /// The properties of the channel instantiated by the local endpoint.
    /// </summary>
    private readonly ChannelProperties _localChannelProperties;
    /// <summary>
    /// The properties of the channel instantiated by the remote endpoint.
    /// </summary>
    private readonly ChannelProperties _remoteChannelProperties;
    /// <summary>
    /// The configuration of the local endpoint's server.
    /// </summary>
    private readonly EndPointConfigurationData<TRemoteEndPoint> _localEndPointConfig;
    /// <summary>
    /// The <see cref="IpcServerChannel"/> instantiated by the current <see cref="DuplexChannel{TRemoteEndPoint}"/>.
    /// </summary>
    private IpcServerChannel _serverChannel;
    /// <summary>
    /// The <see cref="DuplexChannelState"/> of the current <see cref="DuplexChannel{TRemoteEndPoint}"/>.
    /// </summary>
    private DuplexChannelState _state;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the <see cref="DuplexChannelState"/> of the current <see cref="DuplexChannel{TRemoteEndPoint}"/>.
    /// </summary>
    public DuplexChannelState State
    {
      get { return _state; }
    }

    /// <summary>
    /// Gets the full URL of the local endpoint server.
    /// </summary>
    public string LocalEndPointUrl
    {
      get { return _localChannelProperties.Url; }
    }

    /// <summary>
    /// Gets whether or not the local endpoint is the channel's instantiator.
    /// </summary>
    public bool IsLocalEndPointInstantiator
    {
      get { return _localChannelProperties.IsInstantiatorChannel; }
    }

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a new full duplex channel, in which the current <see cref="AppDomain"/> is the instantiator.
    /// </summary>
    /// <param name="localEndPointConfig">Configuration for the local endpoint.</param>
    public DuplexChannel(EndPointConfigurationData<TRemoteEndPoint> localEndPointConfig)
      : this(localEndPointConfig, ChannelProperties.CreateRandomChannelProperties())
    {
    }

    /// <summary>
    /// Creates a new full duplex channel, in which the remote endpoint's <see cref="AppDomain"/> is the instantiator.
    /// </summary>
    /// <param name="localEndPointConfig">Configuration for the local endpoint.</param>
    /// <param name="otherChannelUrl">The url to the remote endpoint, which is the channel instantiator.</param>
    public DuplexChannel(EndPointConfigurationData<TRemoteEndPoint> localEndPointConfig, string otherChannelUrl)
      : this(localEndPointConfig, ChannelProperties.InitializeFromUrl(otherChannelUrl).GetRemoteEndpointChannelProperties())
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="DuplexChannel{TRemoteEndPoint}"/> based on the given data.
    /// </summary>
    /// <param name="localEndPointConfig"></param>
    /// <param name="serverChannelProperties"></param>
    internal DuplexChannel(EndPointConfigurationData<TRemoteEndPoint> localEndPointConfig, ChannelProperties serverChannelProperties)
    {
      AssertEndpointConfigurationData(localEndPointConfig, "localEndpointConfig");
      _localEndPointConfig = localEndPointConfig;
      _localChannelProperties = serverChannelProperties;
      _remoteChannelProperties = serverChannelProperties.GetRemoteEndpointChannelProperties();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Initializes the full duplex connection and calls <paramref name="onConnectionReady"/> when the connection is in a full duplex <see cref="State"/>.
    /// </summary>
    /// <param name="onConnectionReady"></param>
    public void InitializeConnection(DuplexChannelReadyEventHandler<TRemoteEndPoint> onConnectionReady)
    {
      CreateServer();
      CreateClient(onConnectionReady);
    }

    /// <summary>
    /// Returns a new proxy to the remote endpoint object.
    /// </summary>
    /// <returns></returns>
    public TRemoteEndPoint GetRemoteEndPoint()
    {
      return GetRemoteEndPoint(true);
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Creates the server side of the current duplex channel.
    /// </summary>
    /// <remarks>
    /// In general, this method must always be called before calling <see cref="CreateClient"/>.
    /// </remarks>
    private void CreateServer()
    {
      var provider = new BinaryServerFormatterSinkProvider { TypeFilterLevel = TypeFilterLevel.Full };
      var securityDescriptor = CreateSecurityDescriptor(_localEndPointConfig.AllowedClients);
      _serverChannel = new IpcServerChannel(_localChannelProperties.AsDictionary(), provider, securityDescriptor);
      ChannelServices.RegisterChannel(_serverChannel, false);
      RemotingConfiguration.RegisterWellKnownServiceType(_localEndPointConfig.RemoteObjectType,
                                                         _localChannelProperties.EndPointName,
                                                         _localEndPointConfig.ObjectMode);
      _state |= DuplexChannelState.ServerUp;
    }

    /// <summary>
    /// Initializes the client side of the current duplex channel.
    /// <paramref name="callback"/> is called when the clientside is known to be up.
    /// </summary>
    /// <remarks>
    /// In general, <see cref="CreateServer"/> must always be called before using this method.
    /// </remarks>
    /// <param name="callback"></param>
    private void CreateClient(DuplexChannelReadyEventHandler<TRemoteEndPoint> callback)
    {
      if (_remoteChannelProperties.IsInstantiatorChannel
          // The remote endpoint instantiated the channel and must already have set up its server,
          // therefore the remote endpoint is always expected to be ready to be connected to.
          // Verify this by attempting a connection.
          && TryConnectClient())
      {
        callback(this);
      }
      else
      {
        // Subscribe to the server object using reflection.
        // When the client is connected, it signals the server that its own server is ready to be connected to.
        DuplexChannelEndPointObject.SubscribeEndPointReadyEvent(_localEndPointConfig.RemoteObjectType,
                                                                OnRemoteEndPointSignaledReady,
                                                                callback);
      }
    }

    /// <summary>
    /// Handles the event of the remote endpoint signalling its ready state.
    /// </summary>
    /// <param name="state"></param>
    /// <param name="args"></param>
    private void OnRemoteEndPointSignaledReady(object state, EventArgs args)
    {
      if (!TryConnectClient())
        return; // Remote endpoint is not ready, why did it signal?
      var callback = state as DuplexChannelReadyEventHandler<TRemoteEndPoint>;
      if (callback != null)
        callback(this);
    }

    /// <summary>
    /// Tries to connect as a client in the current duplex channel.
    /// <see cref="_state"/> is set accordingly.
    /// </summary>
    /// <returns></returns>
    private bool TryConnectClient()
    {
      try
      {
        var remoteEndPoint = GetRemoteEndPoint(false);
        remoteEndPoint.SignalEndpointReady();
        _state |= DuplexChannelState.ClientUp;
        return true;
      }
      catch (RemotingException)
      {
        return false;
      }
    }

    /// <summary>
    /// Returns an instance to the remote endpoint.
    /// </summary>
    /// <exception cref="ApplicationException">
    /// An <see cref="ApplicationException"/> is thrown if <paramref name="checkChannelStateFirst"/> is true
    /// and <see cref="_state"/> doesn't specify <see cref="DuplexChannelState.ClientUp"/>.
    /// </exception>
    /// <exception cref="RemotingException">
    /// A <see cref="RemotingException"/> is thrown if no valid proxy can be created.
    /// </exception>
    /// <param name="checkChannelStateFirst">Specifies whether or not <see cref="_state"/> must be checked first.</param>
    /// <returns></returns>
    private TRemoteEndPoint GetRemoteEndPoint(bool checkChannelStateFirst)
    {
      if (checkChannelStateFirst
          && (_state & DuplexChannelState.ClientUp) != DuplexChannelState.ClientUp)
        throw new ApplicationException("Unable to return remote endpoint due to channel state " + _state);
      // Get proxy.
      var remoteObject = Activator.GetObject(typeof (TRemoteEndPoint), _remoteChannelProperties.Url) as TRemoteEndPoint;
      // Verify the proxy instance.
      if (remoteObject != null)
        remoteObject.Ping();
      else
        throw new RemotingException("Unable to create remote interface of type " + typeof (TRemoteEndPoint));
      return remoteObject;
    }

    #endregion

    #region Private Static Methods

    /// <summary>
    /// Asserts the given <see cref="EndPointConfigurationData{TRemoteEndPoint}"/> to be valid.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// And <see cref="ArgumentException"/> is thrown if any of <paramref name="configData"/>'s properties is invalid.
    /// The given <paramref name="paramName"/> is set to <see cref="ArgumentException.ParamName"/>.
    /// </exception>
    /// <param name="configData"></param>
    /// <param name="paramName"></param>
    private static void AssertEndpointConfigurationData(EndPointConfigurationData<TRemoteEndPoint> configData, string paramName)
    {
      if (configData.AllowedClients == null)
        throw new ArgumentException("The given EndPointConfigurationData specifies an illegal value for AllowedClients",
                                    paramName);
    }

    /// <summary>
    /// Returns a default <see cref="CommonSecurityDescriptor"/> based on the given collection of <see cref="WellKnownSidType"/>.
    /// </summary>
    /// <param name="allowedClients"></param>
    /// <returns></returns>
    private static CommonSecurityDescriptor CreateSecurityDescriptor(ICollection<WellKnownSidType> allowedClients)
    {
      var dacl = new DiscretionaryAcl(false, false, allowedClients.Count);
      foreach (var sid in allowedClients)
      {
        var securityId = new SecurityIdentifier(sid, null);
        dacl.AddAccess(AccessControlType.Allow, securityId, -1, InheritanceFlags.None, PropagationFlags.None);
      }
      const ControlFlags controlFlags =
        ControlFlags.GroupDefaulted | ControlFlags.OwnerDefaulted | ControlFlags.DiscretionaryAclPresent;
      var securityDescriptor = new CommonSecurityDescriptor(false, false, controlFlags, null, null, null, dacl);
      return securityDescriptor;
    }

    #endregion

  }
}
