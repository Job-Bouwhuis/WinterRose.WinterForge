using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinterRose.WinterForgeSerializing.Logging
{
    /// <summary>
    /// When should <see cref="WinterForge"/> mark its progress
    /// </summary>
    public enum WinterForgeProgressVerbosity
    {
        /// <summary>
        /// Never
        /// </summary>
        None,
        /// <summary>
        /// When an instance has been created
        /// </summary>
        Instance,
        /// <summary>
        /// When a class has been created, structs dont count
        /// </summary>
        ClassOnly,
        /// <summary>
        /// When classes or structs are created, and for each field that is handled
        /// </summary>
        Full
    }
    /// <summary>
    /// An abstract class to hook into WinterForge progress in its process to serialize or deserialize a data structure
    /// </summary>
    public abstract class WinterForgeProgressTracker
    {
        private Stack<string> pathStack = new();
        private WinterForgeProgressVerbosity verbosity;

        protected WinterForgeProgressTracker(WinterForgeProgressVerbosity verbosity)
        {
            this.verbosity = verbosity;
        }

        internal void OnInstance(string message, string typeName, bool isClass, int currentInstruction, int totalInstructions)
        {
            if(totalInstructions is not 0)
            {
                float progress = (float)currentInstruction / totalInstructions;
                Report(progress);
            }
            

            if (verbosity == WinterForgeProgressVerbosity.None)
                return;

            if (verbosity == WinterForgeProgressVerbosity.ClassOnly && !isClass)
                return;

            if (verbosity == WinterForgeProgressVerbosity.Full)
                pathStack.Push(typeName);

            
            Report(message);

        }

        internal void OnExitInstance()
        {
            if (verbosity != WinterForgeProgressVerbosity.Full)
                return;

            if (pathStack != null && pathStack.Count > 0)
                pathStack.Pop();
        }

        internal void OnField(string fieldName, int currentInstruction, int totalInstructions)
        {
            if (verbosity == WinterForgeProgressVerbosity.Full)
            {
                pathStack.Push(fieldName);
                if (totalInstructions is not 0)
                {
                    float progress = (float)currentInstruction / totalInstructions;
                    ReportCurrentPath(progress);
                }
                pathStack.Pop();
            }
        }

        private void ReportCurrentPath(float progress)
        {
            string path = string.Join('.', pathStack.Reverse());
            Report(progress);
            Report(path);
        }

        /// <summary>
        /// Invoked when a new message is ready
        /// </summary>
        /// <param name="message"></param>
        internal protected abstract void Report(string message);
        /// <summary>
        /// Invoked when new progress has been made
        /// </summary>
        /// <param name="graphPercentage"></param>
        internal protected abstract void Report(float graphPercentage);
        internal void OnMethod(string typeName, string methodName) => Report($"Invoking: {typeName}.{methodName}()");
    }
}
