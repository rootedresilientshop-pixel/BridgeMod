# BridgeMod Project Constitution

## Purpose
This document defines the guiding principles, scope boundaries, and values for the BridgeMod platform. It is the authoritative reference for developers, modders, and Claude Code automation.

## Core Principles
1. **Developer-First Authority**: Developers decide what mod surfaces exist. BridgeMod enforces boundaries but never overrides developer limits.
2. **Platform Transparency**: Mod surfaces are clearly declared, visible, and comparable. Community pressure is social, not enforced.
3. **Safety by Design**: Mods are treated as untrusted data. Runtime execution is sandboxed and deterministic.
4. **Hybrid Validation**: Local validation is mandatory; optional cloud validation provides advisory checks and metadata enrichment.
5. **Failure Containment**: Errors in mods are isolated; the game remains stable at all times.
6. **Anonymous Mod Identity**: v1 mods are file-based with no accounts required; identity and accounts are deferred to future phases.

## v1 Scope
- Pure Data Mods ✅
- Behavior Definition Graphs ✅
- Procedural Control ✅
- Asset Replacement Mods ❌ (Deferred)
- Sandbox Scripting ❌ (Deferred)
- Player-Facing Mod Browser ❌ (Deferred)

## Expansion Philosophy
- Add features incrementally (v2/v3)
- Respect developer trust first
- Empower modder creativity within safe bounds

## Enforcement
- Claude Code or contributors must adhere to these principles
- No runtime behavior should violate developer authority
- Transparency must be maintained for modders
