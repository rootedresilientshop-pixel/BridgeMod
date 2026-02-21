// =============================================================================
// ModBehaviorGraphTests.cs — BridgeMod Phase 3 Runtime
// =============================================================================
// Tests for deterministic behavior graph execution.
// Validates: immutability, validation, deterministic transitions, guard evaluation.
// =============================================================================

using System;
using System.Collections.Generic;
using BridgeMod.Bridge.BehaviorGraphs;
using Xunit;

namespace BridgeMod.Tests
{
    public class BehaviorStateTests
    {
        [Fact]
        public void BehaviorState_WithValidArgs_CreatesSuccessfully()
        {
            var state = new BehaviorState("idle", "Idle State");

            Assert.Equal("idle", state.StateId);
            Assert.Equal("Idle State", state.DisplayName);
            Assert.Null(state.Metadata);
        }

        [Fact]
        public void BehaviorState_EmptyStateId_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new BehaviorState(string.Empty));
        }

        [Fact]
        public void BehaviorState_WithMetadata_StoresIt()
        {
            var metadata = new Dictionary<string, string> { { "type", "active" } };
            var state = new BehaviorState("running", "Running", metadata);

            Assert.NotNull(state.Metadata);
            Assert.Equal("active", state.Metadata["type"]);
        }
    }

    public class BehaviorTransitionTests
    {
        [Fact]
        public void BehaviorTransition_WithValidArgs_CreatesSuccessfully()
        {
            var transition = new BehaviorTransition("idle", "running", "start");

            Assert.Equal("idle", transition.FromStateId);
            Assert.Equal("running", transition.ToStateId);
            Assert.Equal("start", transition.EventName);
            Assert.Null(transition.Guard);
        }

        [Fact]
        public void BehaviorTransition_WithGuard_StoresIt()
        {
            var guard = new TransitionGuard("energy", GuardOperator.GreaterThan, 10);
            var transition = new BehaviorTransition("idle", "running", "start", guard);

            Assert.NotNull(transition.Guard);
            Assert.Equal("energy", transition.Guard.ContextKey);
        }

        [Fact]
        public void BehaviorTransition_NullEventName_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                new BehaviorTransition("idle", "running", null!));
        }
    }

    public class TransitionGuardTests
    {
        [Fact]
        public void TransitionGuard_WithIntValue_CreatesSuccessfully()
        {
            var guard = new TransitionGuard("health", GuardOperator.GreaterThan, 50);

            Assert.Equal("health", guard.ContextKey);
            Assert.Equal(GuardOperator.GreaterThan, guard.Operator);
            Assert.Equal(50, guard.ComparisonValue);
        }

        [Fact]
        public void TransitionGuard_WithFloatValue_CreatesSuccessfully()
        {
            var guard = new TransitionGuard("distance", GuardOperator.LessThan, 5.5f);

            Assert.Equal(5.5f, guard.ComparisonValue);
        }

        [Fact]
        public void TransitionGuard_WithStringValue_CreatesSuccessfully()
        {
            var guard = new TransitionGuard("mood", GuardOperator.Equals, "happy");

            Assert.Equal("happy", guard.ComparisonValue);
        }

        [Fact]
        public void TransitionGuard_WithBoolValue_CreatesSuccessfully()
        {
            var guard = new TransitionGuard("active", GuardOperator.Equals, true);

            Assert.Equal(true, guard.ComparisonValue);
        }

        [Fact]
        public void TransitionGuard_WithInvalidType_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                new TransitionGuard("key", GuardOperator.Equals, new object()));
        }

        [Fact]
        public void TransitionGuard_NullContextKey_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                new TransitionGuard(null!, GuardOperator.Equals, 10));
        }
    }

    public class BehaviorGraphDefinitionTests
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
                new BehaviorTransition("idle", "running", "start"),
                new BehaviorTransition("running", "idle", "stop")
            };
            return new BehaviorGraphDefinition("test_graph", "1.0", states, "idle", transitions);
        }

        [Fact]
        public void BehaviorGraphDefinition_WithValidArgs_CreatesSuccessfully()
        {
            var graph = CreateSimpleGraph();

            Assert.Equal("test_graph", graph.GraphId);
            Assert.Equal("1.0", graph.Version);
            Assert.Equal(2, graph.States.Count);
            Assert.Equal("idle", graph.InitialStateId);
        }

        [Fact]
        public void BehaviorGraphDefinition_EmptyGraphId_ThrowsArgumentException()
        {
            var states = new List<BehaviorState> { new BehaviorState("idle") };
            var transitions = new List<BehaviorTransition>();

            Assert.Throws<ArgumentException>(() =>
                new BehaviorGraphDefinition("", "1.0", states, "idle", transitions));
        }

        [Fact]
        public void BehaviorGraphDefinition_NullStates_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                new BehaviorGraphDefinition("graph", "1.0", null!, "idle", new List<BehaviorTransition>()));
        }

        [Fact]
        public void BehaviorGraphDefinition_EmptyStates_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                new BehaviorGraphDefinition("graph", "1.0", new List<BehaviorState>(), "idle", new List<BehaviorTransition>()));
        }
    }

    public class BehaviorGraphValidatorTests
    {
        private static BehaviorGraphDefinition CreateValidGraph()
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
            return new BehaviorGraphDefinition("test", "1.0", states, "idle", transitions);
        }

        [Fact]
        public void Validator_ValidGraph_DoesNotThrow()
        {
            var graph = CreateValidGraph();

            // Should not throw
            BehaviorGraphValidator.Validate(graph);
        }

        [Fact]
        public void Validator_DuplicateStateIds_ThrowsArgumentException()
        {
            var states = new List<BehaviorState>
            {
                new BehaviorState("idle"),
                new BehaviorState("idle")  // Duplicate
            };
            var transitions = new List<BehaviorTransition>();
            var graph = new BehaviorGraphDefinition("test", "1.0", states, "idle", transitions);

            Assert.Throws<ArgumentException>(() => BehaviorGraphValidator.Validate(graph));
        }

        [Fact]
        public void Validator_InitialStateNotExists_ThrowsArgumentException()
        {
            var states = new List<BehaviorState> { new BehaviorState("idle") };
            var transitions = new List<BehaviorTransition>();
            var graph = new BehaviorGraphDefinition("test", "1.0", states, "nonexistent", transitions);

            Assert.Throws<ArgumentException>(() => BehaviorGraphValidator.Validate(graph));
        }

        [Fact]
        public void Validator_TransitionFromInvalidState_ThrowsArgumentException()
        {
            var states = new List<BehaviorState>
            {
                new BehaviorState("idle"),
                new BehaviorState("running")
            };
            var transitions = new List<BehaviorTransition>
            {
                new BehaviorTransition("nonexistent", "running", "start")
            };
            var graph = new BehaviorGraphDefinition("test", "1.0", states, "idle", transitions);

            Assert.Throws<ArgumentException>(() => BehaviorGraphValidator.Validate(graph));
        }

        [Fact]
        public void Validator_TransitionToInvalidState_ThrowsArgumentException()
        {
            var states = new List<BehaviorState>
            {
                new BehaviorState("idle"),
                new BehaviorState("running")
            };
            var transitions = new List<BehaviorTransition>
            {
                new BehaviorTransition("idle", "nonexistent", "start")
            };
            var graph = new BehaviorGraphDefinition("test", "1.0", states, "idle", transitions);

            Assert.Throws<ArgumentException>(() => BehaviorGraphValidator.Validate(graph));
        }

        [Fact]
        public void Validator_AmbiguousTransitions_ThrowsArgumentException()
        {
            var states = new List<BehaviorState>
            {
                new BehaviorState("idle"),
                new BehaviorState("running"),
                new BehaviorState("paused")
            };
            var transitions = new List<BehaviorTransition>
            {
                new BehaviorTransition("idle", "running", "start"),
                new BehaviorTransition("idle", "paused", "start")  // Same event, no guards
            };
            var graph = new BehaviorGraphDefinition("test", "1.0", states, "idle", transitions);

            Assert.Throws<ArgumentException>(() => BehaviorGraphValidator.Validate(graph));
        }
    }

    public class BehaviorGraphExecutorTests
    {
        private static BehaviorGraphDefinition CreateSimpleGraph()
        {
            var states = new List<BehaviorState>
            {
                new BehaviorState("idle"),
                new BehaviorState("running"),
                new BehaviorState("stopped")
            };
            var transitions = new List<BehaviorTransition>
            {
                new BehaviorTransition("idle", "running", "start"),
                new BehaviorTransition("running", "stopped", "stop"),
                new BehaviorTransition("stopped", "idle", "reset")
            };
            return new BehaviorGraphDefinition("test", "1.0", states, "idle", transitions);
        }

        [Fact]
        public void Executor_StartsInInitialState()
        {
            var graph = CreateSimpleGraph();
            var executor = new BehaviorGraphExecutor(graph);

            Assert.Equal("idle", executor.CurrentStateId);
        }

        [Fact]
        public void Executor_TransitionsOnEvent()
        {
            var graph = CreateSimpleGraph();
            var executor = new BehaviorGraphExecutor(graph);

            executor.Dispatch("start");

            Assert.Equal("running", executor.CurrentStateId);
        }

        [Fact]
        public void Executor_ChainedTransitions()
        {
            var graph = CreateSimpleGraph();
            var executor = new BehaviorGraphExecutor(graph);

            executor.Dispatch("start");
            Assert.Equal("running", executor.CurrentStateId);

            executor.Dispatch("stop");
            Assert.Equal("stopped", executor.CurrentStateId);

            executor.Dispatch("reset");
            Assert.Equal("idle", executor.CurrentStateId);
        }

        [Fact]
        public void Executor_NoMatchingTransition_StaysInCurrentState()
        {
            var graph = CreateSimpleGraph();
            var executor = new BehaviorGraphExecutor(graph);

            executor.Dispatch("unknown_event");

            Assert.Equal("idle", executor.CurrentStateId);
        }

        [Fact]
        public void Executor_DeterministicExecution_SameInputProducesSameOutput()
        {
            var graph = CreateSimpleGraph();

            var executor1 = new BehaviorGraphExecutor(graph);
            executor1.Dispatch("start");
            executor1.Dispatch("stop");
            var state1 = executor1.CurrentStateId;

            var executor2 = new BehaviorGraphExecutor(graph);
            executor2.Dispatch("start");
            executor2.Dispatch("stop");
            var state2 = executor2.CurrentStateId;

            Assert.Equal(state1, state2);
            Assert.Equal("stopped", state1);
        }

        [Fact]
        public void Executor_Reset_ReturnsToInitialState()
        {
            var graph = CreateSimpleGraph();
            var executor = new BehaviorGraphExecutor(graph);

            executor.Dispatch("start");
            Assert.Equal("running", executor.CurrentStateId);

            executor.Reset();
            Assert.Equal("idle", executor.CurrentStateId);
        }

        [Fact]
        public void Executor_WithGuard_EvaluatesCondition()
        {
            var states = new List<BehaviorState>
            {
                new BehaviorState("idle"),
                new BehaviorState("running")
            };
            var guard = new TransitionGuard("energy", GuardOperator.GreaterThan, 50);
            var transitions = new List<BehaviorTransition>
            {
                new BehaviorTransition("idle", "running", "start", guard)
            };
            var graph = new BehaviorGraphDefinition("test", "1.0", states, "idle", transitions);
            var executor = new BehaviorGraphExecutor(graph);

            // Without context: guard fails
            executor.Dispatch("start");
            Assert.Equal("idle", executor.CurrentStateId);

            // With low energy: guard fails
            var context = new Dictionary<string, object> { { "energy", 30 } };
            executor.Dispatch("start", context);
            Assert.Equal("idle", executor.CurrentStateId);

            // With high energy: guard succeeds
            context["energy"] = 100;
            executor.Dispatch("start", context);
            Assert.Equal("running", executor.CurrentStateId);
        }

        [Fact]
        public void Executor_WithMultipleGuards_PicksFirstMatching()
        {
            var states = new List<BehaviorState>
            {
                new BehaviorState("idle"),
                new BehaviorState("run"),
                new BehaviorState("walk")
            };
            var transitions = new List<BehaviorTransition>
            {
                // Try walk first
                new BehaviorTransition("idle", "walk", "move", new TransitionGuard("energy", GuardOperator.LessThanOrEqual, 50)),
                // Try run (only if energy > 50)
                new BehaviorTransition("idle", "run", "move", new TransitionGuard("energy", GuardOperator.GreaterThan, 50))
            };
            var graph = new BehaviorGraphDefinition("test", "1.0", states, "idle", transitions);
            var executor = new BehaviorGraphExecutor(graph);

            // With low energy: should walk
            executor.Dispatch("move", new Dictionary<string, object> { { "energy", 30 } });
            Assert.Equal("walk", executor.CurrentStateId);

            // Reset and try with high energy: should run
            executor.Reset();
            executor.Dispatch("move", new Dictionary<string, object> { { "energy", 100 } });
            Assert.Equal("run", executor.CurrentStateId);
        }

        [Fact]
        public void Executor_InvalidContextType_IsRejected()
        {
            var graph = CreateSimpleGraph();
            var executor = new BehaviorGraphExecutor(graph);

            var context = new Dictionary<string, object> { { "key", new object() as object } };

            Assert.Throws<ArgumentException>(() => executor.Dispatch("start", context!));
        }

        [Fact]
        public void Executor_NullInContext_GuardEvaluatesToFalse()
        {
            var states = new List<BehaviorState>
            {
                new BehaviorState("idle"),
                new BehaviorState("running")
            };
            var guard = new TransitionGuard("energy", GuardOperator.GreaterThan, 50);
            var transitions = new List<BehaviorTransition>
            {
                new BehaviorTransition("idle", "running", "start", guard)
            };
            var graph = new BehaviorGraphDefinition("test", "1.0", states, "idle", transitions);
            var executor = new BehaviorGraphExecutor(graph);

            // Context with null value
            executor.Dispatch("start", new Dictionary<string, object> { { "energy", null! } });
            Assert.Equal("idle", executor.CurrentStateId);
        }

        [Fact]
        public void Executor_GuardWithAllOperators()
        {
            var states = new List<BehaviorState>
            {
                new BehaviorState("a"),
                new BehaviorState("b")
            };

            // Test all operators
            TestGuardOperator(GuardOperator.Equals, 10, 10, true);
            TestGuardOperator(GuardOperator.Equals, 10, 20, false);
            TestGuardOperator(GuardOperator.NotEquals, 10, 20, true);
            TestGuardOperator(GuardOperator.NotEquals, 10, 10, false);
            TestGuardOperator(GuardOperator.GreaterThan, 20, 10, true);
            TestGuardOperator(GuardOperator.GreaterThan, 10, 20, false);
            TestGuardOperator(GuardOperator.LessThan, 10, 20, true);
            TestGuardOperator(GuardOperator.LessThan, 20, 10, false);
            TestGuardOperator(GuardOperator.GreaterThanOrEqual, 20, 10, true);
            TestGuardOperator(GuardOperator.GreaterThanOrEqual, 10, 10, true);
            TestGuardOperator(GuardOperator.GreaterThanOrEqual, 10, 20, false);
            TestGuardOperator(GuardOperator.LessThanOrEqual, 10, 20, true);
            TestGuardOperator(GuardOperator.LessThanOrEqual, 10, 10, true);
            TestGuardOperator(GuardOperator.LessThanOrEqual, 20, 10, false);
        }

        private static void TestGuardOperator(GuardOperator op, int contextValue, int compareValue, bool expectedResult)
        {
            var states = new List<BehaviorState>
            {
                new BehaviorState("a"),
                new BehaviorState("b")
            };
            var guard = new TransitionGuard("val", op, compareValue);
            var transitions = new List<BehaviorTransition>
            {
                new BehaviorTransition("a", "b", "go", guard)
            };
            var graph = new BehaviorGraphDefinition("test", "1.0", states, "a", transitions);
            var executor = new BehaviorGraphExecutor(graph);

            executor.Dispatch("go", new Dictionary<string, object> { { "val", contextValue } });

            var expectedState = expectedResult ? "b" : "a";
            Assert.Equal(expectedState, executor.CurrentStateId);
        }
    }

    public class BehaviorGraphDeterminismTests
    {
        [Fact]
        public void DeterminismTest_1000Executions_AllProduceSameResult()
        {
            var states = new List<BehaviorState>
            {
                new BehaviorState("s1"),
                new BehaviorState("s2"),
                new BehaviorState("s3")
            };
            var transitions = new List<BehaviorTransition>
            {
                new BehaviorTransition("s1", "s2", "go"),
                new BehaviorTransition("s2", "s3", "go"),
                new BehaviorTransition("s3", "s1", "reset")
            };
            var graph = new BehaviorGraphDefinition("test", "1.0", states, "s1", transitions);

            var results = new List<string>();
            for (int i = 0; i < 1000; i++)
            {
                var executor = new BehaviorGraphExecutor(graph);
                executor.Dispatch("go");
                executor.Dispatch("go");
                executor.Dispatch("reset");
                results.Add(executor.CurrentStateId);
            }

            // All 1000 executions should produce "s1"
            Assert.True(results.TrueForAll(r => r == "s1"));
        }
    }
}
