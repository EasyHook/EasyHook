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
using EasyHook.Domain;
using DomainChannel = EasyHook.IPC.DuplexChannel<EasyHook.IPC.DomainConnectionEndPoint>;

namespace EasyHook.IPC
{
  /// <summary>
  /// Class managing all connections between the current <see cref="AppDomain"/> and any other <see cref="AppDomain"/>,
  /// which might even run in a different process than the current one.
  /// </summary>
  public class ConnectionManager
  {

    #region Variables

    /// <summary>
    /// A collection of all known connections between the current application domain and any other domains.
    /// </summary>
    private readonly IDictionary<DomainIdentifier, DomainChannel> _domainConnections;
    /// <summary>
    /// Syncroot for <see cref="_domainConnections"/>.
    /// </summary>
    private readonly object _domainConnectionsSyncRoot;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of <see cref="ConnectionManager"/>.
    /// </summary>
    internal ConnectionManager()
    {
      _domainConnections = new Dictionary<DomainIdentifier, DuplexChannel<DomainConnectionEndPoint>>();
      _domainConnectionsSyncRoot = new object();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Returns whether a connection can be made to the specified remote domain.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public bool CanConnect(DomainIdentifier id)
    {
      lock (_domainConnectionsSyncRoot)
        return _domainConnections.ContainsKey(id);
    }

    /// <summary>
    /// Returns a new proxy to an object of type <typeparamref name="TEndPoint"/>
    /// located in the <see cref="AppDomain"/> with the specified identifier.
    /// </summary>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="RemotingException"></exception>
    /// <typeparam name="TEndPoint">The type of the object to get access to.</typeparam>
    /// <param name="id">The identifier of the targetted application domain.</param>
    /// <returns></returns>
    public TEndPoint Connect<TEndPoint>(DomainIdentifier id)
      where TEndPoint : EndPointObject
    {
      DomainConnectionEndPoint remoteDomainEndPoint;
      lock (_domainConnectionsSyncRoot)
      {
        if (!_domainConnections.ContainsKey(id))
          throw new ArgumentException("Unable to connect to remote domain with id " + id);
        remoteDomainEndPoint = _domainConnections[id].GetRemoteEndPoint();
      }
      var objectUri = remoteDomainEndPoint.CreateChannel<TEndPoint>();
      var result = Activator.GetObject(typeof (TEndPoint), objectUri) as TEndPoint;
      if (result == null)
        throw new RemotingException();
      result.Ping();
      return result;
    }

    /// <summary>
    /// Tries to create a new proxy to an object of type <typeparamref name="TEndPoint"/>
    /// located in the <see cref="AppDomain"/> with the specified identifier.
    /// </summary>
    /// <typeparam name="TEndPoint">The type of the object to get access to.</typeparam>
    /// <param name="id">The identifier of the targetted application domain.</param>
    /// <param name="result">The newly created proxy.</param>
    /// <returns></returns>
    public bool TryConnect<TEndPoint>(DomainIdentifier id, out TEndPoint result)
      where TEndPoint : EndPointObject
    {
      if (!CanConnect(id))
      {
        result = null;
        return false;
      }
      try
      {
        result = Connect<TEndPoint>(id);
        return true;
      }
      catch
      {
        result = null;
        return false;
      }
    }

    #endregion

    #region Internal Methods

    /// <summary>
    /// Initializes a new IPC connection which has a <see cref="DomainConnectionEndPoint"/> on both connection sides.
    /// </summary>
    /// <remarks>
    /// To connect to the created connection,
    /// call <see cref="ConnectInterDomainConnection"/> in the remote application domain using the returned value.
    /// </remarks>
    /// <returns>The url of the created IPC channel.</returns>
    internal string InitializeInterDomainConnection()
    {
      var endPointConfig = EndPointConfigurationData<DomainConnectionEndPoint>.InitializeDefault();
      var duplexChannel = new DomainChannel(endPointConfig);
      duplexChannel.InitializeConnection(OnDomainConnected);
      return duplexChannel.LocalEndPointUrl;
    }

    /// <summary>
    /// Connects to an existing IPC connection.
    /// </summary>
    /// <remarks>
    /// To set up such a connection, call <see cref="InitializeInterDomainConnection"/> in the remote application domain.
    /// </remarks>
    /// <param name="channelUrl"></param>
    internal void ConnectInterDomainConnection(string channelUrl)
    {
      var endPointConfig = EndPointConfigurationData<DomainConnectionEndPoint>.InitializeDefault();
      var duplexChannel = new DomainChannel(endPointConfig, channelUrl);
      duplexChannel.InitializeConnection(OnDomainConnected);
    }

    /// <summary>
    /// Creates or opens a connection to an instance of <typeparamref name="TEndPoint"/>.
    /// </summary>
    /// <typeparam name="TEndPoint">The type of the endpoint to create or open.</typeparam>
    /// <returns>The url to the endpoint of the connection.</returns>
    internal string CreateChannel<TEndPoint>()
      where TEndPoint : EndPointObject
    {
      var endPointConfig = EndPointConfigurationData<TEndPoint>.InitializeDefault();
      var channel = new SimplexChannel<TEndPoint>(endPointConfig);
      channel.InitializeChannel();
      return channel.EndPointUrl;
      // ToDo: Testing required, need to save reference to the channel?
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Eventhandler that's called when an IPC connection is ready to be used.
    /// </summary>
    /// <param name="sender"></param>
    private void OnDomainConnected(DomainChannel sender)
    {
      var remoteDomainEndPoint = sender.GetRemoteEndPoint();
      var remoteDomainId = remoteDomainEndPoint.Id;
      lock (_domainConnectionsSyncRoot)
      {
        if (!_domainConnections.ContainsKey(remoteDomainId))
          _domainConnections.Add(remoteDomainId, sender);
      }
    }

    /// <summary>
    /// Eventhandler that's called when an IPC connection is disposed.
    /// </summary>
    /// <param name="domainIdentifier"></param>
    private void OnDomainDisposed(DomainIdentifier domainIdentifier)
    {
      lock (_domainConnectionsSyncRoot)
      {
        if (_domainConnections.ContainsKey(domainIdentifier))
          _domainConnections.Remove(domainIdentifier);
      }
    }

    #endregion

  }
}
