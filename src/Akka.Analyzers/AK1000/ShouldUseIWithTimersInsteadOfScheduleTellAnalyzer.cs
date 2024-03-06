// -----------------------------------------------------------------------
//  <copyright file="ShouldUseIWithTimerInsteadOfITellScheduler.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2024 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Akka.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ShouldUseIWithTimersInsteadOfScheduleTellAnalyzer(): AkkaDiagnosticAnalyzer(RuleDescriptors.Ak1004ShouldUseIWithTimersInsteadOfScheduleTell)
{
    public override void AnalyzeCompilation(CompilationStartAnalysisContext context, AkkaContext akkaContext)
    {
        Guard.AssertIsNotNull(context);
        Guard.AssertIsNotNull(akkaContext);
        
        context.RegisterSyntaxNodeAction(ctx =>
        {
            var invocationExpr = (InvocationExpressionSyntax)ctx.Node;
            
            // invocation must be a member access expression
            if (invocationExpr.Expression is not MemberAccessExpressionSyntax memberAccessExpr)
                return;
            
            var semanticModel = ctx.SemanticModel;
            // Get the member symbol from the invocation expression
            var memberSymbol = semanticModel.GetSymbolInfo(memberAccessExpr).Symbol;
            
            // Check if the method name is `ScheduleTellOnce` or `ScheduleTellRepeatedly`
            if (memberSymbol is null || (memberSymbol.Name is not "ScheduleTellOnce" && memberSymbol.Name is not "ScheduleTellRepeatedly"))
                return;
            
            var classDeclaration = invocationExpr.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (classDeclaration is null)
                return;
            
            var classBase = semanticModel.GetDeclaredSymbol(classDeclaration)?.BaseType;

            var coreContext = akkaContext.AkkaCore;
            // Check that the class declaration inherits from ActorBase
            if (classBase is null || !classBase.IsDerivedOrImplements(coreContext.Actor.ActorBaseType!))
                return;
            
            // Member access is accessing something that needs to derive from ITellScheduler
            var symbol = semanticModel.GetSymbolInfo(memberAccessExpr.Expression).Symbol;
            var type = symbol switch
            {
                IPropertySymbol p => p.Type,
                IFieldSymbol f => f.Type,
                ILocalSymbol l => l.Type,
                IParameterSymbol p => p.Type,
                IMethodSymbol m => m.ReturnType,
                _ => null
            };
            
            if(type is null || type.AllInterfaces.All(i => 
                   !SymbolEqualityComparer.Default.Equals(i, coreContext.Actor.TellSchedulerInterface)))
                return;
            
            ReportDiagnostic();
            return;
            
            void ReportDiagnostic()
            {
                var diagnostic = Diagnostic.Create(
                    descriptor: RuleDescriptors.Ak1004ShouldUseIWithTimersInsteadOfScheduleTell, 
                    location: invocationExpr.GetLocation(), 
                    "ScheduleTell invocation");
                ctx.ReportDiagnostic(diagnostic);
            }
        }, SyntaxKind.InvocationExpression);
    }
}