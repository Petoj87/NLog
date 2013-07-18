// 
// Copyright (c) 2004-2011 Jaroslaw Kowalski <jaak@jkowalski.net>
// 
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without 
// modification, are permitted provided that the following conditions 
// are met:
// 
// * Redistributions of source code must retain the above copyright notice, 
//   this list of conditions and the following disclaimer. 
// 
// * Redistributions in binary form must reproduce the above copyright notice,
//   this list of conditions and the following disclaimer in the documentation
//   and/or other materials provided with the distribution. 
// 
// * Neither the name of Jaroslaw Kowalski nor the names of its 
//   contributors may be used to endorse or promote products derived from this
//   software without specific prior written permission. 
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE 
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE 
// ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE 
// LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR 
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN 
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) 
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF 
// THE POSSIBILITY OF SUCH DAMAGE.
// 


#if !SILVERLIGHT

namespace NLog.UnitTests.Targets
{
    using System;
    using System.IO;
    using System.Text;

    using NUnit.Framework;

#if !NUNIT
    using TestFixture = Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using Test = Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif
    using NLog.Config;
    using NLog.Layouts;
    using NLog.Targets;
    using NLog.Targets.Wrappers;

    using System.Threading;
    using System.Collections.Generic;
    using System.IO.IsolatedStorage;

    [TestFixture]
    public class IsolatedStorageFileTargetTests : NLogTestBase
    {
        private readonly Logger logger = LogManager.GetLogger("NLog.UnitTests.Targets.FileTargetTests");

        private readonly List<string> usedRandomFileNames = new List<string>();

        private string GenerateRandomFileName()
        {
            Random random = new Random();
            string filename = random.Next().ToString("X") + random.Next().ToString("X");
            while (usedRandomFileNames.Contains(filename))
            {
                filename = random.Next().ToString("X") + random.Next().ToString("X");
            }
            usedRandomFileNames.Add(filename);
            return filename;
        }

        public void DeleteDirectoryRecursive(IsolatedStorageFile isolatedStorageFile, String dirName)
        {
            String pattern = dirName + @"\*";
            String[] files = isolatedStorageFile.GetFileNames(pattern);
            foreach (String fName in files)
            {
                isolatedStorageFile.DeleteFile(Path.Combine(dirName, fName));
            }
            String[] dirs = isolatedStorageFile.GetDirectoryNames(pattern);
            foreach (String dName in dirs)
            {
                DeleteDirectoryRecursive(isolatedStorageFile, Path.Combine(dirName, dName));
            }
            isolatedStorageFile.DeleteDirectory(dirName);
        }

        public void AssertIsolatedStorageFileContents(IsolatedStorageFile isolatedStorageFile, string fileName, string contents, Encoding encoding)
        {

            if (!isolatedStorageFile.FileExists(fileName))
                Assert.Fail("File '" + fileName + "' doesn't exist.");
            using (Stream stream = isolatedStorageFile.OpenFile(fileName, FileMode.Open))
            {
                byte[] encodedBuf = encoding.GetBytes(contents);

                byte[] buf = new byte[(int)stream.Length];
                stream.Read(buf, 0, buf.Length);
                string value = encoding.GetString(buf);
                Assert.AreEqual((long)encodedBuf.Length, stream.Length, "File length is incorrect.");
                for (int i = 0; i < buf.Length; ++i)
                {
                    Assert.AreEqual(encodedBuf[i], buf[i], "File contents are different at position: #" + i);
                }
            }

        }

        [Test]
        public void SimpleFileTest1()
        {
            var tempFile = GenerateRandomFileName();
            var isft = new IsolatedStorageFileTarget
            {
                FileName = SimpleLayout.Escape(tempFile),
                LineEnding = LineEndingMode.LF,
                Layout = "${level} ${message}",
                OpenFileCacheTimeout = 0
            };
            try
            {

                SimpleConfigurator.ConfigureForTargetLogging(isft, LogLevel.Debug);

                logger.Debug("aaa");
                logger.Info("bbb");
                logger.Warn("ccc");
                LogManager.Configuration = null;
                AssertIsolatedStorageFileContents(isft.IsolatedStorageFile, tempFile, "Debug aaa\nInfo bbb\nWarn ccc\n", Encoding.UTF8);
            }
            finally
            {
                if (isft.IsolatedStorageFile.FileExists(tempFile))
                    isft.IsolatedStorageFile.DeleteFile(tempFile);
            }
        }

