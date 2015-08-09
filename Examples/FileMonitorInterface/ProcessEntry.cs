using System;

namespace FileMonitorInterface
{
    [Serializable]
    public class ProcessEntry
    {
        /// <summary>
        /// Gets the full path to the image.
        /// </summary>
        public string FullPath { get; set; }

        /// <summary>
        /// Gets the short name to the image.
        /// </summary>
        public string ImageName { get; set; }

        /// <summary>
        /// Gets the process ID.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets whether this process is 64-bit.
        /// </summary>
        public bool IsX64;

        /// <summary>
        /// Gets the owner of this process.
        /// </summary>
        public string Owner { get; set; }
    }
}
