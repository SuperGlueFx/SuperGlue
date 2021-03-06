﻿using System;

namespace SuperGlue.Logging
{
    public class ConsoleLogger : ILog
    {
        public void Debug(string message, params object[] parameters)
        {
            Write("DEBUG", message, parameters, null);
        }

        public void Debug(Exception exception, string message, params object[] parameters)
        {
            Write("DEBUG", message, parameters, exception);
        }

        public void Info(string message, params object[] parameters)
        {
            Write("INFO", message, parameters, null);
        }

        public void Info(Exception exception, string message, params object[] parameters)
        {
            Write("INFO", message, parameters, exception);
        }

        public void Warn(string message, params object[] parameters)
        {
            Write("WARN", message, parameters, null);
        }

        public void Warn(Exception exception, string message, params object[] parameters)
        {
            Write("WARN", message, parameters, exception);
        }

        public void Error(string message, params object[] parameters)
        {
            Write("ERROR", message, parameters, null);
        }

        public void Error(Exception exception, string message, params object[] parameters)
        {
            Write("ERROR", message, parameters, exception);
        }

        public void Fatal(string message, params object[] parameters)
        {
            Write("FATAL", message, parameters, null);
        }

        public void Fatal(Exception exception, string message, params object[] parameters)
        {
            Write("FATAL", message, parameters, exception);
        }

        private static void Write(string level, string message, object[] parameters, Exception exception)
        {
            Console.WriteLine("{0}: {1}", level, string.Format(message, parameters));

            var exceptionLevel = 1;
            var currentException = exception;

            while (currentException != null)
            {
                Console.WriteLine("EXCEPTION LEVEL {0}: {1}", exceptionLevel, currentException.Message);
                Console.WriteLine(currentException.StackTrace);

                currentException = currentException.InnerException;
                exceptionLevel++;
            }
        }
    }
}