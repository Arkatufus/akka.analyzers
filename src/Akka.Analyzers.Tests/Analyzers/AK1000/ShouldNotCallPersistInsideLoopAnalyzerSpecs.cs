// -----------------------------------------------------------------------
//  <copyright file="ShouldNotCallPersistInsideLoopAnalyzerSpecs.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2024 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Verify = Akka.Analyzers.Tests.Utility.AkkaVerifier<Akka.Analyzers.ShouldNotCallPersistInsideLoopAnalyzer>;

namespace Akka.Analyzers.Tests.Analyzers.AK1000;

public class ShouldNotCallPersistInsideLoopAnalyzerSpecs
{
    public static readonly TheoryData<string> SuccessCases = new()
    {
        // ReceivePersistenceActor calling Persist and PersistAsync methods outside of loop
"""
// 01
using Akka.Persistence;
using System.Linq;

public class MyActor: ReceivePersistentActor
{
    public override string PersistenceId { get; }
    public MyActor(string persistenceId)
    {
        PersistenceId = persistenceId;
        CommandAny(obj =>
        {
            foreach (var _ in Enumerable.Range(0, 10))
            {
                // Inconsequential loop
            }
            
            Persist(obj, o => {});
            PersistAsync(obj, o => {});
            PersistAll( new[]{ obj }, o => {});
            PersistAllAsync( new[]{ obj }, o => {});
        });
    }
}
""",

    // UntypedPersistenceActor calling Persist and PersistAsync methods outside of loop
"""
// 02
using Akka.Persistence;
using System.Linq;

public class MyActor: UntypedPersistentActor
{
    public override string PersistenceId { get; }
    public MyActor(string persistenceId)
    {
        PersistenceId = persistenceId;
    }

    protected override void OnCommand(object obj)
    {
        foreach (var _ in Enumerable.Range(0, 10))
        {
            // Inconsequential loop
        }
        
        Persist(obj, o => {});
        PersistAsync(obj, o => {});
        PersistAll( new[]{ obj }, o => {});
        PersistAllAsync( new[]{ obj }, o => {});
    }

    protected override void OnRecover(object message) { }
}
""",

        // ReceivePersistenceActor without Persist and PersistAsync methods calls
"""
// 03
using Akka.Persistence;

public class MyActor: ReceivePersistentActor
{
    public override string PersistenceId { get; }
    public MyActor(string persistenceId)
    {
        PersistenceId = persistenceId;
    }
}
""",

        // UntypedPersistenceActor without Persist and PersistAsync methods calls
"""
// 04
using Akka.Persistence;

public class MyActor: UntypedPersistentActor
{
    public override string PersistenceId { get; }
    public MyActor(string persistenceId)
    {
        PersistenceId = persistenceId;
    }

    protected override void OnCommand(object message) { }
    protected override void OnRecover(object message) { }
}
""",

        // Non-Actor class that implements methods that have the same methods fingerprints, we're not responsible for this.
"""
// 05
using System;
using System.Linq;

public class MyNonActor
{
    public string PersistenceId { get; }
    
    public MyNonActor(string persistenceId)
    {
        PersistenceId = persistenceId;
    }

    protected void OnCommand(object message)
    {
        foreach (var i in Enumerable.Range(0, 10))
        {
            Persist(i, o => {});
            PersistAsync(i, o => {});
        }
    }

    public void Persist<TEvent>(TEvent @event, Action<TEvent> handler) { }
    public void PersistAsync<TEvent>(TEvent @event, Action<TEvent> handler) { }
}
""",
    };

