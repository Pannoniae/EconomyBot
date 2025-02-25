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
        log(LogLevel.DEBUG, "#4169E1", msg);  // royal blue
    }

    public void debug(string msg) {
        log(LogLevel.DEBUG, "#4169E1", msg);
    }

    public void debug(Exception ex) {
        logException(LogLevel.DEBUG, "#4169E1", ex);
    }


    public void info<T>(T msg) {
        log(LogLevel.INFO, "#32CD32", msg);  // limegreen
    }

    public void info(string msg) {
        log(LogLevel.INFO, "#32CD32", msg);
    }

    public void info(Exception ex) {
        logException(LogLevel.INFO, "#32CD32", ex);
    }


    public void warn<T>(T msg) {
        log(LogLevel.WARNING, "#FFD700", msg);  // gold
    }

    public void warn(string msg) {
        log(LogLevel.WARNING, "#FFD700", msg);
    }

    public void warn(Exception ex) {
        logException(LogLevel.WARNING, "#FFD700", ex);
    }

    private void log(LogLevel logLevel, string? colour, string msg) {
        if (logLevel < Logger.logLevel) {
            return;
        }

        var time = DateTime.Now;
        AnsiConsole.MarkupLine($"[{colour ?? "default"}]{time} {GetLogLevelMarkup(logLevel)} {name}: {msg.EscapeMarkup()}[/]");
    }

    private void log<T>(LogLevel logLevel, string? colour, T msg) {
        if (logLevel < Logger.logLevel) {
            return;
        }
        
        var time = DateTime.Now;
        AnsiConsole.MarkupLine($"[{colour ?? "default"}]{time} {GetLogLevelMarkup(logLevel)} {name}: {msg?.ToString().EscapeMarkup()}[/]");
    }


    private void logException(LogLevel logLevel, string? colour, Exception msg) {
        if (logLevel < Logger.logLevel) {
            return;
        }

        var time = DateTime.Now;
        AnsiConsole.Markup($"[{colour ?? "default"}]{time} {GetLogLevelMarkup(logLevel)} {name}:[/]");
        AnsiConsole.WriteException(msg);
        AnsiConsole.WriteLine();
    }

    public void error<T>(T msg) {
        log(LogLevel.ERROR, "#FF1493", msg);  // deep pink
    }

    public void error(string msg) {
        log(LogLevel.ERROR, "#FF1493", msg);
    }

    public void error(Exception ex) {
        logException(LogLevel.ERROR, "#FF1493", ex);
    }

    private static string GetLogLevelMarkup(LogLevel level) => level switch {
        LogLevel.TRACE => $"[silver on grey][[{level}]][/]",
        LogLevel.DEBUG => $"[white on #4169E1][[{level}]][/]",
        LogLevel.INFO => $"[black on #32CD32][[{level}]][/]",
        LogLevel.WARNING => $"[black on #FFD700][[{level}]][/]",
        LogLevel.ERROR => $"[white on #FF1493][[{level}]][/]",
        LogLevel.FATAL => $"[white on #800080][[{level}]][/]",  // purple
        _ => level.ToString()
    };
}

public enum LogLevel {
    TRACE,
    DEBUG,
    INFO,
    WARNING,
    ERROR,
    FATAL
}