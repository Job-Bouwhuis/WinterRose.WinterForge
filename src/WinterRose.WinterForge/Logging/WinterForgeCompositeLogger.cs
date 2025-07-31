using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinterRose.WinterForgeSerializing.Logging;

namespace WinterRose.WinterForgeSerializing.Logging;

/// <summary>
/// Holds reference to several <see cref="WinterForgeProgressTracker"/> subclasses so logging can go to more than one place
/// </summary>
/// <param name="verbosity"></param>
public class WinterForgeCompositeLogger(WinterForgeProgressVerbosity verbosity) : WinterForgeProgressTracker(verbosity)
{
    private readonly List<WinterForgeProgressTracker> loggers;

    protected internal override void Report(string message)
    {
        foreach (var logger in loggers)
            logger.Report(message);
    }

    protected internal override void Report(float graphPercentage)
    {
        foreach (var logger in loggers)
            logger.Report(graphPercentage);
    }

    public void AddLogger(WinterForgeProgressTracker logger)
    {
        loggers.Add(logger);
    }

    public void RemoveLogger(WinterForgeProgressTracker logger)
    {
        loggers.Remove(logger); 
    }
}