        [Test]
        public void CsvHeaderTest()
        {
            var tempFile = GenerateRandomFileName();
            IsolatedStorageFile isolatedStorageFile = null;
            try
            {

                for (var i = 0; i < 2; i++)
                {
                    var layout = new CsvLayout
                    {
                        Delimiter = CsvColumnDelimiterMode.Semicolon,
                        WithHeader = true,
                        Columns =
                        {
                            new CsvColumn("name", "${logger}"),
                            new CsvColumn("level", "${level}"),
                            new CsvColumn("message", "${message}"),
                        }
                    };

                    var isft = new IsolatedStorageFileTarget
                    {
                        FileName = SimpleLayout.Escape(tempFile),
                        LineEnding = LineEndingMode.LF,
                        Layout = layout,
                        OpenFileCacheTimeout = 0,
                        ReplaceFileContentsOnEachWrite = false
                    };

                    SimpleConfigurator.ConfigureForTargetLogging(isft, LogLevel.Debug);

                    logger.Debug("aaa");
                    LogManager.Configuration = null;
                    isolatedStorageFile = isft.IsolatedStorageFile;
                }
                AssertIsolatedStorageFileContents(isolatedStorageFile, tempFile, "name;level;message\nNLog.UnitTests.Targets.FileTargetTests;Debug;aaa\nNLog.UnitTests.Targets.FileTargetTests;Debug;aaa\n", Encoding.UTF8);
            }
            finally
            {
                if (isolatedStorageFile.FileExists(tempFile))
                    isolatedStorageFile.DeleteFile(tempFile);
            }
        }

        [Test]
        public void DeleteFileOnStartTest()
        {
            var tempFile = GenerateRandomFileName();

            var isft = new IsolatedStorageFileTarget
            {
                FileName = SimpleLayout.Escape(tempFile),
                LineEnding = LineEndingMode.LF,
                Layout = "${level} ${message}"
            };

            try
            {

                SimpleConfigurator.ConfigureForTargetLogging(isft, LogLevel.Debug);

                logger.Debug("aaa");
                logger.Info("bbb");
                logger.Warn("ccc");

                LogManager.Configuration = null;

                AssertIsolatedStorageFileContents(isft.IsolatedStorageFile, tempFile, "Debug aaa\nInfo bbb\nWarn ccc\n", Encoding.UTF8);

                // configure again, without
                // DeleteOldFileOnStartup

                isft = new IsolatedStorageFileTarget
                {
                    FileName = SimpleLayout.Escape(tempFile),
                    LineEnding = LineEndingMode.LF,
                    Layout = "${level} ${message}"
                };

                SimpleConfigurator.ConfigureForTargetLogging(isft, LogLevel.Debug);

                logger.Debug("aaa");
                logger.Info("bbb");
                logger.Warn("ccc");

                LogManager.Configuration = null;
                AssertIsolatedStorageFileContents(isft.IsolatedStorageFile, tempFile, "Debug aaa\nInfo bbb\nWarn ccc\nDebug aaa\nInfo bbb\nWarn ccc\n", Encoding.UTF8);

                // configure again, this time with
                // DeleteOldFileOnStartup

                isft = new IsolatedStorageFileTarget
                {
                    FileName = SimpleLayout.Escape(tempFile),
                    LineEnding = LineEndingMode.LF,
                    Layout = "${level} ${message}",
                    DeleteOldFileOnStartup = true
                };

                SimpleConfigurator.ConfigureForTargetLogging(isft, LogLevel.Debug);
                logger.Debug("aaa");
                logger.Info("bbb");
                logger.Warn("ccc");

                LogManager.Configuration = null;
                AssertIsolatedStorageFileContents(isft.IsolatedStorageFile, tempFile, "Debug aaa\nInfo bbb\nWarn ccc\n", Encoding.UTF8);
            }
            finally
            {
                LogManager.Configuration = null;
                if (isft.IsolatedStorageFile.FileExists(tempFile))
                    isft.IsolatedStorageFile.DeleteFile(tempFile);
            }
        }

