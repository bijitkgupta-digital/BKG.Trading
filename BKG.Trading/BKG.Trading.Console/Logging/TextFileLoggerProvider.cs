using Microsoft.Extensions.Logging;

namespace BKG.Trading.Logging;

public sealed class TextFileLoggerProvider : ILoggerProvider
{
    private readonly string _logFilePath;
    private readonly object _syncLock = new();

    public TextFileLoggerProvider(string logDirectoryPath, string filePrefix)
    {
        Directory.CreateDirectory(logDirectoryPath);
        _logFilePath = Path.Combine(logDirectoryPath, $"{filePrefix}-{DateTime.Now:yyyyMMdd}.txt");
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new TextFileLogger(categoryName, _logFilePath, _syncLock);
    }

    public void Dispose()
    {
    }

    private sealed class TextFileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly string _logFilePath;
        private readonly object _syncLock;

        public TextFileLogger(string categoryName, string logFilePath, object syncLock)
        {
            _categoryName = categoryName;
            _logFilePath = logFilePath;
            _syncLock = syncLock;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            string message = formatter(state, exception);
            if (string.IsNullOrWhiteSpace(message) && exception is null)
            {
                return;
            }

            string entry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{logLevel}] {_categoryName}: {message}";
            if (exception is not null)
            {
                entry = $"{entry}{Environment.NewLine}{exception}";
            }

            lock (_syncLock)
            {
                File.AppendAllText(_logFilePath, entry + Environment.NewLine);
            }
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
