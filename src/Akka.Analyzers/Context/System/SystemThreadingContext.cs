// -----------------------------------------------------------------------
//  <copyright file="SystemTask.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2024 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

namespace Akka.Analyzers.Context.System;

public interface ISystemThreadingTasksContext
{
    public INamedTypeSymbol? TaskType { get; }
}

public sealed class SystemThreadingTasksContext: ISystemThreadingTasksContext
{
    private readonly Lazy<INamedTypeSymbol?> _lazyTaskTypes;
    
    private SystemThreadingTasksContext(Compilation compilation)
    {
        Guard.AssertIsNotNull(compilation);

        _lazyTaskTypes = new Lazy<INamedTypeSymbol?>(() =>
        {
            var type = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
            return type ?? throw new InvalidOperationException(
                "The type `System.Threading.Tasks.Task` does not exist, this target framework platform is not supported.");
        });
    }

    public INamedTypeSymbol? TaskType => _lazyTaskTypes.Value;

    public static SystemThreadingTasksContext Get(Compilation compilation)
        => new(compilation);
}