        [Test]
        public void CreateDirsTest()
        {
            var tempPath = Path.Combine(GenerateRandomFileName(), Guid.NewGuid().ToString());
            var tempFile = Path.Combine(tempPath, "file.txt");
            var isft = new IsolatedStorageFileTarget
            {
                FileName = tempFile,
                LineEnding = LineEndingMode.LF,
                Layout = "${level} ${message}"
            };
            try
            {

                SimpleConfigurator.ConfigureForTargetLogging(isft, LogLevel.Debug);

                logger.Debug("aaa");
                logger.Info("bbb");
                logger.Warn("ccc");
                LogManager.Configuration = null;
                AssertIsolatedStorageFileContents(isft.IsolatedStorageFile, tempFile, "Debug aaa\nInfo bbb\nWarn ccc\n", Encoding.UTF8);
            }
            finally
            {
                LogManager.Configuration = null;
                if (isft.IsolatedStorageFile.FileExists(tempFile))
                    isft.IsolatedStorageFile.DeleteFile(tempFile);
                if (isft.IsolatedStorageFile.DirectoryExists(tempPath))
                    DeleteDirectoryRecursive(isft.IsolatedStorageFile, tempPath);
            }
        }

        [Test]
        public void SequentialArchiveTest1()
        {
            var tempPath = Path.Combine(GenerateRandomFileName(), Guid.NewGuid().ToString());
            var tempFile = Path.Combine(tempPath, "file.txt");
            var isft = new IsolatedStorageFileTarget
            {
                FileName = tempFile,
                ArchiveFileName = Path.Combine(tempPath, "archive", "{####}.txt"),
                ArchiveAboveSize = 1000,
                LineEnding = LineEndingMode.LF,
                Layout = "${message}",
                MaxArchiveFiles = 3,
                ArchiveNumbering = ArchiveNumberingMode.Sequence
            };
            try
            {

                SimpleConfigurator.ConfigureForTargetLogging(isft, LogLevel.Debug);

                // we emit 5 * 250 *(3 x aaa + \n) bytes
                // so that we should get a full file + 3 archives
                for (var i = 0; i < 250; ++i)
                {
                    logger.Debug("aaa");
                }
                for (var i = 0; i < 250; ++i)
                {
                    logger.Debug("bbb");
                }
                for (var i = 0; i < 250; ++i)
                {
                    logger.Debug("ccc");
                }
                for (var i = 0; i < 250; ++i)
                {
                    logger.Debug("ddd");
                }
                for (var i = 0; i < 250; ++i)
                {
                    logger.Debug("eee");
                }

                LogManager.Configuration = null;

                AssertIsolatedStorageFileContents(isft.IsolatedStorageFile, tempFile,
                    StringRepeat(250, "eee\n"),
                    Encoding.UTF8);

                AssertIsolatedStorageFileContents(isft.IsolatedStorageFile, 
                    Path.Combine(tempPath, "archive","0001.txt"),
                    StringRepeat(250, "bbb\n"),
                    Encoding.UTF8);

                AssertIsolatedStorageFileContents(isft.IsolatedStorageFile,
                    Path.Combine(tempPath, "archive", "0002.txt"),
                    StringRepeat(250, "ccc\n"),
                    Encoding.UTF8);

                AssertIsolatedStorageFileContents(isft.IsolatedStorageFile,
                    Path.Combine(tempPath, "archive", "0003.txt"),
                    StringRepeat(250, "ddd\n"),
                    Encoding.UTF8);

                Assert.IsTrue(!isft.IsolatedStorageFile.FileExists(Path.Combine(tempPath, "archive", "0000.txt")));
                Assert.IsTrue(!isft.IsolatedStorageFile.FileExists(Path.Combine(tempPath, "archive", "0004.txt")));
            }
            finally
            {
                LogManager.Configuration = null;
                if (isft.IsolatedStorageFile.FileExists(tempFile))
                    isft.IsolatedStorageFile.DeleteFile(tempFile);
                if (isft.IsolatedStorageFile.DirectoryExists(tempPath))
                    DeleteDirectoryRecursive(isft.IsolatedStorageFile, tempPath);
            }
        }

