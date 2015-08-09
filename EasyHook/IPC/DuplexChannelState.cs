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

namespace EasyHook.IPC
{
  /// <summary>
  /// Defines the state of a <see cref="DuplexChannel{T}"/>.
  /// </summary>
  [Flags]
  internal enum DuplexChannelState
  {
    /// <summary>
    /// The channel is down.
    /// </summary>
    Down = 0x00,
    /// <summary>
    /// The remote endpoint is able to send messages to the local endpoint.
    /// </summary>
    ServerUp = 0x01,
    /// <summary>
    /// The local endpoint is able to send messages to the remote endpoint.
    /// </summary>
    ClientUp = 0x02,
    /// <summary>
    /// Both endpoints can communicate in either direction.
    /// </summary>
    FullDuplex = 0x03
  }
}
