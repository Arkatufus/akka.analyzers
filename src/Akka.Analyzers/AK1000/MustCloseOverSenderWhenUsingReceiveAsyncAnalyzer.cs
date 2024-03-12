// -----------------------------------------------------------------------
//  <copyright file="MustCloseOverSenderWhenUsingReceiveAsyncAnalyzer.cs" company="Akka.NET Project">
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
public class MustCloseOverSenderWhenUsingReceiveAsyncAnalyzer()
    : AkkaDiagnosticAnalyzer(RuleDescriptors.Ak1005MustCloseOverSenderWhenUsingReceiveAsync)
{
    public override void AnalyzeCompilation(CompilationStartAnalysisContext context, AkkaContext akkaContext)
    {
        Guard.AssertIsNotNull(context);
        Guard.AssertIsNotNull(akkaContext);

        context.RegisterSyntaxNodeAction(ctx =>
        {
            var memberAccessExpr = (MemberAccessExpressionSyntax)ctx.Node;
            var semanticModel = ctx.SemanticModel;
            
            // Check if it is a `Sender` property access
            if(!memberAccessExpr.IsAccessingActorSenderProperty(semanticModel, akkaContext.AkkaCore))
                return;

            // Check if it's a ReceiveAsync<T>() or ReceiveAnyAsync() method call
            if (!invocationExpr.IsReceiveAsyncInvocation(semanticModel, akkaContext.AkkaCore))
                return;
            
            // Check if 'this.Sender' is used in the arguments
            foreach (var arg in invocationExpr.ArgumentList.Arguments)
            {
                var symbol = ModelExtensions.GetSymbolInfo(ctx.SemanticModel, arg.Expression).Symbol;
                if (IsThisSenderSymbol(symbol, akkaContext))
                {
                    var diagnostic = Diagnostic.Create(RuleDescriptors.Ak1001CloseOverSenderUsingPipeTo,
                        memberAccessExpr.Name.GetLocation());
                    ctx.ReportDiagnostic(diagnostic);
                    break; // Report only once per invocation
                }
            }
        }, SyntaxKind.SimpleMemberAccessExpression);
    }
}