        [Test]
        public void RollingArchiveTest1()
        {
            var tempPath = Path.Combine(GenerateRandomFileName(), Guid.NewGuid().ToString());
            var tempFile = Path.Combine(tempPath, "file.txt");
            var isft = new IsolatedStorageFileTarget
            {
                FileName = tempFile,
                ArchiveFileName = Path.Combine(tempPath, "archive", "{####}.txt"),
                ArchiveAboveSize = 1000,
                LineEnding = LineEndingMode.LF,
                ArchiveNumbering = ArchiveNumberingMode.Rolling,
                Layout = "${message}",
                MaxArchiveFiles = 3
            };
            try
            {

                SimpleConfigurator.ConfigureForTargetLogging(isft, LogLevel.Debug);

                // we emit 5 * 250 * (3 x aaa + \n) bytes
                // so that we should get a full file + 3 archives
                for (var i = 0; i < 250; ++i)
                {
                    logger.Debug("aaa");
                }
                for (var i = 0; i < 250; ++i)
                {
                    logger.Debug("bbb");
                }
                for (var i = 0; i < 250; ++i)
                {
                    logger.Debug("ccc");
                }
                for (var i = 0; i < 250; ++i)
                {
                    logger.Debug("ddd");
                }
                for (var i = 0; i < 250; ++i)
                {
                    logger.Debug("eee");
                }

                LogManager.Configuration = null;

                AssertIsolatedStorageFileContents(isft.IsolatedStorageFile, tempFile,
                    StringRepeat(250, "eee\n"),
                    Encoding.UTF8);

                AssertIsolatedStorageFileContents(isft.IsolatedStorageFile, 
                    Path.Combine(tempPath, "archive", "0000.txt"),
                    StringRepeat(250, "ddd\n"),
                    Encoding.UTF8);

                AssertIsolatedStorageFileContents(isft.IsolatedStorageFile, 
                    Path.Combine(tempPath, "archive", "0001.txt"),
                    StringRepeat(250, "ccc\n"),
                    Encoding.UTF8);

                AssertIsolatedStorageFileContents(isft.IsolatedStorageFile, 
                    Path.Combine(tempPath, "archive", "0002.txt"),
                    StringRepeat(250, "bbb\n"),
                    Encoding.UTF8);

                Assert.IsTrue(!isft.IsolatedStorageFile.FileExists(Path.Combine(tempPath, "archive", "0003.txt")));
            }
            finally
            {
                LogManager.Configuration = null;
                if (isft.IsolatedStorageFile.FileExists(tempFile))
                    isft.IsolatedStorageFile.DeleteFile(tempFile);
                if (isft.IsolatedStorageFile.DirectoryExists(tempPath))
                    DeleteDirectoryRecursive(isft.IsolatedStorageFile, tempPath);
            }
        }

        [Test]
        public void MultiFileWrite()
        {
            var tempPath = Path.Combine(GenerateRandomFileName(), Guid.NewGuid().ToString());
            var isft = new IsolatedStorageFileTarget
            {
                FileName = Path.Combine(tempPath, "${level}.txt"),
                LineEnding = LineEndingMode.LF,
                Layout = "${message}"
            };
            try
            {

                SimpleConfigurator.ConfigureForTargetLogging(isft, LogLevel.Debug);

                for (var i = 0; i < 250; ++i)
                {
                    logger.Trace("@@@");
                    logger.Debug("aaa");
                    logger.Info("bbb");
                    logger.Warn("ccc");
                    logger.Error("ddd");
                    logger.Fatal("eee");
                }

                LogManager.Configuration = null;

                Assert.IsFalse(isft.IsolatedStorageFile.FileExists(Path.Combine(tempPath, "Trace.txt")));

                AssertIsolatedStorageFileContents(isft.IsolatedStorageFile, Path.Combine(tempPath, "Debug.txt"),
                    StringRepeat(250, "aaa\n"), Encoding.UTF8);

                AssertIsolatedStorageFileContents(isft.IsolatedStorageFile, Path.Combine(tempPath, "Info.txt"),
                    StringRepeat(250, "bbb\n"), Encoding.UTF8);

                AssertIsolatedStorageFileContents(isft.IsolatedStorageFile, Path.Combine(tempPath, "Warn.txt"),
                    StringRepeat(250, "ccc\n"), Encoding.UTF8);

                AssertIsolatedStorageFileContents(isft.IsolatedStorageFile, Path.Combine(tempPath, "Error.txt"),
                    StringRepeat(250, "ddd\n"), Encoding.UTF8);

                AssertIsolatedStorageFileContents(isft.IsolatedStorageFile, Path.Combine(tempPath, "Fatal.txt"),
                    StringRepeat(250, "eee\n"), Encoding.UTF8);
            }
            finally
            {
                //if (isft.IsolatedStorageFile.FileExists(tempFile))
                //    isolatedStorageFile.DeleteFile(tempFile);
                LogManager.Configuration = null;
                if (isft.IsolatedStorageFile.DirectoryExists(tempPath))
                    DeleteDirectoryRecursive(isft.IsolatedStorageFile, tempPath);
            }
        }

