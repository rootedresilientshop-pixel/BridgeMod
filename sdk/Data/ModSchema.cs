using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace BridgeMod.Data
{
    /// <summary>
    /// Defines schema validation rules for mod contents.
    /// Treats mods as untrusted data and enforces strict constraints.
    /// </summary>
    public class ModSchema
    {
        /// <summary>
        /// Unique identifier for this schema.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The type of mod this schema validates.
        /// </summary>
        public ModType ModType { get; set; }

        /// <summary>
        /// Schemas for each field, keyed by field name.
        /// </summary>
        public Dictionary<string, FieldSchema> Fields { get; set; }

        /// <summary>
        /// Maximum number of nodes allowed in a behavior graph.
        /// </summary>
        public int? MaxNodeCount { get; set; }

        /// <summary>
        /// Maximum depth allowed in a behavior graph.
        /// </summary>
        public int? MaxGraphDepth { get; set; }

        /// <summary>
        /// Bounds for procedural generation parameters, keyed by parameter name.
        /// </summary>
        public Dictionary<string, (double Min, double Max)>? ProcedureParameterBounds { get; set; }

        /// <summary>
        /// Creates a new mod schema for validation.
        /// </summary>
        /// <param name="name">Unique schema identifier.</param>
        /// <param name="modType">The type of mods this schema validates.</param>
        public ModSchema(string name, ModType modType)
        {
            Name = name;
            ModType = modType;
            Fields = new Dictionary<string, FieldSchema>();
        }

        /// <summary>
        /// Validates a JSON object against this schema.
        /// </summary>
        public bool Validate(JObject data, out List<string> errors)
        {
            errors = new List<string>();

            foreach (var field in Fields)
            {
                var fieldName = field.Key;
                var fieldSchema = field.Value;

                if (!data.ContainsKey(fieldName))
                {
                    if (fieldSchema.IsRequired)
                        errors.Add($"Required field '{fieldName}' is missing");
                    continue;
                }

                var value = data[fieldName];
                if (!fieldSchema.Validate(value, out var fieldErrors))
                {
                    errors.AddRange(fieldErrors.Select(e => $"Field '{fieldName}': {e}"));
                }
            }

            return errors.Count == 0;
        }

        /// <summary>
        /// Validates a behavior graph structure.
        /// </summary>
        public bool ValidateBehaviorGraph(JObject graph, out List<string> errors)
        {
            errors = new List<string>();

            if (!graph.ContainsKey("nodes") || graph["nodes"] is not JArray nodes)
            {
                errors.Add("Behavior graph must contain 'nodes' array");
                return false;
            }

            if (!graph.ContainsKey("edges") || graph["edges"] is not JArray edges)
            {
                errors.Add("Behavior graph must contain 'edges' array");
                return false;
            }

            if (MaxNodeCount.HasValue && nodes.Count > MaxNodeCount.Value)
            {
                errors.Add($"Graph exceeds maximum node count ({nodes.Count} > {MaxNodeCount.Value})");
            }

            // Validate edges reference existing nodes
            var nodeIds = nodes.OfType<JObject>()
                .Where(n => n.ContainsKey("id"))
                .Select(n => n["id"]?.ToString())
                .Where(id => !string.IsNullOrEmpty(id))
                .ToHashSet()!;

            foreach (var edge in edges.OfType<JObject>())
            {
                var from = edge["from"]?.ToString();
                var to = edge["to"]?.ToString();

                if (string.IsNullOrEmpty(from) || !nodeIds.Contains(from))
                    errors.Add($"Edge references undefined source node: {from}");

                if (string.IsNullOrEmpty(to) || !nodeIds.Contains(to))
                    errors.Add($"Edge references undefined target node: {to}");
            }

            if (MaxGraphDepth.HasValue)
            {
                var depth = CalculateGraphDepth(nodeIds, edges.OfType<JObject>().ToList());
                if (depth > MaxGraphDepth.Value)
                    errors.Add($"Graph depth exceeds maximum ({depth} > {MaxGraphDepth.Value})");
            }

            return errors.Count == 0;
        }

        private int CalculateGraphDepth(HashSet<string> nodes, List<JObject> edges)
        {
            if (nodes.Count == 0) return 0;

            // Simple BFS to calculate max depth
            var startNode = nodes.First();
            var visited = new HashSet<string>();
            var queue = new Queue<(string nodeId, int depth)>();

            queue.Enqueue((startNode, 0));
            int maxDepth = 0;

            while (queue.Count > 0)
            {
                var (nodeId, depth) = queue.Dequeue();

                if (visited.Contains(nodeId))
                    continue;

                visited.Add(nodeId);
                maxDepth = Math.Max(maxDepth, depth);

                var children = edges
                    .Where(e => e["from"]?.ToString() == nodeId)
                    .Select(e => e["to"]?.ToString())
                    .Where(id => !string.IsNullOrEmpty(id))
                    .ToList();

                foreach (var child in children)
                {
                    if (!visited.Contains(child!))
                        queue.Enqueue((child, depth + 1));
                }
            }

            return maxDepth;
        }
    }

    /// <summary>
    /// Schema definition for a single field within a mod.
    /// </summary>
    public class FieldSchema
    {
        /// <summary>
        /// The field name as it appears in mod content.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The data type of this field.
        /// </summary>
        public FieldType Type { get; set; }

        /// <summary>
        /// Whether this field must be present in mod content.
        /// </summary>
        public bool IsRequired { get; set; } = true;

        /// <summary>
        /// Default value if field is not provided.
        /// </summary>
        public object? DefaultValue { get; set; }

        /// <summary>
        /// Min and max bounds for numeric fields.
        /// </summary>
        public (double Min, double Max)? NumericBounds { get; set; }

        /// <summary>
        /// Allowed values for string fields (whitelist).
        /// </summary>
        public string[]? AllowedValues { get; set; }

        /// <summary>
        /// Maximum length for string fields.
        /// </summary>
        public int? MaxLength { get; set; }

        /// <summary>
        /// Maximum size for array fields.
        /// </summary>
        public int? MaxArraySize { get; set; }

        /// <summary>
        /// Creates a field schema with a name and type.
        /// </summary>
        /// <param name="name">The field name.</param>
        /// <param name="type">The field type.</param>
        public FieldSchema(string name, FieldType type)
        {
            Name = name;
            Type = type;
        }

        /// <summary>
        /// Validates a JSON value against this field's constraints.
        /// </summary>
        /// <param name="value">The JSON value to validate.</param>
        /// <param name="errors">List of validation errors found.</param>
        /// <returns>True if validation succeeds, false otherwise.</returns>
        public bool Validate(JToken? value, out List<string> errors)
        {
            errors = new List<string>();

            if (value == null || value.Type == JTokenType.Null)
            {
                if (IsRequired)
                    errors.Add($"Field is required");
                return !IsRequired;
            }

            return ValidateType(value, out errors);
        }

        private bool ValidateType(JToken value, out List<string> errors)
        {
            errors = new List<string>();

            switch (Type)
            {
                case FieldType.String:
                    if (value.Type != JTokenType.String)
                    {
                        errors.Add($"Expected string, got {value.Type}");
                        return false;
                    }

                    var strValue = value.ToString();
                    if (MaxLength.HasValue && strValue.Length > MaxLength.Value)
                        errors.Add($"String exceeds max length ({strValue.Length} > {MaxLength.Value})");

                    if (AllowedValues != null && !AllowedValues.Contains(strValue))
                        errors.Add($"Value '{strValue}' not in allowed values");

                    break;

                case FieldType.Number:
                    if (value.Type != JTokenType.Float && value.Type != JTokenType.Integer)
                    {
                        errors.Add($"Expected number, got {value.Type}");
                        return false;
                    }

                    if (double.TryParse(value.ToString(), out var numValue) && NumericBounds.HasValue)
                    {
                        var (min, max) = NumericBounds.Value;
                        if (numValue < min || numValue > max)
                            errors.Add($"Number {numValue} outside bounds [{min}, {max}]");
                    }

                    break;

                case FieldType.Boolean:
                    if (value.Type != JTokenType.Boolean)
                    {
                        errors.Add($"Expected boolean, got {value.Type}");
                        return false;
                    }

                    break;

                case FieldType.Array:
                    if (value.Type != JTokenType.Array)
                    {
                        errors.Add($"Expected array, got {value.Type}");
                        return false;
                    }

                    var arr = (JArray)value;
                    if (MaxArraySize.HasValue && arr.Count > MaxArraySize.Value)
                        errors.Add($"Array exceeds max size ({arr.Count} > {MaxArraySize.Value})");

                    break;

                case FieldType.Object:
                    if (value.Type != JTokenType.Object)
                    {
                        errors.Add($"Expected object, got {value.Type}");
                        return false;
                    }

                    break;

                default:
                    errors.Add($"Unknown field type: {Type}");
                    return false;
            }

            return errors.Count == 0;
        }
    }

    /// <summary>
    /// Supported JSON field types for mod schemas.
    /// </summary>
    public enum FieldType
    {
        /// <summary>
        /// A text string value.
        /// </summary>
        String,

        /// <summary>
        /// A numeric value (integer or float).
        /// </summary>
        Number,

        /// <summary>
        /// A true/false boolean value.
        /// </summary>
        Boolean,

        /// <summary>
        /// An array of values (homogeneous or heterogeneous).
        /// </summary>
        Array,

        /// <summary>
        /// A nested JSON object.
        /// </summary>
        Object
    }
}
