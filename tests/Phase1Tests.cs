using System.Collections.Generic;
using BridgeMod.Bridge;
using Xunit;

namespace BridgeMod.Tests
{
    public class ModBridgeValidationTests
    {
        private static ModBridge CreateBridge(int maxStat = 9999) =>
            new ModBridge(new BridgeConfig { MaxStatValue = maxStat, EnableAuditLog = true });

        [Fact]
        public void ValidPayload_ShouldPass()
        {
            var bridge = CreateBridge();
            var payload = new Dictionary<string, object>
            {
                { "name", "IronSword" },
                { "health", 100 }
            };

            var result = bridge.Validate(payload, "test-001");

            Assert.True(result.IsValid);
            Assert.NotNull(result.SanitizedPayload);
            Assert.Null(result.ErrorCode);
        }

        [Fact]
        public void ScriptInjection_ShouldBeRejected()
        {
            var bridge = CreateBridge();
            var payload = new Dictionary<string, object>
            {
                { "name", "<script>alert('xss')</script>" }
            };

            var result = bridge.Validate(payload, "test-002");

            Assert.False(result.IsValid);
            Assert.Equal(ErrorCodes.ParseErr001, result.ErrorCode);
            Assert.Null(result.SanitizedPayload);
        }

        [Fact]
        public void ScriptInjection_ShouldBeAuditLogged()
        {
            var bridge = CreateBridge();
            var payload = new Dictionary<string, object>
            {
                { "name", "<b>bold injection</b>" }
            };

            bridge.Validate(payload, "test-003");

            Assert.True(bridge.Logger.HasCode(ErrorCodes.ParseErr001));
        }

        [Fact]
        public void OutOfBoundsStat_ShouldBeClamped()
        {
            var bridge = CreateBridge(maxStat: 9999);
            var payload = new Dictionary<string, object>
            {
                { "health", 999999 }
            };

            var result = bridge.Validate(payload, "test-004");

            Assert.True(result.IsValid);
            Assert.NotNull(result.SanitizedPayload);
            Assert.Equal(9999, result.SanitizedPayload!["health"]);
        }

        [Fact]
        public void OutOfBoundsStat_ShouldBeAuditLogged()
        {
            var bridge = CreateBridge(maxStat: 100);
            var payload = new Dictionary<string, object>
            {
                { "health", 500 }
            };

            bridge.Validate(payload, "test-005");

            Assert.True(bridge.Logger.HasCode(ErrorCodes.BoundClamp003));
        }

        [Fact]
        public void StatAtMaxBoundary_ShouldNotBeClamped()
        {
            var bridge = CreateBridge(maxStat: 100);
            var payload = new Dictionary<string, object>
            {
                { "health", 100 }
            };

            var result = bridge.Validate(payload, "test-006");

            Assert.True(result.IsValid);
            Assert.False(bridge.Logger.HasCode(ErrorCodes.BoundClamp003));
        }

        [Fact]
        public void MultipleStatsClamped_ShouldAllBeFixed()
        {
            var bridge = CreateBridge(maxStat: 50);
            var payload = new Dictionary<string, object>
            {
                { "health", 200 },
                { "mana", 300 },
                { "strength", 1000 }
            };

            var result = bridge.Validate(payload, "test-007");

            Assert.True(result.IsValid);
            Assert.Equal(50, result.SanitizedPayload!["health"]);
            Assert.Equal(50, result.SanitizedPayload["mana"]);
            Assert.Equal(50, result.SanitizedPayload["strength"]);
        }

        [Fact]
        public void AuditLog_TracksEntriesInOrder()
        {
            var bridge = CreateBridge(maxStat: 10);
            var payload = new Dictionary<string, object>
            {
                { "health", 999 }
            };

            bridge.Validate(payload, "test-008");

            Assert.NotEmpty(bridge.Logger.Entries);
            Assert.Contains("test-008", bridge.Logger.Entries[0]);
        }

        [Fact]
        public void DisabledAuditLog_ShouldNotRecord()
        {
            var bridge = new ModBridge(new BridgeConfig
            {
                MaxStatValue = 10,
                EnableAuditLog = false
            });
            var payload = new Dictionary<string, object>
            {
                { "health", 999 }
            };

            bridge.Validate(payload, "test-009");

            Assert.Empty(bridge.Logger.Entries);
        }

        [Fact]
        public void EmptyPayload_ShouldPass()
        {
            var bridge = CreateBridge();
            var result = bridge.Validate(new Dictionary<string, object>(), "test-010");

            Assert.True(result.IsValid);
            Assert.NotNull(result.SanitizedPayload);
        }

        [Fact]
        public void HtmlTagInBody_ShouldBeRejected()
        {
            var bridge = CreateBridge();
            var payload = new Dictionary<string, object>
            {
                { "description", "<img src=x onerror=alert(1)>" }
            };

            var result = bridge.Validate(payload, "test-011");

            Assert.False(result.IsValid);
            Assert.Equal(ErrorCodes.ParseErr001, result.ErrorCode);
        }

        [Fact]
        public void BridgeConfig_DefaultsAreCorrect()
        {
            var config = new BridgeConfig();

            Assert.Equal(9999, config.MaxStatValue);
            Assert.Equal(0, config.MinStatValue);
            Assert.True(config.EnableAuditLog);
            Assert.Null(config.AuditLogPath);
        }
    }
}