        [Test]
        public void BufferedMultiFileWrite()
        {
            var tempPath = Path.Combine(GenerateRandomFileName(), Guid.NewGuid().ToString());
            var isft = new IsolatedStorageFileTarget
            {
                FileName = Path.Combine(tempPath, "${level}.txt"),
                LineEnding = LineEndingMode.LF,
                Layout = "${message}"
            };
            try
            {

                SimpleConfigurator.ConfigureForTargetLogging(new BufferingTargetWrapper(isft, 10), LogLevel.Debug);

                for (var i = 0; i < 250; ++i)
                {
                    logger.Trace("@@@");
                    logger.Debug("aaa");
                    logger.Info("bbb");
                    logger.Warn("ccc");
                    logger.Error("ddd");
                    logger.Fatal("eee");
                }

                LogManager.Configuration = null;

                Assert.IsFalse(isft.IsolatedStorageFile.FileExists(Path.Combine(tempPath, "Trace.txt")));

                AssertIsolatedStorageFileContents(isft.IsolatedStorageFile, Path.Combine(tempPath, "Debug.txt"),
                    StringRepeat(250, "aaa\n"), Encoding.UTF8);

                AssertIsolatedStorageFileContents(isft.IsolatedStorageFile, Path.Combine(tempPath, "Info.txt"),
                    StringRepeat(250, "bbb\n"), Encoding.UTF8);

                AssertIsolatedStorageFileContents(isft.IsolatedStorageFile, Path.Combine(tempPath, "Warn.txt"),
                    StringRepeat(250, "ccc\n"), Encoding.UTF8);

                AssertIsolatedStorageFileContents(isft.IsolatedStorageFile, Path.Combine(tempPath, "Error.txt"),
                    StringRepeat(250, "ddd\n"), Encoding.UTF8);

                AssertIsolatedStorageFileContents(isft.IsolatedStorageFile, Path.Combine(tempPath, "Fatal.txt"),
                    StringRepeat(250, "eee\n"), Encoding.UTF8);
            }
            finally
            {
                //if (isft.IsolatedStorageFile.FileExists(tempFile))
                //    isolatedStorageFile.DeleteFile(tempFile);
                LogManager.Configuration = null;
                if (isft.IsolatedStorageFile.DirectoryExists(tempPath))
                    DeleteDirectoryRecursive(isft.IsolatedStorageFile, tempPath);
            }
        }

        [Test]
        public void AsyncMultiFileWrite()
        {
            var tempPath = Path.Combine(GenerateRandomFileName(), Guid.NewGuid().ToString());
            var isft = new IsolatedStorageFileTarget
            {
                FileName = Path.Combine(tempPath, "${level}.txt"),
                LineEnding = LineEndingMode.LF,
                Layout = "${message} ${threadid}"
            };
            try
            {

                // this also checks that thread-volatile layouts
                // such as ${threadid} are properly cached and not recalculated
                // in logging threads.

                var threadID = Thread.CurrentThread.ManagedThreadId.ToString();

                SimpleConfigurator.ConfigureForTargetLogging(new AsyncTargetWrapper(isft, 1000, AsyncTargetWrapperOverflowAction.Grow), LogLevel.Debug);
                LogManager.ThrowExceptions = true;

                for (var i = 0; i < 250; ++i)
                {
                    logger.Trace("@@@");
                    logger.Debug("aaa");
                    logger.Info("bbb");
                    logger.Warn("ccc");
                    logger.Error("ddd");
                    logger.Fatal("eee");
                }
                LogManager.Flush();
                LogManager.Configuration = null;

                Assert.IsFalse(isft.IsolatedStorageFile.FileExists(Path.Combine(tempPath, "Trace.txt")));

                AssertIsolatedStorageFileContents(isft.IsolatedStorageFile, Path.Combine(tempPath, "Debug.txt"),
                    StringRepeat(250, "aaa " + threadID + "\n"), Encoding.UTF8);

                AssertIsolatedStorageFileContents(isft.IsolatedStorageFile, Path.Combine(tempPath, "Info.txt"),
                    StringRepeat(250, "bbb " + threadID + "\n"), Encoding.UTF8);

                AssertIsolatedStorageFileContents(isft.IsolatedStorageFile, Path.Combine(tempPath, "Warn.txt"),
                    StringRepeat(250, "ccc " + threadID + "\n"), Encoding.UTF8);

                AssertIsolatedStorageFileContents(isft.IsolatedStorageFile, Path.Combine(tempPath, "Error.txt"),
                    StringRepeat(250, "ddd " + threadID + "\n"), Encoding.UTF8);

                AssertIsolatedStorageFileContents(isft.IsolatedStorageFile, Path.Combine(tempPath, "Fatal.txt"),
                    StringRepeat(250, "eee " + threadID + "\n"), Encoding.UTF8);
            }
            finally
            {
                //if (isft.IsolatedStorageFile.FileExists(tempFile))
                //    isolatedStorageFile.DeleteFile(tempFile);
                LogManager.Configuration = null;
                if (isft.IsolatedStorageFile.DirectoryExists(tempPath))
                    DeleteDirectoryRecursive(isft.IsolatedStorageFile, tempPath);

                // Clean up configuration change, breaks onetimeonlyexceptioninhandlertest
                LogManager.ThrowExceptions = true;
            }
        }