    public static readonly
        TheoryData<(string testData, (int startLine, int startColumn, int endLine, int endColumn) spanData, object[] arguments)>
        FailureCases = new()
        {
            // ReceivePersistentActor calling Persist inside a foreach
            (
"""
// 01
using Akka.Persistence;
using System.Linq;

public class MyActor: ReceivePersistentActor
{
    public override string PersistenceId { get; }
    public MyActor(string persistenceId)
    {
        PersistenceId = persistenceId;
        CommandAny(obj =>
        {
            foreach (var i in Enumerable.Range(0, 10))
            {
                Persist(i, o => {});
            }
        });
    }
}
""", (15, 17, 15, 36), ["Persist", "PersistAll"]),
            
            // ReceivePersistentActor calling Persist inside a for loop
            (
"""
// 02
using Akka.Persistence;

public class MyActor: ReceivePersistentActor
{
    public override string PersistenceId { get; }
    public MyActor(string persistenceId)
    {
        PersistenceId = persistenceId;
        CommandAny(obj =>
        {
            for (var i=0; i<10; i++)
            {
                Persist(i, o => {});
            }
        });
    }
}
""", (14, 17, 14, 36), ["Persist", "PersistAll"]),
            
            // ReceivePersistentActor calling Persist inside a while loop
            (
"""
// 03
using Akka.Persistence;

public class MyActor: ReceivePersistentActor
{
    public override string PersistenceId { get; }
    public MyActor(string persistenceId)
    {
        PersistenceId = persistenceId;
        CommandAny(obj =>
        {
            var i=0;
            while(i<10)
            {
                Persist(i, o => {});
                i++;
            }
        });
    }
}
""", (15, 17, 15, 36), ["Persist", "PersistAll"]),
            
            // ReceivePersistentActor calling Persist inside a do loop
            (
"""
// 04
using Akka.Persistence;

public class MyActor: ReceivePersistentActor
{
    public override string PersistenceId { get; }
    public MyActor(string persistenceId)
    {
        PersistenceId = persistenceId;
        CommandAny(obj =>
        {
            var i=0;
            do
            {
                Persist(i, o => {});
                i++;
            } while(i<10);
        });
    }
}
""", (15, 17, 15, 36), ["Persist", "PersistAll"]),

            // ReceivePersistentActor calling PersistAsync inside a foreach
            (
"""
// 05
using Akka.Persistence;
using System.Linq;

public class MyActor: ReceivePersistentActor
{
    public override string PersistenceId { get; }
    public MyActor(string persistenceId)
    {
        PersistenceId = persistenceId;
        CommandAny(obj =>
        {
            foreach (var i in Enumerable.Range(0, 10))
            {
                PersistAsync(i, o => {});
            }
        });
    }
}
""", (15, 17, 15, 41), ["PersistAsync", "PersistAllAsync"]),
            
            // ReceivePersistentActor calling PersistAsync inside a for loop
            (
"""
// 06
using Akka.Persistence;

public class MyActor: ReceivePersistentActor
{
    public override string PersistenceId { get; }
    public MyActor(string persistenceId)
    {
        PersistenceId = persistenceId;
        CommandAny(obj =>
        {
            for (var i=0; i<10; i++)
            {
                PersistAsync(i, o => {});
            }
        });
    }
}
""", (14, 17, 14, 41), ["PersistAsync", "PersistAllAsync"]),
            
            // ReceivePersistentActor calling PersistAsync inside a while loop
            (
"""
// 07
using Akka.Persistence;

public class MyActor: ReceivePersistentActor
{
    public override string PersistenceId { get; }
    public MyActor(string persistenceId)
    {
        PersistenceId = persistenceId;
        CommandAny(obj =>
        {
            var i=0;
            while(i<10)
            {
                PersistAsync(i, o => {});
                i++;
            }
        });
    }
}
""", (15, 17, 15, 41), ["PersistAsync", "PersistAllAsync"]),
            
            // ReceivePersistentActor calling Persist inside a do loop
            (
"""
// 08
using Akka.Persistence;

public class MyActor: ReceivePersistentActor
{
    public override string PersistenceId { get; }
    public MyActor(string persistenceId)
    {
        PersistenceId = persistenceId;
        CommandAny(obj =>
        {
            var i=0;
            do
            {
                PersistAsync(i, o => {});
                i++;
            } while(i<10);
        });
    }
}
""", (15, 17, 15, 41), ["PersistAsync", "PersistAllAsync"]),
            
            // UntypedPersistentActor calling Persist inside a foreach
            (
"""
// 09
using Akka.Persistence;
using System.Linq;

public class MyActor: UntypedPersistentActor
{
    public override string PersistenceId { get; }
    public MyActor(string persistenceId)
    {
        PersistenceId = persistenceId;
    }

    protected override void OnCommand(object message)
    {
        foreach (var i in Enumerable.Range(0, 10))
        {
            Persist(i, o => {});
        }
    }

    protected override void OnRecover(object message) { }
}
""", (17, 13, 17, 32), ["Persist", "PersistAll"]),
            
            // UntypedPersistentActor calling Persist inside a for loop
            (
"""
// 10
using Akka.Persistence;

public class MyActor: UntypedPersistentActor
{
    public override string PersistenceId { get; }
    public MyActor(string persistenceId)
    {
        PersistenceId = persistenceId;
    }

    protected override void OnCommand(object message)
    {
        for (var i=0; i<10; i++)
        {
            Persist(i, o => {});
        }
    }

    protected override void OnRecover(object message) { }
}
""", (16, 13, 16, 32), ["Persist", "PersistAll"]),
            
            // UntypedPersistentActor calling Persist inside a while loop
            (
"""
// 11
using Akka.Persistence;

public class MyActor: UntypedPersistentActor
{
    public override string PersistenceId { get; }
    public MyActor(string persistenceId)
    {
        PersistenceId = persistenceId;
    }

    protected override void OnCommand(object message)
    {
        var i=0;
        while(i<10)
        {
            Persist(i, o => {});
            i++;
        }
    }

    protected override void OnRecover(object message) { }
}
""", (17, 13, 17, 32), ["Persist", "PersistAll"]),
            
            // UntypedPersistentActor calling Persist inside a do loop
            (
"""
// 12
using Akka.Persistence;

public class MyActor: UntypedPersistentActor
{
    public override string PersistenceId { get; }
    public MyActor(string persistenceId)
    {
        PersistenceId = persistenceId;
    }

    protected override void OnCommand(object message)
    {
        var i=0;
        do
        {
            Persist(i, o => {});
            i++;
        } while(i<10);
    }

    protected override void OnRecover(object message) { }
}
""", (17, 13, 17, 32), ["Persist", "PersistAll"]),

            // UntypedPersistentActor calling PersistAsync inside a foreach
            (
"""
// 13
using Akka.Persistence;
using System.Linq;

public class MyActor: UntypedPersistentActor
{
    public override string PersistenceId { get; }
    public MyActor(string persistenceId)
    {
        PersistenceId = persistenceId;
    }

    protected override void OnCommand(object message)
    {
        foreach (var i in Enumerable.Range(0, 10))
        {
            PersistAsync(i, o => {});
        }
    }

    protected override void OnRecover(object message) { }
}
""", (17, 13, 17, 37), ["PersistAsync", "PersistAllAsync"]),
            
            // UntypedPersistentActor calling PersistAsync inside a for loop
            (
"""
// 14
using Akka.Persistence;

public class MyActor: UntypedPersistentActor
{
    public override string PersistenceId { get; }
    public MyActor(string persistenceId)
    {
        PersistenceId = persistenceId;
    }

    protected override void OnCommand(object message)
    {
        for (var i=0; i<10; i++)
        {
            PersistAsync(i, o => {});
        }
    }

    protected override void OnRecover(object message) { }
}
""", (16, 13, 16, 37), ["PersistAsync", "PersistAllAsync"]),
            
            // UntypedPersistentActor calling PersistAsync inside a while loop
            (
"""
// 15
using Akka.Persistence;

public class MyActor: UntypedPersistentActor
{
    public override string PersistenceId { get; }
    public MyActor(string persistenceId)
    {
        PersistenceId = persistenceId;
    }

    protected override void OnCommand(object message)
    {
        var i=0;
        while(i<10)
        {
            PersistAsync(i, o => {});
            i++;
        }
    }

    protected override void OnRecover(object message) { }
}
""", (17, 13, 17, 37), ["PersistAsync", "PersistAllAsync"]),
            
            // UntypedPersistentActor calling Persist inside a do loop
            (
"""
// 16
using Akka.Persistence;

public class MyActor: UntypedPersistentActor
{
    public override string PersistenceId { get; }
    public MyActor(string persistenceId)
    {
        PersistenceId = persistenceId;
    }

    protected override void OnCommand(object message)
    {
        var i=0;
        do
        {
            PersistAsync(i, o => {});
            i++;
        } while(i<10);
    }

    protected override void OnRecover(object message) { }
}
""", (17, 13, 17, 37), ["PersistAsync", "PersistAllAsync"]),
            
            // ReceivePersistentActor calling Persist inside a message handler delegate
            (
"""
// 17
using Akka.Persistence;
using System.Linq;

public class MyActor: ReceivePersistentActor
{
    public override string PersistenceId { get; }
    public MyActor(string persistenceId)
    {
        PersistenceId = persistenceId;
        CommandAny(MessageHandler);
    }
    
    private void MessageHandler(object obj)
    {
        foreach (var i in Enumerable.Range(0, 10))
        {
            Persist(i, o => {});
        }
    }
}
""", (18, 13, 18, 32), ["Persist", "PersistAll"]),
        };

    [Theory]
    [MemberData(nameof(SuccessCases))]
    public async Task SuccessCase(string testCode)
    {
        await Verify.VerifyAnalyzer(testCode).ConfigureAwait(true);
    }

    [Theory]
    [MemberData(nameof(FailureCases))]
    public Task FailureCase(
        (string testCode, (int startLine, int startColumn, int endLine, int endColumn) spanData, object[] arguments) d)
    {
        var expected = Verify.Diagnostic()
            .WithSpan(d.spanData.startLine, d.spanData.startColumn, d.spanData.endLine, d.spanData.endColumn)
            .WithSeverity(DiagnosticSeverity.Warning)
            .WithArguments(d.arguments);

        return Verify.VerifyAnalyzer(d.testCode, expected);
    }
}