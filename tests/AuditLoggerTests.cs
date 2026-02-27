// =============================================================================
// AuditLoggerTests.cs — BridgeMod.SDK v0.4.1
// =============================================================================
// Tests for AuditLogger.FlushToDisk and BehaviorGraphExecutor stuck-state logging.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using BridgeMod.Bridge;
using BridgeMod.Bridge.BehaviorGraphs;
using Newtonsoft.Json;
using Xunit;

namespace BridgeMod.Tests
{
    public class AuditLoggerFlushTests
    {
        private static AuditLogger CreateLogger(bool enabled = true) =>
            new AuditLogger(new BridgeConfig { EnableAuditLog = enabled });

        [Fact]
        public void FlushToDisk_WritesJsonFile_ContainingAllEntries()
        {
            var logger = CreateLogger();
            logger.Log(ErrorCodes.ParseErr001, "payload-a", "detail one");
            logger.Log(ErrorCodes.BoundClamp003, "payload-b", "detail two");

            var path = Path.Combine(Path.GetTempPath(), "bridgemod_audit_test_write.json");
            try
            {
                logger.FlushToDisk(path);

                Assert.True(File.Exists(path));
                var json = File.ReadAllText(path);
                var entries = JsonConvert.DeserializeObject<List<string>>(json);
                Assert.NotNull(entries);
                Assert.Equal(2, entries!.Count);
                Assert.Contains("PARSE_ERR_001", entries[0]);
                Assert.Contains("BOUND_CLAMP_003", entries[1]);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public void FlushToDisk_IsNoThrow_WhenPathIsInvalid()
        {
            var logger = CreateLogger();
            logger.Log(ErrorCodes.ParseErr001, "payload-c", "any detail");

            // An invalid path (null bytes, reserved device names, or inaccessible directory)
            // must not throw any exception.
            var exception = Record.Exception(() =>
                logger.FlushToDisk("/this/path/does/not/exist/audit.json"));

            Assert.Null(exception);
        }

        [Fact]
        public void FlushToDisk_ProducesEmptyArray_WhenNoEntriesLogged()
        {
            var logger = CreateLogger();

            var path = Path.Combine(Path.GetTempPath(), "bridgemod_audit_test_empty.json");
            try
            {
                logger.FlushToDisk(path);

                Assert.True(File.Exists(path));
                var json = File.ReadAllText(path);
                var entries = JsonConvert.DeserializeObject<List<string>>(json);
                Assert.NotNull(entries);
                Assert.Empty(entries!);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }

    public class StuckStateLoggingTests
    {
        private static BehaviorGraphDefinition CreateSimpleGraph()
        {
            var states = new List<BehaviorState>
            {
                new BehaviorState("idle"),
                new BehaviorState("running")
            };
            var transitions = new List<BehaviorTransition>
            {
                new BehaviorTransition("idle", "running", "start")
            };
            return new BehaviorGraphDefinition("test_stuck", "1.0", states, "idle", transitions);
        }

        [Fact]
        public void StuckState_LogsWarnStuck001_WhenNoTransitionMatches()
        {
            var logger = new AuditLogger(new BridgeConfig { EnableAuditLog = true });
            var graph = CreateSimpleGraph();
            var executor = new BehaviorGraphExecutor(graph, logger);

            executor.Dispatch("unknown_event");

            Assert.True(logger.HasCode(ErrorCodes.WarnStuck001));
        }

        [Fact]
        public void StuckState_DoesNotLog_WhenTransitionSucceeds()
        {
            var logger = new AuditLogger(new BridgeConfig { EnableAuditLog = true });
            var graph = CreateSimpleGraph();
            var executor = new BehaviorGraphExecutor(graph, logger);

            executor.Dispatch("start");

            Assert.False(logger.HasCode(ErrorCodes.WarnStuck001));
            Assert.Equal("running", executor.CurrentStateId);
        }
    }
}
