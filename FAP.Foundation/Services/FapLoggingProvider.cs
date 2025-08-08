using System;
using Microsoft.Extensions.Logging;

namespace Fap.Foundation.Services
{
    public sealed class FapLoggingProvider : ILoggerProvider
    {
        private readonly SafeObservedCollection<string> messages;

        public FapLoggingProvider(SafeObservedCollection<string> messages)
        {
            this.messages = messages;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new FapLogger(messages);
        }

        public void Dispose()
        {
        }

        private sealed class FapLogger : ILogger
        {
            private readonly SafeObservedCollection<string> messages;

            public FapLogger(SafeObservedCollection<string> messages)
            {
                this.messages = messages;
            }

            public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                if (formatter == null) return;
                var message = formatter(state, exception);
                var logEntry = $"{logLevel}=> {message}";
                if (exception != null)
                {
                    logEntry += $" {exception.Message} {exception.GetType()} {exception.StackTrace}";
                }
                messages.AddRotate(logEntry, 50);
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new NullScope();
            public void Dispose() { }
        }
    }
}


