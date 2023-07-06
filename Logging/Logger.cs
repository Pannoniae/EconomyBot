using System.Net;
using Spectre.Console;

namespace EconomyBot.Logging;

public class Logger {
    // the least log level which is logged
    private static LogLevel logLevel = LogLevel.INFO;

    private readonly string name;

    private Logger(string name) {
        this.name = name;
    }

    public static Logger getClassLogger(string name) {
        return new Logger(name);
    }

    public static void setLogLevel(LogLevel level) {
        logLevel = level;
    }

    // Note: using generics for primitive types is fucking inefficient. For commonly used types, add an explicit overload.
    // Of course, this doesn't matter much for this homemade logging solution:tm:, but if anyone else sees it or use it, this is a high-priority issue.

    public void debug<T>(T msg) {
        log(LogLevel.DEBUG, null, msg);
    }

    public void debug(string msg) {
        log(LogLevel.DEBUG, null, msg);
    }

    public void debug(Exception ex) {
        logException(LogLevel.DEBUG, null, ex);
    }


    public void info<T>(T msg) {
        log(LogLevel.INFO, null, msg);
    }

    public void info(string msg) {
        log(LogLevel.INFO, null, msg);
    }

    public void info(Exception ex) {
        logException(LogLevel.INFO, null, ex);
    }


    public void warn<T>(T msg) {
        log(LogLevel.WARNING, "#FF2288", msg);
    }

    public void warn(string msg) {
        log(LogLevel.WARNING, "#FF2288", msg);
    }

    public void warn(Exception ex) {
        logException(LogLevel.WARNING, "#FF2288", ex);
    }

    private void log(LogLevel logLevel, string? colour, string msg) {
        if (logLevel < Logger.logLevel) {
            return;
        }

        var time = DateTime.Now;
        AnsiConsole.MarkupLine($"[{colour ?? "default"}]{time} [[{logLevel}]] {name}: {msg.EscapeMarkup()}[/]");
    }

    private void log<T>(LogLevel logLevel, string? colour, T msg) {
        if (logLevel < Logger.logLevel) {
            return;
        }

        var time = DateTime.Now;
        AnsiConsole.MarkupLine($"[{colour ?? "default"}]{time} [[{logLevel}]] {name}: {msg?.ToString().EscapeMarkup()}[/]");
    }


    private void logException(LogLevel logLevel, string? colour, Exception msg) {
        if (logLevel < Logger.logLevel) {
            return;
        }

        var time = DateTime.Now;
        AnsiConsole.Markup($"[{colour ?? "default"}]{time} [[{logLevel}]] {name}:[/]");
        AnsiConsole.WriteException(msg);
        AnsiConsole.WriteLine();
    }
}

public enum LogLevel {
    TRACE,
    DEBUG,
    INFO,
    WARNING,
    ERROR,
    FATAL
}