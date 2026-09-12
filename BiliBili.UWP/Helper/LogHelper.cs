using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Toolkit.Uwp.Helpers;
using Newtonsoft.Json;
using NLog;
using NLog.Config;

namespace BiliBili.UWP.Helper
{
    public enum LogType
    {
        INFO,
        DEBUG,
        ERROR,
        FATAL
    }
    /// <summary>
    /// 日志记录
    /// </summary>
    public static class LogHelper
    {
        public static LoggingConfiguration config;
        public static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        public static void WriteLog(string message, LogType type, Exception ex = null)
        {
            if (config == null)
            {
                config = new NLog.Config.LoggingConfiguration();
                Windows.Storage.StorageFolder storageFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
                var logfile = new NLog.Targets.FileTarget()
                {
                    Name = "logfile",
                    CreateDirs = true,
                    FileName = storageFolder.Path + @"\log\" + DateTime.Now.ToString("yyyyMMdd") + ".log",
                    Layout = "${longdate}|${level:uppercase=true}|${logger}|${threadid}|${message}|${exception:format=ToString}"
                };
                config.AddRule(LogLevel.Info, LogLevel.Info, logfile);
                config.AddRule(LogLevel.Debug, LogLevel.Debug, logfile);
                config.AddRule(LogLevel.Error, LogLevel.Error, logfile);
                config.AddRule(LogLevel.Fatal, LogLevel.Fatal, logfile);
                NLog.LogManager.Configuration = config;
            }
            Debug.WriteLine("[" + LogType.INFO.ToString() + "]" + message);
            // 未处理异常常被 Activator/Frame.Navigate 包装成 TargetInvocationException，
            // 只记录外层消息会丢失真正原因，这里显式展开 InnerException 链兜底。
            string detail = BuildExceptionDetail(ex, message);
            switch (type)
            {
                case LogType.INFO:
                    logger.Info(message);
                    break;
                case LogType.DEBUG:
                    logger.Debug(message);
                    break;
                case LogType.ERROR:
                    logger.Error(ex, detail);
                    break;
                case LogType.FATAL:
                    logger.Fatal(ex, detail);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// 将异常及其全部内层异常拼成一行，避免只看到包装异常的消息。
        /// 无异常时原样返回 message。
        /// </summary>
        private static string BuildExceptionDetail(Exception ex, string message)
        {
            if (ex == null)
            {
                return message;
            }

            StringBuilder sb = new StringBuilder(message);
            sb.Append(" | 异常类型: ").Append(ex.GetType().FullName);
            sb.Append(" | HResult: 0x").Append(ex.HResult.ToString("X8"));
            sb.Append(" | 消息: ").Append(ex.Message);

            Exception inner = ex.InnerException;
            int depth = 1;
            while (inner != null)
            {
                sb.Append(" || 内层[").Append(depth).Append("] 类型: ").Append(inner.GetType().FullName);
                sb.Append(" | HResult: 0x").Append(inner.HResult.ToString("X8"));
                sb.Append(" | 消息: ").Append(inner.Message);
                if (!string.IsNullOrEmpty(inner.StackTrace))
                {
                    sb.Append(" | 堆栈: ").Append(inner.StackTrace);
                }
                inner = inner.InnerException;
                depth++;
            }

            return sb.ToString();
        }
        public static bool IsNetworkError(Exception ex)
        {
            if (ex.HResult == -2147012867 || ex.HResult == -2147012889)
            {
                return true;
            }
            {
                return false;
            }
        }

       
    }
}
