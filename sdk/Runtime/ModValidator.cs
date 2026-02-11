using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using BridgeMod.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BridgeMod.Runtime
{
    /// <summary>
    /// Validates mod packages locally before loading.
    /// Treats mods as untrusted data and enforces strict checks.
    /// </summary>
    public class ModValidator
    {
        private readonly Dictionary<ModType, ModSchema> _schemas;

        public ModValidator()
        {
            _schemas = new Dictionary<ModType, ModSchema>();
            InitializeDefaultSchemas();
        }

        /// <summary>
        /// Registers a schema for a specific mod type.
        /// </summary>
        public void RegisterSchema(ModType modType, ModSchema schema)
        {
            _schemas[modType] = schema;
        }

        /// <summary>
        /// Validates a mod package file (.zip).
        /// Returns validation result with detailed error reporting.
        /// </summary>
        public ValidationResult ValidateModPackage(string modPackagePath)
        {
            var result = new ValidationResult();

            if (!File.Exists(modPackagePath))
            {
                result.AddError($"Mod package file not found: {modPackagePath}");
                return result;
            }

            try
            {
                using (var archive = ZipFile.OpenRead(modPackagePath))
                {
                    // Check for manifest
                    var manifestEntry = archive.GetEntry("manifest.json");
                    if (manifestEntry == null)
                    {
                        result.AddError("Mod package must contain manifest.json");
                        return result;
                    }

                    // Parse manifest
                    string manifestContent;
                    using (var stream = manifestEntry.Open())
                    using (var reader = new StreamReader(stream))
                    {
                        manifestContent = reader.ReadToEnd();
                    }

                    if (!TryParseManifest(manifestContent, out var manifest, out var manifestErrors))
                    {
                        result.Errors.AddRange(manifestErrors);
                        return result;
                    }

                    result.Manifest = manifest!;

                    // Validate manifest structure
                    if (!manifest.IsValid(out var validationErrors))
                    {
                        result.Errors.AddRange(validationErrors);
                        return result;
                    }

                    // Verify all declared files exist in archive
                    var archivedFiles = archive.Entries.Select(e => e.FullName).ToHashSet();
                    foreach (var declaredFile in manifest.Files)
                    {
                        if (!archivedFiles.Contains(declaredFile) && declaredFile != "manifest.json")
                        {
                            result.AddWarning($"Declared file not found in package: {declaredFile}");
                        }
                    }

                    // Validate mod contents based on type
                    result.IsValid = ValidateModContents(archive, manifest, result);
                }
            }
            catch (Exception ex)
            {
                result.AddError($"Failed to validate mod package: {ex.Message}");
            }

            return result;
        }

        private bool ValidateModContents(ZipArchive archive, ModManifest manifest, ValidationResult result)
        {
            if (!_schemas.TryGetValue(manifest.ModType, out var schema))
            {
                result.AddWarning($"No schema registered for mod type: {manifest.ModType}");
                return true; // Don't fail if no schema
            }

            var contentPath = manifest.ModType switch
            {
                ModType.Data => "data/",
                ModType.BehaviorGraph => "graphs/",
                ModType.Procedural => "procedural/",
                _ => null
            };

            if (contentPath == null)
                return true;

            var contentFiles = manifest.Files.Where(f => f.StartsWith(contentPath)).ToList();

            foreach (var filePath in contentFiles)
            {
                var entry = archive.GetEntry(filePath);
                if (entry == null) continue;

                try
                {
                    string content;
                    using (var stream = entry.Open())
                    using (var reader = new StreamReader(stream))
                    {
                        content = reader.ReadToEnd();
                    }

                    var json = JObject.Parse(content);

                    if (manifest.ModType == ModType.BehaviorGraph)
                    {
                        if (!schema.ValidateBehaviorGraph(json, out var graphErrors))
                        {
                            result.Errors.AddRange(graphErrors.Select(e => $"{filePath}: {e}"));
                            return false;
                        }
                    }
                    else
                    {
                        if (!schema.Validate(json, out var schemaErrors))
                        {
                            result.Errors.AddRange(schemaErrors.Select(e => $"{filePath}: {e}"));
                            return false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.AddError($"Failed to parse {filePath}: {ex.Message}");
                    return false;
                }
            }

            return true;
        }

        private bool TryParseManifest(string content, out ModManifest? manifest, out List<string> errors)
        {
            manifest = null;
            errors = new List<string>();

            try
            {
                manifest = Newtonsoft.Json.JsonConvert.DeserializeObject<ModManifest>(content);
                if (manifest == null)
                {
                    errors.Add("Manifest deserialization resulted in null");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                errors.Add($"Invalid manifest JSON: {ex.Message}");
                return false;
            }
        }

        private void InitializeDefaultSchemas()
        {
            // Data mod schema
            var dataSchema = new ModSchema("Data", ModType.Data);
            dataSchema.Fields["type"] = new FieldSchema("type", FieldType.String) { IsRequired = true };
            dataSchema.Fields["content"] = new FieldSchema("content", FieldType.Object) { IsRequired = true };
            RegisterSchema(ModType.Data, dataSchema);

            // Behavior graph schema
            var behaviorSchema = new ModSchema("BehaviorGraph", ModType.BehaviorGraph)
            {
                MaxNodeCount = 1000,
                MaxGraphDepth = 50
            };
            behaviorSchema.Fields["id"] = new FieldSchema("id", FieldType.String) { IsRequired = true };
            behaviorSchema.Fields["nodes"] = new FieldSchema("nodes", FieldType.Array) { IsRequired = true, MaxArraySize = 1000 };
            behaviorSchema.Fields["edges"] = new FieldSchema("edges", FieldType.Array) { IsRequired = true, MaxArraySize = 5000 };
            RegisterSchema(ModType.BehaviorGraph, behaviorSchema);

            // Procedural schema
            var procSchema = new ModSchema("Procedural", ModType.Procedural);
            procSchema.Fields["seed"] = new FieldSchema("seed", FieldType.Number) { IsRequired = true };
            procSchema.Fields["parameters"] = new FieldSchema("parameters", FieldType.Object) { IsRequired = true };
            RegisterSchema(ModType.Procedural, procSchema);
        }
    }

    /// <summary>
    /// Result of mod package validation.
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; } = true;
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public ModManifest? Manifest { get; set; }

        public void AddError(string error)
        {
            IsValid = false;
            Errors.Add(error);
        }

        public void AddWarning(string warning)
        {
            Warnings.Add(warning);
        }
    }
}
