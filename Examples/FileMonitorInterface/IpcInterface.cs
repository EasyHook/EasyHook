using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace FileMonitorInterface
{
    /// <summary>
    /// Allows communication between the IPC server and client.
    /// </summary>
    /// <remarks>
    /// Both the client (FileMonitorInterceptor) and the server (FileMonitorController) access the methods and properties of this same interface.
    /// </remarks>
    public class IpcInterface : MarshalByRefObject
    {
        /// <summary>
        /// A map of intercepted file entries to each specific process.
        /// </summary>
        /// <remarks>
        /// This same dictionary is filled by the client (FileMonitorInterceptor) and queried by the server (FileMonitorController).
        /// </remarks>
        public readonly Dictionary <int, List <FileEntry>> FileEntries;

        /// <summary>
        /// Added to when FileMonitorController requests a process to terminate its hook.
        /// </summary>
        public readonly List<int> TerminatingProcesses;

        public IpcInterface()
        {
            FileEntries = new Dictionary <int, List <FileEntry>>();
            TerminatingProcesses = new List <int>();
        }

        public void AddFileEntry (int processId, FileEntry entry)
        {
            if (!FileEntries.ContainsKey (processId))
            {
                FileEntries.Add (processId, new List <FileEntry>());
            }

            FileEntries [processId].Add (entry);
        }

        public override object InitializeLifetimeService()
        {
            return null;
        }

        /// <summary>
        /// Posts a message from the client (FileMonitorInterceptor) to the server (FileMonitorController).
        /// </summary>
        /// <remarks>
        /// The message isn't really "sent". Rather, an event handler is fired, and the server has hopefully subscribed to this event handler.
        /// </remarks>
        /// <param name="message">The string message to post.</param>
        public void PostMessage (string message)
        {
            if (OnMessagePosted != null)
            {
                OnMessagePosted (this, new MessageEventArgs (message));
            }
        }

        public void PostException (Exception ex)
        {
            PostMessage (ex.ToString());
        }

        /// <summary>
        /// Does nothing, but will throw an exception from either the client or server if the other is unreachable.
        /// </summary>
        public void Ping()
        {
        }

        /// <summary>
        /// Occurs when the client posts an informational message.
        /// </summary>
        public event EventHandler <MessageEventArgs> OnMessagePosted;
    }
}
