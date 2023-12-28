﻿// -----------------------------------------------------------------------
//  <copyright file="AkkaVerifier.cs" company="Akka.NET Project">
//      Copyright (C) 2015-2023 .NET Petabridge, LLC
//  </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Akka.Analyzers.Tests.Utility;

[SuppressMessage("Design", "CA1000:Do not declare static members on generic types")]
public sealed class AkkaVerifier<TAnalyzer> where TAnalyzer : DiagnosticAnalyzer, new()
{
    /// <summary>
    /// Creates a diagnostic result for the diagnostic referenced in <see cref="TAnalyzer"/>.
    /// </summary>
    public static DiagnosticResult Diagnostic() =>
        CSharpCodeFixVerifier<TAnalyzer, EmptyCodeFixProvider, DefaultVerifier>.Diagnostic();

    public static Task VerifyAnalyzer(string source, params DiagnosticResult[] diagnostics)
    {
        return VerifyAnalyzer(new[] { source }, diagnostics);
    }

    public static Task VerifyAnalyzer(string[] sources, params DiagnosticResult[] diagnostics)
    {
        Guard.AssertIsNotNull(sources);

        var test = new AkkaTest();
#pragma warning disable CA1062
        foreach (var source in sources)
#pragma warning restore CA1062
            test.TestState.Sources.Add(source);

        test.ExpectedDiagnostics.AddRange(diagnostics);
        return test.RunAsync();
    }

    private sealed class AkkaTest() : TestBase(ReferenceAssembliesHelper.CurrentAkka);

    private class TestBase : CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
    {
        protected TestBase(ReferenceAssemblies referenceAssemblies)
        {
            ReferenceAssemblies = referenceAssemblies;

            // Diagnostics are reported in both normal and generated code
            TestBehaviors |= TestBehaviors.SkipGeneratedCodeCheck;

            // Tests that check for messages should run independent of current system culture.
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
        }
    }
}