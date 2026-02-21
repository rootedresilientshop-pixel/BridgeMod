// =============================================================================
// BehaviorGraphs.cs — BridgeMod.SDK Phase 3 Runtime
// =============================================================================
// Copyright (c) 2024 DreamCraft: Legacies Project
// License: MIT (see LICENSE in repository root)
//
// Description:
//   Deterministic state machine engine for mod-authored behavior graphs.
//   All types are immutable. All transitions are explicit. No scripting,
//   no reflection, no dynamic evaluation.
//
//   This namespace provides a minimal, boring, explicit state machine that
//   preserves deterministic guarantees: same input → same output, always.
//
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;

namespace BridgeMod.Bridge.BehaviorGraphs
{
    /// <summary>
    /// Defines a deterministic state machine graph.
    /// All properties are immutable after construction.
    /// </summary>
    public sealed class BehaviorGraphDefinition
    {
        /// <summary>
        /// Unique identifier for this graph.
        /// Must not be null or whitespace.
        /// </summary>
        public string GraphId { get; }

        /// <summary>
        /// Semantic version of this graph definition (e.g., "1.0", "1.2.1").
        /// Used to track graph evolution.
        /// </summary>
        public string Version { get; }

        /// <summary>
        /// All states in this graph.
        /// Must not be null. Must contain at least one state.
        /// </summary>
        public IReadOnlyList<BehaviorState> States { get; }

        /// <summary>
        /// The ID of the initial state.
        /// Must reference a state that exists in <see cref="States"/>.
        /// </summary>
        public string InitialStateId { get; }

        /// <summary>
        /// All transitions in this graph.
        /// Must not be null. May be empty (static state machine).
        /// </summary>
        public IReadOnlyList<BehaviorTransition> Transitions { get; }

        /// <summary>
        /// Initializes a new behavior graph definition.
        /// </summary>
        /// <param name="graphId">Unique identifier for this graph.</param>
        /// <param name="version">Version string (e.g., "1.0").</param>
        /// <param name="states">Collection of states. Must not be null or empty.</param>
        /// <param name="initialStateId">ID of the initial state.</param>
        /// <param name="transitions">Collection of transitions. Must not be null.</param>
        public BehaviorGraphDefinition(
            string graphId,
            string version,
            IReadOnlyList<BehaviorState> states,
            string initialStateId,
            IReadOnlyList<BehaviorTransition> transitions)
        {
            if (string.IsNullOrWhiteSpace(graphId))
                throw new ArgumentException("Graph ID must not be null or whitespace.", nameof(graphId));

            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentException("Version must not be null or whitespace.", nameof(version));

            if (states == null || states.Count == 0)
                throw new ArgumentException("States collection must not be null or empty.", nameof(states));

            if (string.IsNullOrWhiteSpace(initialStateId))
                throw new ArgumentException("Initial state ID must not be null or whitespace.", nameof(initialStateId));

            if (transitions == null)
                throw new ArgumentNullException(nameof(transitions), "Transitions collection must not be null.");

            GraphId = graphId;
            Version = version;
            States = states;
            InitialStateId = initialStateId;
            Transitions = transitions;
        }
    }

    /// <summary>
    /// A single state in a behavior graph.
    /// States are immutable. They have no behavior of their own;
    /// transitions define how to leave a state.
    /// </summary>
    public sealed class BehaviorState
    {
        /// <summary>
        /// Unique identifier for this state within its graph.
        /// Must not be null or whitespace.
        /// </summary>
        public string StateId { get; }

        /// <summary>
        /// Human-readable name for this state.
        /// May be null. Used only for documentation and debugging.
        /// </summary>
        public string? DisplayName { get; }

        /// <summary>
        /// Optional metadata (string key-value pairs).
        /// Used for state classification or host-defined properties.
        /// May be null or empty.
        /// </summary>
        public IReadOnlyDictionary<string, string>? Metadata { get; }

        /// <summary>
        /// Initializes a new behavior state.
        /// </summary>
        /// <param name="stateId">Unique identifier for this state.</param>
        /// <param name="displayName">Optional human-readable name.</param>
        /// <param name="metadata">Optional metadata dictionary.</param>
        public BehaviorState(
            string stateId,
            string? displayName = null,
            IReadOnlyDictionary<string, string>? metadata = null)
        {
            if (string.IsNullOrWhiteSpace(stateId))
                throw new ArgumentException("State ID must not be null or whitespace.", nameof(stateId));

            StateId = stateId;
            DisplayName = displayName;
            Metadata = metadata;
        }
    }

    /// <summary>
    /// A transition between two states, triggered by an event.
    /// Transitions are immutable. Guards are optional and must use
    /// only simple value comparison operators (no arbitrary expressions).
    /// </summary>
    public sealed class BehaviorTransition
    {
        /// <summary>
        /// The state this transition originates from.
        /// Must not be null or whitespace.
        /// </summary>
        public string FromStateId { get; }

        /// <summary>
        /// The state this transition leads to.
        /// Must not be null or whitespace.
        /// </summary>
        public string ToStateId { get; }

