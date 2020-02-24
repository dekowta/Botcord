using System;

namespace Botcord.Core
{
    public enum LogType
    {
        Discord,
        Script,
        Server,
        Bot //for anything else
    }

    public enum LogLevel
    {
        Info,
        Warning,
        Error,
        Debug
    }

    public delegate void LogHandler(LogLevel level, bool isException, string message);

    public class Logging
    {
        public static event LogHandler OnLog;

        private static object m_mutex = new object();

        public static void LogInfo(LogType type, string message, params object[] obj)
        {
            Log(type, LogLevel.Info, message, obj);
        }

        public static void LogWarn(LogType type, string message, params object[] obj)
        {
            Log(type, LogLevel.Warning, message, obj);
        }

        public static void LogError(LogType type, string message, params object[] obj)
        {
            Log(type, LogLevel.Error, message, obj);
        }

        public static void LogDebug(LogType type, string message, params object[] obj)
        {
            Log(type, LogLevel.Debug, message, obj);
        }

        public static void LogInfo(LogType type, string message)
        {
            Log(type, LogLevel.Info, message);
        }

        public static void LogWarn(LogType type, string message)
        {
            Log(type, LogLevel.Warning, message);
        }

        public static void LogError(LogType type, string message)
        {
            Log(type, LogLevel.Error, message);
        }

        public static void LogDebug(LogType type, string message)
        {
            Log(type, LogLevel.Debug, message);
        }

        public static void LogException(LogType type, Exception ex, string message)
        {
            LogException(type, LogLevel.Error, $"{message}\n\n`Exception: {ex.Message}`\n\n```Stack:\n{ex.StackTrace}\n```");
        }

        public static void LogException(LogType type, LogLevel level, Exception ex, string message)
        {
            LogException(type, level, $"{message}\n\n`Exception: {ex.Message}`\n\n```Stack:\n{ex.StackTrace}\n```");
        }

        public static void Log(LogType type, LogLevel level, string message)
        {
            Log(level, string.Format("[{0}]:[{1}] <{2}> : ", DateTime.Now.ToString("dd/MM/yy HH:mm:ss"), type.ToString(), level.ToString()) + message);
        }

        public static void Log(LogType type, LogLevel level, string message, params object[] obj)
        {
            Log(level, string.Format("[{0}]:[{1}] <{2}> : ", DateTime.Now.ToString("dd/MM/yy HH:mm:ss"), type.ToString(), level.ToString()) + string.Format(message, obj));
        }

        public static void LogException(LogType type, LogLevel level, string message)
        {
            LogException(level, string.Format("[{0}]:[{1}] <{2}> : ", DateTime.Now.ToString("dd/MM/yy HH:mm:ss"), type.ToString(), level.ToString()) + message);
        }

        public static void LogException(LogType type, LogLevel level, string message, params object[] obj)
        {
            LogException(level, string.Format("[{0}]:[{1}] <{2}> : ", DateTime.Now.ToString("dd/MM/yy HH:mm:ss"), type.ToString(), level.ToString()) + string.Format(message, obj));
        }

        private static void Log(LogLevel level, string message)
        {
            lock (m_mutex)
            {
                if (OnLog != null)
                    OnLog(level, false, message);

                Console.WriteLine(message);
            }
        }

        private static void LogException(LogLevel level, string message)
        {
            lock (m_mutex)
            {
                if (OnLog != null)
                    OnLog(level, true, message);

                Console.WriteLine(message);
            }
        }
    }
}
