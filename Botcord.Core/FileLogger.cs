using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace Botcord.Core
{

    public class FileLogger : Singleton<FileLogger>
    {
        private struct LogQueue
        {
            public LogType Type;
            public string Message;
        }
        
        private ConcurrentQueue<LogQueue> logQueue = new ConcurrentQueue<LogQueue>();
        private string launchFile = string.Empty;
        private object mutex = new object();

        private bool exit = false;
        private ManualResetEvent queueSignal = new ManualResetEvent(false);
        private Thread queueThread;
        public FileLogger()
        {
            Reset();
            Logging.OnLog += Logging_OnLog;
        }

        public void Reset()
        {
            if (queueThread != null)
            {
                exit = true;
                queueSignal.Set();

                while (queueThread.IsAlive)
                { }
            }
            queueSignal.Reset();
            exit = false;
            launchFile = DateTime.UtcNow.ToString("HH_mm_ss");
            queueThread = new Thread(QueueFlush);
            queueThread.IsBackground = true;
            queueThread.Start();
        }

        public void CopyLogs(string destination)
        {
            lock(mutex)
            {
                string dst = Utilities.EnsurePath(destination);
                foreach(var type in (LogType[])Enum.GetValues(typeof(LogType)))
                {
                    string dstFile = Path.Combine(dst, $"{type}.txt");
                    string srcFile = GetTypeLog(type);
                    if (File.Exists(srcFile))
                    {
                        Utilities.TryCatch(() => File.Copy(srcFile, dstFile, true), $"Failed to copy log type '{type}' to '{destination}'");
                    }
                }

                string completeDstFile = Path.Combine(dst, "log.txt");
                string completeSrcFile = GetFullLog();
                if (File.Exists(completeSrcFile))
                {
                    Utilities.TryCatch(() => File.Copy(completeSrcFile, completeDstFile, true), $"Filed to copy full log to '{destination}'");
                }
            }
        }

        private void Logging_OnLog(LogType type, LogLevel level, bool isException, string message)
        {
            if(Monitor.TryEnter(mutex))
            {
                LogToFile(type, message);

                Monitor.Exit(mutex);
            }
            else
            {
                queueSignal.Set();
                logQueue.Enqueue(new LogQueue() { Type = type, Message = message });
            }
        }

        private void LogToFile(LogType type, string message)
        {
            try
            {
                string fullLog = GetFullLog();
                using (StreamWriter writer = File.Exists(fullLog) ? File.AppendText(fullLog) : File.CreateText(fullLog))
                {
                    writer.WriteLine(message);
                }

                string typeLog = GetTypeLog(type);
                using (StreamWriter writer = File.Exists(typeLog) ? File.AppendText(typeLog) : File.CreateText(typeLog))
                {
                    writer.WriteLine(message);
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"An Exception occured file trying to log message '{ex.Message}' of type '{type}' to file");
            }
        }

        private void QueueFlush(object param)
        {
            while (!exit)
            {
                queueSignal.WaitOne();
                LogQueue queueItem;
                lock(mutex)
                {
                    while (logQueue.TryDequeue(out queueItem))
                    {
                        LogToFile(queueItem.Type, queueItem.Message);
                    }
                }
                queueSignal.Reset();
            }
        }

        private string GetFullLog()
        {
            string folderName = DateTime.UtcNow.ToString("dd-MM-yyyy");
            string folder = Utilities.EnsurePath(Path.Combine(Utilities.LogFolder, folderName));
            return Path.Combine(folder, $"full_{launchFile}.txt");
        }

        private string GetTypeLog(LogType type)
        {
            string folderName = DateTime.UtcNow.ToString("dd-MM-yyyy");
            string folder = Utilities.EnsurePath(Path.Combine(Utilities.LogFolder, folderName));
            return Path.Combine(folder, $"{type}_{launchFile}.txt");
        }
    }
}
