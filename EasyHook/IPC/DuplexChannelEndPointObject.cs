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
using System.Reflection;

namespace EasyHook.IPC
{
  /// <summary>
  /// The base class of any type representing an endpoint in <see cref="DuplexChannel{TClientObject}"/>.
  /// </summary>
  internal abstract class DuplexChannelEndPointObject : EndPointObject
  {

    #region Private Constants - Member Names for Reflection

    /// <summary>
    /// <see cref="BindingFlags"/> to use when finding a class member through reflection.
    /// </summary>
    private const BindingFlags _BindingFlags = BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy;
    /// <summary>
    /// The name of <see cref="IsRemoteEndPointReady"/>,
    /// to be used with reflection.
    /// </summary>
    private const string _IsEndPointReady = "IsRemoteEndPointReady";
    /// <summary>
    /// The name of <see cref="Subscribe"/>,
    /// to be used with reflection.
    /// </summary>
    private const string _Subscribe = "Subscribe";

    #endregion

    #region Private Variables

    private static EventHandler _onEndPointReady;
    private static object _onEndPointReadyState;
    private static bool _isEndPointReady;

    #endregion

    #region Internal Members

    /// <summary>
    /// Signals the remote endpoint that the local endpoint is ready to be connected to.
    /// </summary>
    /// <remarks>
    /// This is an internal method!
    /// It is marked public to prevent .NET remoting raising the following RemotingException:
    /// "Permission denied: cannot call non-public or static methods remotely."
    /// </remarks>
    [Obsolete("For internal use only!")]
    public void SignalEndpointReady()
    {
      _isEndPointReady = true;
      if (_onEndPointReady != null)
        _onEndPointReady(_onEndPointReadyState, null);
    }

    /// <summary>
    /// Reads, using reflection on <paramref name="derivingType"/>, whether the remote endpoint is ready to be connected to.
    /// </summary>
    /// <param name="derivingType"></param>
    /// <returns></returns>
    internal static bool ReadIsRemoteEndPointReadyProperty(Type derivingType)
    {
      AssertDerivedType(derivingType, "derivingType");
      var isEndpointReady = derivingType.GetProperty(_IsEndPointReady, _BindingFlags);
      if (isEndpointReady == null)
        return false;
      return (bool)isEndpointReady.GetValue(null, null);
    }

    /// <summary>
    /// Subscribes, using reflection on <paramref name="derivingType"/>, to an event raised when the remote endpoint signals its ready status.
    /// </summary>
    /// <param name="derivingType"></param>
    /// <param name="onActivated"></param>
    /// <param name="state"></param>
    internal static void SubscribeEndPointReadyEvent(Type derivingType, EventHandler onActivated, object state)
    {
      AssertDerivedType(derivingType, "derivingType");
      var subscribeMethod = derivingType.GetMethod(_Subscribe, _BindingFlags);
      subscribeMethod.Invoke(null, new[] { onActivated, state });
    }

    /// <summary>
    /// Asserts that the given <paramref name="type"/> is a <see cref="Type"/> deriving from <see cref="DuplexChannelEndPointObject"/>.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="paramName"></param>
    internal new static void AssertDerivedType(Type type, string paramName)
    {
      if (type == null)
        throw new ArgumentNullException(paramName);
      if (!typeof(DuplexChannelEndPointObject).IsAssignableFrom(type))
        throw new ArgumentException("The given type must be a type deriving from EndPointObject", paramName);
    }

    #endregion

    #region Internal Protected Members

    /// <summary>
    /// Is marked protected since it's not possible to use reflection on private static members.
    /// See <seealso cref="ReadIsRemoteEndPointReadyProperty"/>.
    /// </summary>
    internal protected static bool IsRemoteEndPointReady
    {
      get { return _isEndPointReady; }
    }

    /// <summary>
    /// Is marked protected since it's not possible to use reflection on private static members.
    /// See <seealso cref="SubscribeEndPointReadyEvent"/>.
    /// </summary>
    /// <param name="onActivated"></param>
    /// <param name="state"></param>
    internal protected static void Subscribe(EventHandler onActivated, object state)
    {
      _onEndPointReady += onActivated;
      _onEndPointReadyState = state;
    }

    #endregion

  }
}
