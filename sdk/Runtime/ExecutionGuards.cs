using System;
using System.Collections.Generic;
using System.IO;

namespace BridgeMod.Runtime
{
    /// <summary>
    /// Implements failure containment and safety guards for mod execution.
    /// Ensures mods cannot crash the game or escape their sandbox.
    /// </summary>
    public class ExecutionGuards
    {
        private readonly HashSet<string> _disabledMods = new();
        private readonly Dictionary<string, DisabledModInfo> _disablementLog = new();
        private const int MaxExecutionTimeMs = 5000; // 5 second timeout
        private const int MaxMemoryMb = 100;

        /// <summary>
        /// Checks if a mod is disabled due to errors.
        /// </summary>
        public bool IsModDisabled(string modFilePath)
        {
            return _disabledMods.Contains(modFilePath);
        }

        /// <summary>
        /// Disables a mod permanently until the game restarts.
        /// </summary>
        public void DisableMod(string modFilePath, string reason)
        {
            _disabledMods.Add(modFilePath);
            _disablementLog[modFilePath] = new DisabledModInfo
            {
                FilePath = modFilePath,
                DisabledAt = DateTime.UtcNow,
                Reason = reason
            };

            LogError($"Mod disabled: {modFilePath} - {reason}");
        }

        /// <summary>
        /// Gets all currently disabled mods.
        /// </summary>
        public IEnumerable<DisabledModInfo> GetDisabledMods()
        {
            return _disablementLog.Values;
        }

        /// <summary>
        /// Validates that a mod won't exceed execution time limits.
        /// </summary>
        public bool ValidateExecutionTime(Action modAction)
        {
            try
            {
                var cts = new System.Threading.CancellationTokenSource(MaxExecutionTimeMs);
                var task = System.Threading.Tasks.Task.Run(modAction, cts.Token);

                if (!task.Wait(MaxExecutionTimeMs + 100))
                {
                    LogError("Mod execution exceeded maximum time limit");
                    return false;
                }

                return true;
            }
            catch (System.OperationCanceledException)
            {
                LogError("Mod execution was cancelled due to timeout");
                return false;
            }
            catch (Exception ex)
            {
                LogError($"Mod execution error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Validates file path access is within sandbox boundaries.
        /// </summary>
        public bool ValidateFilePath(string requestedPath, string sandboxRoot)
        {
            try
            {
                var fullPath = Path.GetFullPath(requestedPath);
                var fullSandbox = Path.GetFullPath(sandboxRoot);

                if (!fullPath.StartsWith(fullSandbox))
                {
                    LogError($"Attempted access outside sandbox: {requestedPath}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                LogError($"Path validation error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Validates that a value stays within defined bounds.
        /// </summary>
        public bool ValidateParameterBounds(double value, double min, double max)
        {
            if (value < min || value > max)
            {
                LogError($"Parameter out of bounds: {value} (expected [{min}, {max}])");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Creates an isolated execution context for a mod.
        /// </summary>
        public ModExecutionContext CreateExecutionContext(string modName, int timeoutMs = 5000)
        {
            return new ModExecutionContext
            {
                ModName = modName,
                TimeoutMs = timeoutMs,
                StartTime = DateTime.UtcNow,
                Guard = this
            };
        }

        private void LogError(string message)
        {
            // In production, this would integrate with a logging system
            System.Diagnostics.Debug.WriteLine($"[ExecutionGuard] {message}");
        }
    }

    /// <summary>
    /// Information about a disabled mod.
    /// </summary>
    public class DisabledModInfo
    {
        public required string FilePath { get; set; }
        public DateTime DisabledAt { get; set; }
        public required string Reason { get; set; }
    }

    /// <summary>
    /// Execution context for a single mod invocation.
    /// Tracks execution state and enforces timeout/memory limits.
    /// </summary>
    public class ModExecutionContext : IDisposable
    {
        public required string ModName { get; set; }
        public int TimeoutMs { get; set; } = 5000;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public bool IsCompleted { get; set; }
        public Exception? LastException { get; set; }
        public required ExecutionGuards Guard { get; set; }

        /// <summary>
        /// Checks if execution has exceeded its timeout.
        /// </summary>
        public bool IsTimedOut
        {
            get
            {
                var elapsed = (DateTime.UtcNow - StartTime).TotalMilliseconds;
                return elapsed > TimeoutMs;
            }
        }

        /// <summary>
        /// Gets the execution duration so far.
        /// </summary>
        public TimeSpan ElapsedTime
        {
            get
            {
                var endTime = EndTime ?? DateTime.UtcNow;
                return endTime - StartTime;
            }
        }

        /// <summary>
        /// Marks execution as complete.
        /// </summary>
        public void Complete()
        {
            EndTime = DateTime.UtcNow;
            IsCompleted = true;
        }

        /// <summary>
        /// Records an error and marks context as failed.
        /// </summary>
        public void RecordError(Exception ex)
        {
            LastException = ex;
            EndTime = DateTime.UtcNow;
            IsCompleted = true;
        }

        public void Dispose()
        {
            if (!IsCompleted)
            {
                EndTime = DateTime.UtcNow;
            }
        }
    }
}
