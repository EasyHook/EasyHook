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

using EasyHook.Domain;

namespace EasyHook.IPC
{
  /// <summary>
  /// Represents the endpoint in an inter domain connection.
  /// </summary>
  internal sealed class DomainConnectionEndPoint : DuplexChannelEndPointObject
  {

    #region Variables

    /// <summary>
    /// The identifier of the current application domain.
    /// </summary>
    private static readonly DomainIdentifier _id;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the identifier for the endpoint.
    /// </summary>
    public DomainIdentifier Id
    {
      get { return _id; }
    }

    #endregion

    #region Constructors

    static DomainConnectionEndPoint()
    {
      _id = DomainIdentifier.GetLocalDomainIdentifier();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Creates or opens a connection to an instance of <typeparamref name="TEndPoint"/>.
    /// </summary>
    /// <typeparam name="TEndPoint">The type of the endpoint to create or open.</typeparam>
    /// <returns>The url to the endpoint of the connection.</returns>
    public string CreateChannel<TEndPoint>()
      where TEndPoint : EndPointObject
    {
      return DummyCore.ConnectionManager.CreateChannel<TEndPoint>();
    }

    #endregion

    #region Public Overrides

    public override object InitializeLifetimeService()
    {
      // Returning null ensures the endpoint stays available during the complete domain's lifetime.
      return null;
    }

    #endregion

  }
}
