 using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinterRose.WinterForgeSerializing.Logging
{
    /// <summary>
    /// A default implementation of <see cref="WinterForgeProgressTracker"/> that writes to the console
    /// </summary>
    /// <param name="verbosity">The level of verbosity to use when reporting progress</param>
    /// <param name="includeTime">Whether to include the current time in log messages</param>
    public class WinterForgeConsoleLogger(WinterForgeProgressVerbosity verbosity, bool includeTime = false) : WinterForgeProgressTracker(verbosity)
    {
        private float progress;

        protected internal override void Report(string message)
        {
            string timePrefix = includeTime ? $"[{DateTime.Now:HH:mm:ss}] " : "";
            Console.WriteLine($"{timePrefix}{message} ({progress * 100:0.#}%)");
        }

        protected internal override void Report(float graphPercentage)
        {
            if (verbosity == WinterForgeProgressVerbosity.None)
            {
                string timePrefix = includeTime ? $"[{DateTime.Now:HH:mm:ss}] " : "";
                Console.WriteLine($"{timePrefix}{graphPercentage * 100:0.#}%");
                return;
            }

            progress = graphPercentage;
        }
    }

}
