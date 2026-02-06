# Contributing to BridgeMod

We're working toward a world where developers feel confident and modders feel respected. Your contributions help bridge that gap.

## Our Philosophy

BridgeMod exists because we believe:

- **Developer confidence matters.** Developers should control exactly what modders can change—not through restrictive tools, but through clear, intentional design.
- **Modders deserve clarity.** If a mod surface is closed, modders should know why. If it's open, they should know what to expect.
- **Consoles and PC can share.** Safety and transparency shouldn't be features for indie devs—they should be the foundation.

These principles are documented in [CONSTITUTION.md](CONSTITUTION.md). Everything we build should reinforce them.

## Getting Started

### For Small Fixes (docs, typos, clarity)
1. Fork the repository
2. Create a branch: `git checkout -b fix/your-description`
3. Make your changes
4. Open a pull request with a clear description

### For Feature Work

If you're thinking about adding or changing something significant:

1. **Check the roadmap** in [CROWDFUNDING.md](CROWDFUNDING.md) and [console_modding_execution_plan.md](console_modding_execution_plan.md)
2. **Read [CONSTITUTION.md](CONSTITUTION.md)**—does your idea align with our principles?
3. **Open an issue first** and describe what you'd like to explore
4. **Let's discuss** before you invest time in code

This isn't gatekeeping—it's respect. We want your contribution to matter.

## Code Guidelines

### What We Value

- **Clarity over cleverness** - Code should be readable to developers integrating BridgeMod
- **Tests over trust** - New functionality should include tests; see [Phase1Tests.cs](tests/Phase1Tests.cs) for patterns
- **Documentation over assumptions** - If you add code, future developers will read it without you. Make their job easier.
- **Safety by default** - Mods are untrusted. Think like you're processing user input.

### Practical Standards

1. **Follow existing patterns** - Look at [ModValidator.cs](sdk/Runtime/ModValidator.cs) and [ModLoader.cs](sdk/Runtime/ModLoader.cs) for structure
2. **Add XML documentation** to public APIs:
   ```csharp
   /// <summary>
   /// Validates a mod package for integrity and schema compliance.
   /// </summary>
   public ValidationResult Validate(string modPath)
   ```
3. **Write tests** - Aim for the coverage we already have in Phase1Tests.cs
4. **No breaking changes without discussion** - Public API stability matters

### Build & Test Before Pushing

```bash
# Build locally
dotnet build

# Run tests
dotnet test

# Check your code follows patterns
# (we'll add StyleCop later)
```

## Pull Request Process

1. **Update documentation** if your change affects how developers use BridgeMod
2. **Reference any related issues** - e.g., "Closes #123"
3. **Write a clear PR description:**
   - What problem does this solve?
   - How does it respect the Constitution?
   - What testing did you do?

Example:
```
## What
Adds optional cloud validation service configuration (Phase 5 prep)

## Why
Developers planning to enable cloud validation need a way to configure endpoints.
Respects Constitution: optional, local validation still mandatory.

## Testing
- Added 5 test cases in Phase1Tests.cs
- Verified backward compatibility
- All tests pass locally
```

## Documentation

- **For developers integrating BridgeMod:** Update [README_DEVELOPMENT.md](README_DEVELOPMENT.md)
- **For modders:** Update [MOD_SCHEMA.md](MOD_SCHEMA.md)
- **For architecture:** Update [console_modding_execution_plan.md](console_modding_execution_plan.md)
- **For getting started:** Update [QUICKSTART.md](QUICKSTART.md) (when it exists)

## Reporting Issues

Found a bug? Security concern? Let us know.

- **Bug reports:** Include steps to reproduce and expected vs. actual behavior
- **Security issues:** Please email or use GitHub security advisories (not public issues)
- **Feature requests:** Describe the use case, not just the solution

## Questions?

We're learning too. Open an issue and ask—or look at how existing code handles similar problems.

## License

By contributing, you agree your work is licensed under the MIT License. See [LICENSE](LICENSE).

---

**Thank you for helping us bridge the gap.** Whether you're fixing a typo or implementing a new validation strategy, you're part of something that helps developers and modders understand each other better.

We appreciate it more than code can express.
