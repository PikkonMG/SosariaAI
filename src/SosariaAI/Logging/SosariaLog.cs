using System;
using Server.Logging;
using SosariaAI.Logging;

namespace SosariaAI.Logging;

/// <summary>
/// Loggers for the plugin. <see cref="For"/> keeps a class's information lines in the
/// activity file. <see cref="Console"/> also shows them on the console, for the few
/// boot and staff lines an operator must see there.
/// </summary>
public static class SosariaLog
{
    public static ILogger For(Type type) => new FileLogger(type, console: false);

    public static ILogger Console(Type type) => new FileLogger(type, console: true);

    private sealed class FileLogger : ILogger
    {
        private const string DebugLevel = "DBG";
        private const string InformationLevel = "INF";
        private const string WarningLevel = "WRN";
        private const string ErrorLevel = "ERR";
        private const string FatalLevel = "FTL";

        private readonly ILogger _console;
        private readonly string _source;
        private readonly bool _informationOnConsole;

        public FileLogger(Type type, bool console)
        {
            _console = LogFactory.GetLogger(type);
            _source = type.FullName;
            _informationOnConsole = console;
        }

        public void Debug(string message, params object[] args) => ToFile(DebugLevel, message, args);

        public void Debug(Exception exception, string message, params object[] args) =>
            ToFile(DebugLevel, message, args, exception);

        public void Information(string message, params object[] args)
        {
            ToFile(InformationLevel, message, args);

            if (_informationOnConsole)
            {
                _console.Information(message, args);
            }
        }

        public void Information(Exception exception, string message, params object[] args)
        {
            ToFile(InformationLevel, message, args, exception);

            if (_informationOnConsole)
            {
                _console.Information(exception, message, args);
            }
        }

        public void Warning(string message, params object[] args)
        {
            ToFile(WarningLevel, message, args);
            _console.Warning(message, args);
        }

        public void Warning(Exception exception, string message, params object[] args)
        {
            ToFile(WarningLevel, message, args, exception);
            _console.Warning(exception, message, args);
        }

        public void Error(string message, params object[] args)
        {
            ToFile(ErrorLevel, message, args);
            _console.Error(message, args);
        }

        public void Error(Exception exception, string message, params object[] args)
        {
            ToFile(ErrorLevel, message, args, exception);
            _console.Error(exception, message, args);
        }

        public void Fatal(string message, params object[] args)
        {
            ToFile(FatalLevel, message, args);
            _console.Fatal(message, args);
        }

        public void Fatal(Exception exception, string message, params object[] args)
        {
            ToFile(FatalLevel, message, args, exception);
            _console.Fatal(exception, message, args);
        }

        private void ToFile(string level, string message, object[] args, Exception exception = null)
        {
            var text = MessageTemplate.Render(message, args);

            if (exception != null)
            {
                text = $"{text} {exception}";
            }

            ActivityFile.Write(level, _source, text);
        }
    }
}
