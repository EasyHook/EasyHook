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
using System.Collections;
using System.Runtime.Remoting.Channels.Ipc;
using System.Security.Cryptography;
using System.Text;

namespace EasyHook.IPC
{
  /// <summary>
  /// Wraps all properties related to an IPC channel.
  /// </summary>
  internal struct ChannelProperties
  {

    #region Constants

    private const string _UrlPrefix = "ipc://";
    private const string _PostfixInstantiatorSide = "_I";
    private const string _PostfixRemoteSide = "_R";
    private const string _Seperator = "/";

    #endregion

    #region Variables

    private string _endPointName;
    private string _channelName;
    private bool _localEndPointIsInstantiator;

    #endregion

    #region Properties

    /// <summary>
    /// Gets whether or not the current <see cref="ChannelProperties"/> describes the outgoing channel of the instantiating endpoint.
    /// </summary>
    public bool IsInstantiatorChannel
    {
      get { return _localEndPointIsInstantiator; }
    }

    /// <summary>
    /// Gets or sets the name of the endpoint object.
    /// With a <see cref="DuplexChannel{T}"/> this name is expected to be the same for both endpoints.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// An <see cref="ArgumentNullException"/> is thrown if the given <paramref name="value"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// An <see cref="ArgumentException"/> is thrown if the given <paramref name="value"/> contains any of the following values:
    /// "_I", "_R" or "/"
    /// </exception>
    public string EndPointName
    {
      get { return _endPointName; }
      set
      {
        AssertParameterNotNull(value, "value");
        AssertLegalValues(value, "value");
        _endPointName = value;
      }
    }

    /// <summary>
    /// Gets the name of the channel, this is a combination of the value set with <see cref="SetChannelName"/>
    /// and a value based on <see cref="IsInstantiatorChannel"/>.
    /// </summary>
    public string ChannelName
    {
      get
      {
        if (_channelName == null)
          return null;
        return _localEndPointIsInstantiator
                 ? _channelName + _PostfixInstantiatorSide
                 : _channelName + _PostfixRemoteSide;
      }
    }

    /// <summary>
    /// Gets the full url for the described IPC channel.
    /// </summary>
    public string Url
    {
      get
      {
        return ChannelName != null && EndPointName != null
                 ? _UrlPrefix + ChannelName + _Seperator + EndPointName
                 : null;
      }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Returns the current <see cref="ChannelProperties"/> as an <see cref="IDictionary"/>,
    /// which can be used by the <see cref="IpcServerChannel"/> constructor.
    /// </summary>
    /// <returns></returns>
    public IDictionary AsDictionary()
    {
      return new Hashtable(2)
               {
                 {"name", EndPointName},
                 {"portName", ChannelName}
               };
    }

    /// <summary>
    /// Gets the <see cref="ChannelProperties"/> that are expected to be used by the remote endpoint's outgoing channel.
    /// </summary>
    /// <returns></returns>
    public ChannelProperties GetRemoteEndpointChannelProperties()
    {
      return new ChannelProperties
      {
        _endPointName = _endPointName,
        _channelName = _channelName,
        _localEndPointIsInstantiator = !_localEndPointIsInstantiator
      };
    }

    /// <summary>
    /// Sets the first part of the value returned by <see cref="ChannelName"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// An <see cref="ArgumentNullException"/> is thrown if the given <paramref name="channelName"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// An <see cref="ArgumentException"/> is thrown if the given <paramref name="channelName"/> contains any of the following values:
    /// "_I", "_R" or "/"
    /// </exception>
    /// <param name="channelName"></param>
    public void SetChannelName(string channelName)
    {
      AssertParameterNotNull(channelName, "value");
      AssertLegalValues(channelName, "value");
      _channelName = channelName;
    }

    #endregion

    #region Public Static Methods

    /// <summary>
    /// Returns a new instance of <see cref="ChannelProperties"/> describing a randomly named channel,
    /// in which the current <see cref="AppDomain"/> is expected to be the instantiator.
    /// </summary>
    /// <returns></returns>
    public static ChannelProperties CreateRandomChannelProperties()
    {
      // Creating a very random name provides an extra layer of protection to the channel
      var data = new byte[30];
      new RNGCryptoServiceProvider().GetBytes(data);
      var nameLength = 20 + (data[0]%10);
      // Convert the bytes to a string contain only numbers and letters
      var builder = new StringBuilder(nameLength);
      for (var i = 0; i < nameLength; i++)
      {
        var b = data[i]%62;
        if (b < 10)
          builder.Append((char) ('0' + b));
        else if (b < 36)
          builder.Append((char) ('A' + b - 10));
        else
          builder.Append((char) ('a' + b - 36));
      }
      var randomName = builder.ToString();
      return new ChannelProperties
               {
                 _endPointName = randomName,
                 _channelName = randomName,
                 _localEndPointIsInstantiator = true
               };
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ChannelProperties"/>
    /// describing the exact same channel as described by <paramref name="url"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// An <see cref="ArgumentNullException"/> is thrown if the given <paramref name="url"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// An <see cref="ArgumentException"/> is thrown if the given <paramref name="url"/> is not a valid IPC channel url.
    /// </exception>
    /// <param name="url"></param>
    /// <returns></returns>
    public static ChannelProperties InitializeFromUrl(string url)
    {
      // Verify and split the argument.
      AssertParameterNotNull(url, "url");
      if (url.StartsWith(_UrlPrefix))
        url = url.Substring(_UrlPrefix.Length);
      var values = url.Split(new[] {_Seperator}, StringSplitOptions.None);
      if (values.Length != 2)
        throw new ArgumentException("The provided url isn't valid.", "url");
      // Extract properties.
      var properties = new ChannelProperties();
      var channelName = values[0];
      string postFix;
      if (channelName.EndsWith(_PostfixInstantiatorSide))
        postFix = _PostfixInstantiatorSide;
      else if (channelName.EndsWith(_PostfixRemoteSide))
        postFix = _PostfixRemoteSide;
      else
        throw new ArgumentException("The provided url isn't valid.", "url");
      properties._localEndPointIsInstantiator = postFix == _PostfixInstantiatorSide;
      properties._channelName = channelName.Substring(0, channelName.Length - postFix.Length);
      properties._endPointName = values[1];
      AssertLegalValues(properties._channelName, "url");
      AssertLegalValues(properties._endPointName, "url");
      return properties;
    }

    #endregion

    #region Private Static Methods

    private static void AssertParameterNotNull(object value, string paramName)
    {
      if (value == null)
        throw new ArgumentNullException(paramName);
    }

    private static void AssertLegalValues(string value, string paramName)
    {
      if (value.EndsWith(_PostfixInstantiatorSide)
          || value.EndsWith(_PostfixRemoteSide)
          || value.Contains(_Seperator))
        throw new ArgumentException(
          string.Format("The provided value can not contain \"{0}\", \"{1}\" nor \"{2}\"",
                        _PostfixInstantiatorSide, _PostfixRemoteSide, _Seperator),
          paramName);
    }

    #endregion

  }
}
