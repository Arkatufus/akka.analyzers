// -----------------------------------------------------------------------
//  <copyright file="AkkaPersistenceContext.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2024 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Analyzers.Context.Core;
using Microsoft.CodeAnalysis;

namespace Akka.Analyzers.Context.Persistence;

public interface IAkkaPersistenceContext
{
    Version Version { get; }
    
    INamedTypeSymbol? PersistenceType { get; }
    INamedTypeSymbol? EventsourcedType { get; }
    
    IEventsourcedContext Eventsourced { get; }
}

public sealed class EmptyPersistenceContext : IAkkaPersistenceContext
{
    public static EmptyPersistenceContext Instance => new();

    private EmptyPersistenceContext()
    {
    }

    public Version Version => new();
    public INamedTypeSymbol? PersistenceType => null;
    public INamedTypeSymbol? EventsourcedType => null;
    public IEventsourcedContext Eventsourced => EmptyEventsourcedContext.Instance;
}

public class AkkaPersistenceContext: IAkkaPersistenceContext
{
    public const string PersistenceNamespace = AkkaCoreContext.AkkaNamespace + ".Persistence";
    
    private readonly Lazy<INamedTypeSymbol?> _lazyPersistenceType;
    private readonly Lazy<INamedTypeSymbol?> _lazyEventsourcedType;
    
    private AkkaPersistenceContext(Compilation compilation, Version version)
    {
        Version = version;
        _lazyPersistenceType = new Lazy<INamedTypeSymbol?>(() => compilation.GetTypeByMetadataName($"{PersistenceNamespace}.Persistence"));
        _lazyEventsourcedType = new Lazy<INamedTypeSymbol?>(() => compilation.GetTypeByMetadataName($"{PersistenceNamespace}.Eventsourced"));
        Eventsourced = EventsourcedContext.Get(this);
    }
    
    public static IAkkaPersistenceContext Get(Compilation compilation, Version? versionOverride = null)
    {
        // assert that compilation is not null
        Guard.AssertIsNotNull(compilation);

        var version = versionOverride ?? compilation
              .ReferencedAssemblyNames
              .FirstOrDefault(a => a.Name.Equals(PersistenceNamespace, StringComparison.OrdinalIgnoreCase))?
              .Version;

        return version is null ? EmptyPersistenceContext.Instance : new AkkaPersistenceContext(compilation, version);
    }
    
    public Version Version { get; }
    public INamedTypeSymbol? PersistenceType => _lazyPersistenceType.Value;
    public INamedTypeSymbol? EventsourcedType => _lazyEventsourcedType.Value;
    public IEventsourcedContext Eventsourced { get; }
}