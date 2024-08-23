using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Asv.Sdr.LimeSdr;

public static class LmsLogManager
{
    private static ILogger _globalLogger = NullLogger.Instance;
    private static ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;

    public static void SetLoggerFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _globalLogger = loggerFactory.CreateLogger("Asv.Sdr.LimeSdr");
    }

    public static ILogger Logger => _globalLogger;

    // standard LoggerFactory caches logger per category so no need to cache in this manager
    public static ILogger<T> GetLogger<T>() where T : class => _loggerFactory.CreateLogger<T>();
    public static ILogger GetLogger(string categoryName) => _loggerFactory.CreateLogger(categoryName);
}