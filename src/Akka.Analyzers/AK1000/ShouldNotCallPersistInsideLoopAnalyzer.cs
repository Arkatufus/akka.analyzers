﻿// -----------------------------------------------------------------------
//  <copyright file="ShouldNotCallPersistInsideLoop.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2024 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Analyzers.Context;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Akka.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ShouldNotCallPersistInsideLoopAnalyzer(): AkkaDiagnosticAnalyzer(RuleDescriptors.Ak1006ShouldNotUsePersistInsideLoop)
{
    public override void AnalyzeCompilation(CompilationStartAnalysisContext context, AkkaContext akkaContext)
    {
        Guard.AssertIsNotNull(context);
        Guard.AssertIsNotNull(akkaContext);
        
        context.RegisterSyntaxNodeAction(ctx =>
        {
            // No need to check if Akka.Persistence is not installed
            if (!akkaContext.HasAkkaPersistenceInstalled)
                return;
            
            var invocationExpression = (InvocationExpressionSyntax)ctx.Node;
            var semanticModel = ctx.SemanticModel;
            
            // Get the member symbol from the invocation expression
            if(semanticModel.GetSymbolInfo(invocationExpression.Expression).Symbol is not IMethodSymbol methodInvocationSymbol)
                return;
        
            var persistenceContext = akkaContext.AkkaPersistence;
            
            // Check if the method name is `Persist` or `PersistAsync`
            var eventsourcedContext = persistenceContext.Eventsourced;
            var refMethods = eventsourcedContext.Persist.AddRange(eventsourcedContext.PersistAsync);
            if (!methodInvocationSymbol.MatchesAny(refMethods))
                return;

            // Traverse up the parent nodes to see if any of them are loops
            var parent = invocationExpression.Parent;
            while (parent != null)
            {
                if (parent is ForStatementSyntax or WhileStatementSyntax or DoStatementSyntax or ForEachStatementSyntax)
                {
                    // If found, report a diagnostic
                    var methodName = methodInvocationSymbol.Name;
                    var replacementName = methodName.EndsWith("Async", StringComparison.InvariantCulture) 
                        ? "PersistAllAsync" : "PersistAll";
                    var diagnostic = Diagnostic.Create(
                        descriptor: RuleDescriptors.Ak1006ShouldNotUsePersistInsideLoop, 
                        location: invocationExpression.GetLocation(),
                        methodName,
                        replacementName);
                    ctx.ReportDiagnostic(diagnostic);
                }
                parent = parent.Parent;
            }
        }, SyntaxKind.InvocationExpression);
    }
}