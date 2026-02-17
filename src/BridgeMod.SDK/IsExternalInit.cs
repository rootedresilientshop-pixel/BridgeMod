// IsExternalInit.cs — C# 9+ record/init polyfill for netstandard2.1
// ============================================================
// Copyright (c) 2024 DreamCraft: Legacies Project
// License: MIT
//
// The C# compiler requires this type when targeting netstandard2.1 and using
// C# 9+ `record` types or `init`-only properties. It is only compiled into
// the netstandard2.1 target — .NET 5+ already includes it in the BCL.

#if NETSTANDARD2_1
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Enables C# 9+ <c>record</c> types and <c>init</c>-only property setters
    /// on netstandard2.1 targets. This class has no runtime members; it is a
    /// compiler-only marker type that the C# compiler checks for at build time.
    /// </summary>
    internal static class IsExternalInit { }
}
#endif
