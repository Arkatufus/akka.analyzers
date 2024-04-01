// -----------------------------------------------------------------------
//  <copyright file="MustCloseOverSenderWhenUsingReceiveAsyncAnalyzer.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2024 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Analyzers.Context;
using Akka.Analyzers.Context.Core.Actor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Akka.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class MustCloseOverSenderWhenUsedInsideLambdaArgumentAnalyzer()
    : AkkaDiagnosticAnalyzer(RuleDescriptors.Ak1005MustCloseOverSenderWhenUsedInsideLambdaArgument)
{
    public override void AnalyzeCompilation(CompilationStartAnalysisContext context, AkkaContext akkaContext)
    {
        Guard.AssertIsNotNull(context);
        Guard.AssertIsNotNull(akkaContext);

        context.RegisterSyntaxNodeAction(ctx =>
        {
            var classDeclaration = (ClassDeclarationSyntax)ctx.Node;
            var semanticModel = ctx.SemanticModel;
            var akkaActor = akkaContext.AkkaCore.Actor;

            // The class declaration must implements or inherits `Akka.Actor.ActorBase`
            if (!classDeclaration.IsDerivedOrImplements(semanticModel, akkaActor.ActorBaseType!))
                return;
            
            var visitor = new AsyncLambdaArgumentsVisitor(semanticModel, akkaContext, ctx.CancellationToken);
            var diagnostics = visitor.Visit(classDeclaration);
            if(diagnostics is null)
                return;

            foreach (var diagnostic in diagnostics)
            {
                ctx.ReportDiagnostic(diagnostic);
            }
        }, SyntaxKind.ClassDeclaration);
    }
    
    // Visitor that reports all async lambda arguments and local function declarations
    private sealed class AsyncLambdaArgumentsVisitor : CSharpSyntaxVisitor<List<Diagnostic>>
    {
        private readonly CancellationToken _cancellationToken;
        private readonly SemanticModel _semanticModel;
        private readonly BlockVisitor _blockVisitor;
        private readonly AkkaContext _akkaContext;

        public AsyncLambdaArgumentsVisitor(
            SemanticModel semanticModel,
            AkkaContext akkaContext,
            CancellationToken cancellationToken)
        {
            _semanticModel = semanticModel;
            _cancellationToken = cancellationToken;
            _akkaContext = akkaContext;
            _blockVisitor = new BlockVisitor(semanticModel, akkaContext.AkkaCore.Actor, cancellationToken);
        }

        public override List<Diagnostic>? Visit(SyntaxNode? node)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            
            if (node is null)
                return default;

            var result = ((CSharpSyntaxNode)node).Accept(this) ?? [];
            foreach (var syntaxNode in node.DescendantNodes())
            {
                _cancellationToken.ThrowIfCancellationRequested();
                
                var childResult = ((CSharpSyntaxNode)syntaxNode).Accept(this);
                if(childResult is not null)
                    result.AddRange(childResult);
            }

            return result;
        }

        // Checks all argument syntax node and find all of them that are:
        // 1. A lambda method expression
        // 2. Is an argument of an async method
        public override List<Diagnostic>? VisitArgument(ArgumentSyntax node)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            
            // The argument type must be a lambda expression
            if (node.Expression is not LambdaExpressionSyntax lambdaExpression)
                return default;

            // The argument must be an argument of a method invocation
            var invocationExpression = node.Ancestors().OfType<InvocationExpressionSyntax>().FirstOrDefault();
            if (invocationExpression is null)
                return default;

            // Get the method symbol of the invocation
            if (_semanticModel.GetSymbolInfo(invocationExpression).Symbol is not IMethodSymbol methodSymbol)
                return default;
            
            // Skip lambda arguments passed into `ReceiveAsync` and `ReceiveAnyAsync`, these are fine.
            if (methodSymbol.IsReceiveAsyncInvocation(_akkaContext.AkkaCore))
                return default;
            
            // Fast path, the method have an async modifier
            if (!methodSymbol.IsAsync)
            {
                // Slow path, check that the method returns a `Task`
                var returnType = methodSymbol.ReturnType;
                
                // Unroll the return type to make sure that the return type is its base type
                while (!ReferenceEquals(returnType, returnType.OriginalDefinition))
                    returnType = returnType.OriginalDefinition;
                
                // The return type must be of type `Task` (async methods)
                if (!SymbolEqualityComparer.Default.Equals(returnType, _akkaContext.SystemThreadingTasks.TaskType))
                    return default;
            }
            
            // We found a candidate lambda method argument, pass its content to the next visitor.
            return _blockVisitor.Visit(lambdaExpression.Body);
        }
    }
    
    private sealed class BlockVisitor: CSharpSyntaxVisitor<List<Diagnostic>>
    {
        private readonly CancellationToken _cancellationToken;
        private readonly SemanticModel _semanticModel;
        private readonly IAkkaCoreActorContext _actorContext;

        public BlockVisitor(
            SemanticModel semanticModel,
            IAkkaCoreActorContext actorContext,
            CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
            _semanticModel = semanticModel;
            _actorContext = actorContext;
        }

        public override List<Diagnostic>? Visit(SyntaxNode? node)
        {
            _cancellationToken.ThrowIfCancellationRequested();

            if (node is null)
                return default;

            var result = ((CSharpSyntaxNode)node).Accept(this) ?? [];
            foreach (var syntaxNode in node.DescendantNodes())
            {
                var childResult = ((CSharpSyntaxNode)syntaxNode).Accept(this);
                if(childResult is not null)
                    result.AddRange(childResult);
            }

            return result;
        }

        public override List<Diagnostic>? VisitAssignmentExpression(AssignmentExpressionSyntax node)
        {
            var diagnostic = AssertIsSelfOrSender(node.Right);
            return diagnostic is null ? default : [ diagnostic ];
        }

        public override List<Diagnostic> VisitInitializerExpression(InitializerExpressionSyntax node)
        {
            var result = new List<Diagnostic>();
            foreach (var expression in node.Expressions)
            {
                var diagnostic = AssertIsSelfOrSender(expression);
                if(diagnostic is not null)
                    result.Add(diagnostic);
            }
            return result;
        }

        public override List<Diagnostic>? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (node.Expression is not MemberAccessExpressionSyntax memberAccess)
                return default;

            var diagnostic = AssertIsSelfOrSender(memberAccess.Expression);
            return diagnostic is null ? default : [ diagnostic ];
        }

        public override List<Diagnostic>? VisitArgument(ArgumentSyntax node)
        {
            var diagnostic = AssertIsSelfOrSender(node.Expression);
            return diagnostic is null ? default : [ diagnostic ];
        }

        private Diagnostic? AssertIsSelfOrSender(ExpressionSyntax expression)
        {
            switch (expression)
            {
                case IdentifierNameSyntax identifier:
                {
                    // Make sure that identifier is a property
                    if (_semanticModel.GetSymbolInfo(identifier).Symbol is not IPropertySymbol propertySymbol)
                        return default;

                    // Property is equal to `ActorBase.Self` or `ActorBase.Sender`
                    if (ReferenceEquals(propertySymbol, _actorContext.ActorBase.Self!) ||
                        ReferenceEquals(propertySymbol, _actorContext.ActorBase.Sender!))
                    {
                        return Diagnostic.Create(
                            RuleDescriptors.Ak1005MustCloseOverSenderWhenUsedInsideLambdaArgument,
                            identifier.GetLocation(),
                            propertySymbol.Name);
                    }
                    
                    return default;
                }
                
                case MemberAccessExpressionSyntax actorContextMemberAccess:
                {
                    // Make sure that member access is a property
                    if (_semanticModel.GetSymbolInfo(actorContextMemberAccess).Symbol is not IPropertySymbol propertySymbol)
                        return default;

                    // Property is equal to `IActorContext.Self` or `IActorContext.Sender`
                    if (ReferenceEquals(propertySymbol, _actorContext.IActorContext.Self!) ||
                        ReferenceEquals(propertySymbol, _actorContext.IActorContext.Sender!))
                    {
                        return Diagnostic.Create(
                            RuleDescriptors.Ak1005MustCloseOverSenderWhenUsedInsideLambdaArgument,
                            actorContextMemberAccess.GetLocation(),
                            propertySymbol.Name);
                    }
                    
                    return default;
                }
                
                default:
                    return default;
            }
        }
    }
}