        [Test]
        public void BatchErrorHandlingTest()
        {
            var fileTarget = new IsolatedStorageFileTarget 
            { 
                FileName = "${logger}", 
                Layout = "${message}" 
            };
            fileTarget.Initialize(null);

            // make sure that when file names get sorted, the asynchronous continuations are sorted with them as well
            var exceptions = new List<Exception>();
            var events = new[]
            {
                new LogEventInfo(LogLevel.Info, "file99.txt", "msg1").WithContinuation(exceptions.Add),
                new LogEventInfo(LogLevel.Info, "a/", "msg1").WithContinuation(exceptions.Add),
                new LogEventInfo(LogLevel.Info, "a/", "msg2").WithContinuation(exceptions.Add),
                new LogEventInfo(LogLevel.Info, "a/", "msg3").WithContinuation(exceptions.Add)
            };

            fileTarget.WriteAsyncLogEvents(events);

            Assert.AreEqual(4, exceptions.Count);
            Assert.IsNull(exceptions[0]);
            Assert.IsNotNull(exceptions[1]);
            Assert.IsNotNull(exceptions[2]);
            Assert.IsNotNull(exceptions[3]);
        }

        [Test]
        public void DisposingFileTarget_WhenNotIntialized_ShouldNotThrow()
        {
            var exceptionThrown = false;
            var fileTarget = new IsolatedStorageFileTarget();

            try
            {
                fileTarget.Dispose();
            }
            catch
            {
                exceptionThrown = true;
            }

            Assert.IsFalse(exceptionThrown);
        }

        [Test]
        public void FileTarget_WithArchiveFileNameEndingInNumberPlaceholder_ShouldArchiveFile()
        {
            var tempPath = Path.Combine(GenerateRandomFileName(), Guid.NewGuid().ToString());
            var tempFile = Path.Combine(tempPath, "file.txt");
            var isft = new IsolatedStorageFileTarget
            {
                FileName = tempFile,
                ArchiveFileName = Path.Combine(tempPath, "archive/test.log.{####}"),
                ArchiveAboveSize = 1000
            };
            try
            {

                SimpleConfigurator.ConfigureForTargetLogging(isft, LogLevel.Debug);

                for (var i = 0; i < 100; ++i)
                {
                    logger.Debug("a");
                }

                LogManager.Configuration = null;
                Assert.IsTrue(isft.IsolatedStorageFile.FileExists(tempFile));
                Assert.IsTrue(isft.IsolatedStorageFile.FileExists(Path.Combine(tempPath, "archive/test.log.0000")));
            }
            finally
            {
                LogManager.Configuration = null;
                if (isft.IsolatedStorageFile.FileExists(tempFile))
                    isft.IsolatedStorageFile.DeleteFile(tempFile);
                if (isft.IsolatedStorageFile.DirectoryExists(tempPath))
                    DeleteDirectoryRecursive(isft.IsolatedStorageFile, tempPath);
            }
        }
    }
}

#endif