        /// <summary>
        /// The event name that triggers this transition.
        /// Must not be null or whitespace.
        /// </summary>
        public string EventName { get; }

        /// <summary>
        /// Optional guard condition for this transition.
        /// If null, the transition always occurs when the event is dispatched.
        /// If not null, the transition only occurs if the guard evaluates to true.
        /// </summary>
        public TransitionGuard? Guard { get; }

        /// <summary>
        /// Initializes a new transition.
        /// </summary>
        /// <param name="fromStateId">Source state ID.</param>
        /// <param name="toStateId">Destination state ID.</param>
        /// <param name="eventName">Event that triggers this transition.</param>
        /// <param name="guard">Optional guard condition.</param>
        public BehaviorTransition(
            string fromStateId,
            string toStateId,
            string eventName,
            TransitionGuard? guard = null)
        {
            if (string.IsNullOrWhiteSpace(fromStateId))
                throw new ArgumentException("From state ID must not be null or whitespace.", nameof(fromStateId));

            if (string.IsNullOrWhiteSpace(toStateId))
                throw new ArgumentException("To state ID must not be null or whitespace.", nameof(toStateId));

            if (string.IsNullOrWhiteSpace(eventName))
                throw new ArgumentException("Event name must not be null or whitespace.", nameof(eventName));

            FromStateId = fromStateId;
            ToStateId = toStateId;
            EventName = eventName;
            Guard = guard;
        }
    }

    /// <summary>
    /// A deterministic guard condition for a transition.
    /// Guards use only simple value comparison operators.
    /// </summary>
    public sealed class TransitionGuard
    {
        /// <summary>
        /// The context key to evaluate (e.g., "health", "distance").
        /// Must not be null or whitespace.
        /// </summary>
        public string ContextKey { get; }

        /// <summary>
        /// The operator to use for comparison.
        /// </summary>
        public GuardOperator Operator { get; }

        /// <summary>
        /// The value to compare against.
        /// Must be a primitive type (int, float, bool, string).
        /// </summary>
        public object ComparisonValue { get; }

        /// <summary>
        /// Initializes a new transition guard.
        /// </summary>
        /// <param name="contextKey">The context dictionary key to evaluate.</param>
        /// <param name="op">The comparison operator.</param>
        /// <param name="comparisonValue">The value to compare against.</param>
        public TransitionGuard(string contextKey, GuardOperator op, object comparisonValue)
        {
            if (string.IsNullOrWhiteSpace(contextKey))
                throw new ArgumentException("Context key must not be null or whitespace.", nameof(contextKey));

            if (comparisonValue == null)
                throw new ArgumentNullException(nameof(comparisonValue), "Comparison value must not be null.");

            if (!IsValidPrimitiveType(comparisonValue))
                throw new ArgumentException(
                    "Comparison value must be int, float, bool, or string.", nameof(comparisonValue));

            ContextKey = contextKey;
            Operator = op;
            ComparisonValue = comparisonValue;
        }

        private static bool IsValidPrimitiveType(object value)
        {
            return value is int || value is float || value is bool || value is string;
        }
    }

    /// <summary>
    /// Guard comparison operators.
    /// Only simple, deterministic comparisons are supported.
    /// </summary>
    public enum GuardOperator
    {
        /// <summary>
        /// Value equals the comparison value.
        /// </summary>
        Equals = 0,

        /// <summary>
        /// Value is greater than the comparison value (numeric only).
        /// </summary>
        GreaterThan = 1,

        /// <summary>
        /// Value is less than the comparison value (numeric only).
        /// </summary>
        LessThan = 2,

        /// <summary>
        /// Value is greater than or equal to the comparison value (numeric only).
        /// </summary>
        GreaterThanOrEqual = 3,

        /// <summary>
        /// Value is less than or equal to the comparison value (numeric only).
        /// </summary>
        LessThanOrEqual = 4,

        /// <summary>
        /// Value does not equal the comparison value.
        /// </summary>
        NotEquals = 5
    }

    /// <summary>
    /// Validates a behavior graph for correctness before execution.
    /// All validation happens upfront; graphs are guaranteed valid before use.
    /// </summary>
    public static class BehaviorGraphValidator
    {
        /// <summary>
        /// Validates a behavior graph definition.
        /// </summary>
        /// <param name="definition">The graph definition to validate.</param>
        /// <exception cref="ArgumentException">
        /// Thrown if the graph is invalid.
        /// </exception>
        public static void Validate(BehaviorGraphDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            // Check: No duplicate state IDs
            var stateIds = definition.States.Select(s => s.StateId).ToList();
            if (stateIds.Count != stateIds.Distinct().Count())
                throw new ArgumentException("Duplicate state IDs detected in graph.");

            // Check: Initial state exists
            if (!stateIds.Contains(definition.InitialStateId))
                throw new ArgumentException(
                    $"Initial state '{definition.InitialStateId}' does not exist in the graph.");

            // Check: All transitions reference valid states
            foreach (var transition in definition.Transitions)
            {
                if (!stateIds.Contains(transition.FromStateId))
                    throw new ArgumentException(
                        $"Transition references non-existent state: {transition.FromStateId}");

                if (!stateIds.Contains(transition.ToStateId))
                    throw new ArgumentException(
                        $"Transition references non-existent state: {transition.ToStateId}");
            }

            // Check: No ambiguous transitions (multiple transitions from same state with same event and no guards)
            var transitionGroups = definition.Transitions
                .GroupBy(t => new { t.FromStateId, t.EventName })
                .ToList();

            foreach (var group in transitionGroups)
            {
                var unguardedCount = group.Count(t => t.Guard == null);
                if (unguardedCount > 1)
                    throw new ArgumentException(
                        $"Ambiguous transitions: multiple unguarded transitions from state '{group.Key.FromStateId}' on event '{group.Key.EventName}'.");

                // If there are guarded transitions, ensure they don't overlap (basic check)
                if (unguardedCount > 0 && group.Count() > 1)
                    throw new ArgumentException(
                        $"Cannot mix guarded and unguarded transitions for the same event from the same state.");
            }
        }
    }

