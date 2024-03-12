// -----------------------------------------------------------------------
//  <copyright file="MustCloseOverSenderWhenUsingReceiveAsyncAnalyzer.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2024 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;
using System.Linq.Expressions;
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
            var invocationExpr = (InvocationExpressionSyntax)ctx.Node;
            var semanticModel = ctx.SemanticModel;
            var akkaCore = akkaContext.AkkaCore;
            
            // We're only interested in ReceiveAsync<T>() and ReceiveAnyAsync() invocation
            if (!invocationExpr.IsReceiveAsyncInvocation(semanticModel, akkaCore))
                return;
            
            // Get the lambda argument expression
            var lambdaExpression = invocationExpr.ArgumentList.Arguments
                .Where(arg =>
                {
                    if (arg.Expression is not LambdaExpressionSyntax lambdaExpr)
                        return false;

                    // Detect the argument that conforms to `Func<T, Task>` pattern
                    var typeInfo = semanticModel.GetTypeInfo(lambdaExpr);
                    return typeInfo.ConvertedType is INamedTypeSymbol { 
                        DelegateInvokeMethod: { 
                            ReturnType: INamedTypeSymbol { Name: "Task" },
                            Parameters.Length: 1
                        }
                    };
                }).FirstOrDefault();
            if(lambdaExpression is null)
                return;
            
            // Find any "Sender" declaration inside the lambda function and it is not a variable initializer
            var senders = lambdaExpression.DescendantNodes().OfType<IdentifierNameSyntax>();
            foreach (var sender in senders)
            {
                if (!sender.IsActorSenderIdentifier(semanticModel, akkaCore) ||
                    sender.Parent?.Parent is VariableDeclaratorSyntax) 
                    continue;
                
                var diagnostic = Diagnostic.Create(RuleDescriptors.Ak1005MustCloseOverSenderWhenUsingReceiveAsync,
                    sender.GetLocation());
                ctx.ReportDiagnostic(diagnostic);
                break; // Report only once per invocation
            }
        }, SyntaxKind.InvocationExpression);
    }
}