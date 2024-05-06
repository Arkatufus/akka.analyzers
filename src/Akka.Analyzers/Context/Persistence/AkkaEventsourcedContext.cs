// -----------------------------------------------------------------------
//  <copyright file="AkkaEventsourcedContext.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2024 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Akka.Analyzers.Context.Persistence;

public interface IEventsourcedContext
{
    #region Methods

    public ImmutableArray<IMethodSymbol> Persist { get; }
    public ImmutableArray<IMethodSymbol> PersistAsync { get; }
    public ImmutableArray<IMethodSymbol> PersistAll { get; }
    public ImmutableArray<IMethodSymbol> PersistAllAsync { get; }

    #endregion
}

public sealed class EmptyEventsourcedContext : IEventsourcedContext
{
    public static EmptyEventsourcedContext Instance => new();

    private EmptyEventsourcedContext() { }

    public ImmutableArray<IMethodSymbol> Persist => ImmutableArray<IMethodSymbol>.Empty;
    public ImmutableArray<IMethodSymbol> PersistAsync => ImmutableArray<IMethodSymbol>.Empty;
    public ImmutableArray<IMethodSymbol> PersistAll => ImmutableArray<IMethodSymbol>.Empty;
    public ImmutableArray<IMethodSymbol> PersistAllAsync => ImmutableArray<IMethodSymbol>.Empty;
}

public class EventsourcedContext: IEventsourcedContext
{
    private readonly Lazy<ImmutableArray<IMethodSymbol>> _lazyPersist;
    private readonly Lazy<ImmutableArray<IMethodSymbol>> _lazyPersistAsync;
    private readonly Lazy<ImmutableArray<IMethodSymbol>> _lazyPersistAll;
    private readonly Lazy<ImmutableArray<IMethodSymbol>> _lazyPersistAllAsync;

    private EventsourcedContext(AkkaPersistenceContext context)
    {
        Guard.AssertIsNotNull(context.EventsourcedType);
        
        _lazyPersist = new Lazy<ImmutableArray<IMethodSymbol>>(() => context.EventsourcedType!
            .GetMembers(nameof(Persist))
            .Select(m => (IMethodSymbol)m).ToImmutableArray());
        _lazyPersistAsync = new Lazy<ImmutableArray<IMethodSymbol>>(() => context.EventsourcedType!
            .GetMembers(nameof(PersistAsync))
            .Select(m => (IMethodSymbol)m).ToImmutableArray());
        _lazyPersistAll = new Lazy<ImmutableArray<IMethodSymbol>>(() => context.EventsourcedType!
            .GetMembers(nameof(PersistAll))
            .Select(m => (IMethodSymbol)m).ToImmutableArray());
        _lazyPersistAllAsync = new Lazy<ImmutableArray<IMethodSymbol>>(() => context.EventsourcedType!
            .GetMembers(nameof(PersistAllAsync))
            .Select(m => (IMethodSymbol)m).ToImmutableArray());
    }

    public ImmutableArray<IMethodSymbol> Persist => _lazyPersist.Value;
    public ImmutableArray<IMethodSymbol> PersistAsync => _lazyPersistAsync.Value;
    public ImmutableArray<IMethodSymbol> PersistAll => _lazyPersistAll.Value;
    public ImmutableArray<IMethodSymbol> PersistAllAsync => _lazyPersistAllAsync.Value;

    public static EventsourcedContext Get(AkkaPersistenceContext context)
    {
        Guard.AssertIsNotNull(context);
        return new EventsourcedContext(context);
    }
}