    /// <summary>
    /// Deterministic state machine executor.
    /// Executes a behavior graph by dispatching events and evaluating guards.
    /// Provides strict determinism: same graph + same events = same result, always.
    /// </summary>
    public sealed class BehaviorGraphExecutor
    {
        private readonly BehaviorGraphDefinition _definition;
        private string _currentStateId;

        /// <summary>
        /// Gets the current state ID.
        /// </summary>
        public string CurrentStateId
        {
            get { return _currentStateId; }
        }

        /// <summary>
        /// Initializes the executor with a graph definition.
        /// The executor is not reusable; create a new instance for each execution.
        /// </summary>
        /// <param name="definition">The graph definition. Must be valid.</param>
        public BehaviorGraphExecutor(BehaviorGraphDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            BehaviorGraphValidator.Validate(definition);

            _definition = definition;
            _currentStateId = definition.InitialStateId;
        }

        /// <summary>
        /// Dispatches an event to the current state.
        /// If a matching transition exists and its guard (if any) evaluates to true,
        /// the state changes to the transition's target state.
        /// Otherwise, the state remains unchanged.
        /// </summary>
        /// <param name="eventName">The event to dispatch.</param>
        /// <param name="context">
        /// Optional context data for guard evaluation.
        /// Must contain only primitive types (int, float, bool, string).
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown if context data contains non-primitive types.
        /// </exception>
        public void Dispatch(string eventName, Dictionary<string, object>? context = null)
        {
            if (string.IsNullOrWhiteSpace(eventName))
                throw new ArgumentException("Event name must not be null or whitespace.", nameof(eventName));

            // Validate context data
            if (context != null)
            {
                foreach (var kvp in context)
                {
                    if (kvp.Value != null && !IsValidContextType(kvp.Value))
                        throw new ArgumentException(
                            $"Context value for key '{kvp.Key}' must be int, float, bool, or string.");
                }
            }

            // Find matching transitions from the current state with the given event
            var matchingTransitions = _definition.Transitions
                .Where(t => t.FromStateId == _currentStateId && t.EventName == eventName)
                .ToList();

            // Find the first transition whose guard (if any) evaluates to true
            foreach (var transition in matchingTransitions)
            {
                if (transition.Guard == null || EvaluateGuard(transition.Guard, context))
                {
                    _currentStateId = transition.ToStateId;
                    return;
                }
            }

            // No matching transition: remain in current state
        }

        /// <summary>
        /// Resets the executor to the initial state.
        /// Useful for testing or reusing the executor multiple times.
        /// </summary>
        public void Reset()
        {
            _currentStateId = _definition.InitialStateId;
        }

        private static bool IsValidContextType(object value)
        {
            return value is int || value is float || value is bool || value is string;
        }

        private static bool EvaluateGuard(TransitionGuard guard, Dictionary<string, object>? context)
        {
            // No context: guard cannot be evaluated, so assume false
            if (context == null || !context.ContainsKey(guard.ContextKey))
                return false;

            var contextValue = context[guard.ContextKey];

            // Null context value: cannot compare, so assume false
            if (contextValue == null)
                return false;

            try
            {
                return guard.Operator switch
                {
                    GuardOperator.Equals => Equals(contextValue, guard.ComparisonValue),
                    GuardOperator.NotEquals => !Equals(contextValue, guard.ComparisonValue),
                    GuardOperator.GreaterThan => CompareNumeric(contextValue, guard.ComparisonValue) > 0,
                    GuardOperator.LessThan => CompareNumeric(contextValue, guard.ComparisonValue) < 0,
                    GuardOperator.GreaterThanOrEqual => CompareNumeric(contextValue, guard.ComparisonValue) >= 0,
                    GuardOperator.LessThanOrEqual => CompareNumeric(contextValue, guard.ComparisonValue) <= 0,
                    _ => false
                };
            }
            catch
            {
                // Type mismatch or invalid comparison: guard evaluates to false
                return false;
            }
        }

        private static int CompareNumeric(object left, object right)
        {
            double leftVal = Convert.ToDouble(left);
            double rightVal = Convert.ToDouble(right);
            return leftVal.CompareTo(rightVal);
        }
    }
}
