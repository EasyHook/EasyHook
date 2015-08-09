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
using System.Security.Principal;

namespace EasyHook.IPC
{
  /// <summary>
  /// Wraps all data required for instantiating the local endpoint's server for an IPC channel.
  /// </summary>
  /// <remarks>
  /// This class is generic in order to be able to check <see cref="RemoteObjectType"/> at compile time.
  /// </remarks>
  public struct EndPointConfigurationData<TEndPoint>
    where TEndPoint : EndPointObject
  {
    
    #region Variables

    private ICollection<WellKnownSidType> _allowedClients;
    private WellKnownObjectMode _objectMode;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the type which provides the method implementations this server should provide.
    /// </summary>
    public Type RemoteObjectType
    {
      get { return typeof(TEndPoint); }
    }

    /// <summary>
    /// Gets or sets the <see cref="WellKnownObjectMode"/> specifying how calls to the server must be handled.
    /// </summary>
    /// <remarks>
    /// Use <see cref="WellKnownObjectMode.SingleCall"/> if you want to handle each call in an new object instance,
    /// <see cref="WellKnownObjectMode.Singleton"/> otherwise.
    /// The latter will implicitly allow you to use "static" remote variables.
    /// </remarks>
    public WellKnownObjectMode ObjectMode
    {
      get { return _objectMode; }
      set { _objectMode = value; }
    }

    /// <summary>
    /// Gets  or sets the collection of all authenticated users allowed to access the remoting channel.
    /// </summary>
    public ICollection<WellKnownSidType> AllowedClients
    {
      get { return _allowedClients; }
      set
      {
        if (value == null)
          throw new ArgumentNullException("value");
        if (value.Count == 0)
          throw new ArgumentException();
        _allowedClients = value;
      }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Initializes a new instance of <see cref="EndPointConfigurationData{T}"/>
    /// using the given <see cref="Type"/> and default values.
    /// </summary>
    public static EndPointConfigurationData<TEndPoint> InitializeDefault()
    {
      return new EndPointConfigurationData<TEndPoint>
               {
                 _allowedClients =
                   new List<WellKnownSidType> {WellKnownSidType.BuiltinAdministratorsSid, WellKnownSidType.WorldSid},
                 _objectMode = WellKnownObjectMode.Singleton
               };
    }

    #endregion

  }
}
