# BridgeMod v1 Mod Package Schema

## Purpose
Defines the structure and validation rules for BridgeMod-compatible mod packages.

## File Layout
```
ModPackage.zip
 ├─ manifest.json
 ├─ data/
 │   └─ *.json, *.csv
 ├─ graphs/
 │   └─ *.graph.json
 └─ procedural/
     └─ *.json
```

## Manifest Fields
- `name` (string) - Unique mod name
- `version` (string) - Semantic versioning
- `author` (string)
- `description` (string)
- `dependencies` (optional array)
- `modType` (enum: data, behaviorGraph, procedural)

## Validation Rules
- All JSON must match declared schemas
- Behavior graphs must not exceed node/depth limits
- Procedural parameters must stay within declared bounds
- Manifest must declare all included files

## Optional Cloud Metadata
- `compatibilityNotes`
- `recommendedVersion`
- `tags` for discovery
