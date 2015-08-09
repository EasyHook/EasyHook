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
using System.Diagnostics;

namespace EasyHook.Domain
{
  /// <summary>
  /// Provides a system wide unique identifier for an application domain.
  /// </summary>
  [Serializable]
  public struct DomainIdentifier : IEquatable<DomainIdentifier>
  {

    #region Properties

    /// <summary>
    /// Gets or sets the ID of the application domain.
    /// </summary>
    public int DomainId { get; set; }
    /// <summary>
    /// Gets or sets the ID of the process.
    /// </summary>
    public int ProcessId { get; set; }

    #endregion

    #region Public Methods

    /// <summary>
    /// Returns the identifier as a string.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
      return ProcessId + "." + DomainId;
    }

    #endregion

    #region Public Static Methods

    /// <summary>
    /// Gets a new <see cref="DomainIdentifier"/> which identifies the current application domain.
    /// </summary>
    /// <returns></returns>
    public static DomainIdentifier GetLocalDomainIdentifier()
    {
      return new DomainIdentifier
               {
                 DomainId = AppDomain.CurrentDomain.Id,
                 ProcessId = Process.GetCurrentProcess().Id
               };
    }

    #endregion
    
    #region IEquatable<DomainIdentifier> Members

    /// <summary>
    /// Indicates whether the current <see cref="DomainIdentifier"/> is equal to the <paramref name="other"/> <see cref="DomainIdentifier"/>.
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public bool Equals(DomainIdentifier other)
    {
      return ProcessId == other.ProcessId && DomainId == other.DomainId;
    }

    #endregion

  }
}
