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
using System.Runtime.Remoting;

namespace EasyHook.IPC
{
  /// <summary>
  /// The base class of any type representing an endpoint in <see cref="EasyHook"/>'s IPC framework.
  /// </summary>
  public abstract class EndPointObject : MarshalByRefObject
  {

    #region Public Members

    // ToDo: If project is updated to .NET3.5 or above, implement extension method TryPing()
    /// <summary>
    /// If no exception is thrown, the IPC connection is expected to be working.
    /// </summary>
    /// <exception cref="RemotingException">
    /// A <see cref="RemotingException"/> is thrown by the CLR if the current object can't be reached.
    /// </exception>
    public void Ping()
    {
    }

    #endregion

    #region Internal Members

    /// <summary>
    /// Asserts that the given <paramref name="type"/> is a <see cref="Type"/> deriving from <see cref="EndPointObject"/>.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="paramName"></param>
    internal static void AssertDerivedType(Type type, string paramName)
    {
      if (type == null)
        throw new ArgumentNullException(paramName);
      if (!typeof(EndPointObject).IsAssignableFrom(type))
        throw new ArgumentException("The given type must be a type deriving from EndPointObject", paramName);
    }

    #endregion

  }
}
