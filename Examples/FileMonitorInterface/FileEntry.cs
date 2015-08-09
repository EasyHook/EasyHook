using System;

namespace FileMonitorInterface
{
    /// <summary>
    /// Describes an intercepted file.
    /// </summary>
    [Serializable] // Don't forget this; all complex objects in the interface must be serializable
    public class FileEntry
    {
        /// <summary>
        /// Gets the full path to the file.
        /// </summary>
        public string FullPath { get; set; }

        public DateTime Timestamp { get; set; }
    }
}
