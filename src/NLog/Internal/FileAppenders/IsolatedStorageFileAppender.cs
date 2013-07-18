using NLog.Common;
using NLog.Time;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NLog.Internal.FileAppenders
{
    internal class IsolatedStorageFileAppender : IDisposable
    {

        private readonly IsolatedStorageFile isolatedStorageFile;
        private Stream file;
         /// <summary>
        /// Initializes a new instance of the <see cref="BaseFileAppender" /> class.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="isolatedStorageFile">the file storage</param>
        public IsolatedStorageFileAppender(string fileName, IsolatedStorageFile isolatedStorageFile)
        {
            this.isolatedStorageFile = isolatedStorageFile;
            this.FileName = fileName;
            this.OpenTime = TimeSource.Current.Time.ToLocalTime();
            this.LastWriteTime = this.isolatedStorageFile.GetLastWriteTime(FileName).DateTime;
            file = CreateStream();
        }
                
        /// <summary>
        /// Gets the name of the file.
        /// </summary>
        /// <value>The name of the file.</value>
        public string FileName { get; private set; }

        /// <summary>
        /// Gets the last write time.
        /// </summary>
        /// <value>The last write time.</value>
        public DateTime LastWriteTime { get; private set; }

        /// <summary>
        /// Gets the open time of the file.
        /// </summary>
        /// <value>The open time.</value>
        public DateTime OpenTime { get; private set; }
        
        /// <summary>
        /// Writes the specified bytes.
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        public void Write(byte[] bytes)
        {
            if (this.file == null)
                return;

            this.file.Write(bytes, 0, bytes.Length);
            FileTouched();

        }

        /// <summary>
        /// Flushes this instance.
        /// </summary>
        public void Flush()
        {
            if (this.file == null)
                return;

            this.file.Flush();
            FileTouched();
        }

        /// <summary>
        /// Closes this instance.
        /// </summary>
        public void Close()
        {
            if (this.file == null)
                return;

            InternalLogger.Trace("Closing '{0}'", FileName);
            this.file.Close();
            this.file = null;
        }

        /// <summary>
        /// Gets the file info.
        /// </summary>
        /// <param name="lastWriteTime">The last write time.</param>
        /// <param name="fileLength">Length of the file.</param>
        /// <returns>True if the operation succeeded, false otherwise.</returns>
        public bool GetFileInfo(out DateTime lastWriteTime, out long fileLength)
        {
            if (this.file == null)
            {
                lastWriteTime = DateTime.MinValue;
                fileLength = -1;
                return false;
            }
            else
            {
                lastWriteTime = LastWriteTime;
                fileLength = file.Length;
                return true;
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing">True to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.Close();
            }
        }

        /// <summary>
        /// Records the last write time for a file.
        /// </summary>
        protected void FileTouched()
        {
            this.LastWriteTime = TimeSource.Current.Time.ToLocalTime();
        }

        /// <summary>
        /// Records the last write time for a file to be specific date.
        /// </summary>
        /// <param name="dateTime">Date and time when the last write occurred.</param>
        protected void FileTouched(DateTime dateTime)
        {
            this.LastWriteTime = dateTime;
        }

        /// <summary>
        /// Creates the file stream.
        /// </summary>
        /// <returns>A <see cref="FileStream"/> object which can be used to write to the file.</returns>
        protected Stream CreateStream()
        {
            string directory = Path.GetDirectoryName(FileName);
            if (!isolatedStorageFile.DirectoryExists(directory))
                isolatedStorageFile.CreateDirectory(directory);

            return isolatedStorageFile.OpenFile(FileName, FileMode.Append, FileAccess.Write, FileShare.Read);
        }
    }
}
