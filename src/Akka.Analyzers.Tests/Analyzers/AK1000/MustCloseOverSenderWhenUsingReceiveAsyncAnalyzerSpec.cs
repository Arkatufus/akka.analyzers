// -----------------------------------------------------------------------
//  <copyright file="MustCloseOverSenderWhenUsingReceiveAsyncAnalyzerSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2024 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Verify = Akka.Analyzers.Tests.Utility.AkkaVerifier<Akka.Analyzers.MustCloseOverSenderWhenUsingReceiveAsyncAnalyzer>;

namespace Akka.Analyzers.Tests.Analyzers.AK1000;

public class MustCloseOverSenderWhenUsingReceiveAsyncAnalyzerSpec
{
    public static readonly TheoryData<string> SuccessCases = new()
    {
        // ReceiveActor using ReceiveAsync with closed Sender
"""
// 1
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : ReceiveActor
{
    public MyActor()
    {
        ReceiveAsync<string>(async str => {
            var sender = Sender;
            sender.Tell(await LocalFunction());
            return;
            
            async Task<int> LocalFunction()
            {
                await Task.Delay(10);
                return str.Length;
            }
        });
    }
}
""",

// ReceiveActor using ReceiveAsync with closed Sender, alternate version
"""
// 2
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : ReceiveActor
{
    public MyActor()
    {
        ReceiveAsync<string>(str => true, async str => {
            var sender = Sender;
            sender.Tell(await LocalFunction());
            return;
            
            async Task<int> LocalFunction()
            {
                await Task.Delay(10);
                return str.Length;
            }
        });
    }
}
""",

// ReceiveActor using ReceiveAnyAsync with closed Sender
"""
// 3
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : ReceiveActor
{
    public MyActor()
    {
        ReceiveAnyAsync(async obj => {
            var sender = Sender;
            sender.Tell(await LocalFunction());
            return;
            
            async Task<int> LocalFunction()
            {
                await Task.Delay(10);
                return obj.ToString().Length;
            }
        });
    }
}
""",

        // ReceiveActor using ReceiveAsync with closed Sender being accessed inside local function
"""
// 5
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : ReceiveActor
{
    public MyActor(){
        ReceiveAsync<string>(async str => {
            var sender = Sender;
            await Execute();
            return;
            
            async Task Execute()
            {
                await Task.Delay(10);
                sender.Tell(1234);
            }
        });
    }
}
""",

        // ReceiveActor using ReceiveAsync with closed Sender being accessed inside local function, alternate version
"""
// 6
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : ReceiveActor
{
    public MyActor(){
        ReceiveAsync<string>(str => true, async str => {
            var sender = Sender;
            await Execute();
            return;

            async Task Execute()
            {
                await Task.Delay(10);
                sender.Tell(1234);
            }
        });
    }
}
""",

// ReceiveActor using ReceiveAnyAsync with closed Sender being accessed inside local function
"""
// 7
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : ReceiveActor
{
    public MyActor(){
        ReceiveAnyAsync(async obj => {
            var sender = Sender;
            await Execute();
            return;
            
            async Task Execute(){
                await Task.Delay(10);
                sender.Tell(1234);
            }
        });
    }
}
""",

        // Identical ReceiveAsync and ReceiveAnyAsync method fingerprint in non-ReceiveActor class 
"""
// 9
using System;
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : UntypedActor
{
    public MyActor()
    {
        ReceiveAnyAsync(async o => Sender.Tell(o));
        ReceiveAsync<string>(async s => Sender.Tell(s));
    }

    protected override void OnReceive(object message) { }
    
    protected void ReceiveAsync<T>(Func<T, Task> handler, Predicate<T>? shouldHandle = null) { }
    protected void ReceiveAnyAsync(Func<object, Task> handler) { }
}
""",

        // ReceiveActor using ReceiveAsync with async method delegate, this needs to be handled by a different analyzer
"""
// 10
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : ReceiveActor
{
    public MyActor()
    {
        ReceiveAsync<string>(Handler);
    }
    
    private async Task Handler(string s) 
    {
        Sender.Tell(s);
    }
}
""",
    };

    public static readonly
        TheoryData<(string testData, (int startLine, int startColumn, int endLine, int endColumn) spanData)>
        FailureCases = new()
        {
            // ReceiveActor using ReceiveAsync with Sender
            (
"""
// 1
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : ReceiveActor
{
    public MyActor()
    {
        ReceiveAsync<string>(async str =>
        {
            Sender.Tell(str);
        });
    }
}
""", (11, 13, 11, 19)),
            
            // ReceiveActor using ReceiveAsync with Sender, alternate version
            (
"""
// 2
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : ReceiveActor
{
    public MyActor()
    {
        ReceiveAsync<string>(str => true, async str =>
        {
            Sender.Tell(str);
        });
    }
}
""", (11, 13, 11, 19)),
            
            // ReceiveActor using ReceiveAnyAsync with Sender
            (
"""
// 3
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : ReceiveActor
{
    public MyActor()
    {
        ReceiveAnyAsync(async str =>
        {
            Sender.Tell(str);
        });
    }
}
""", (11, 13, 11, 19)),

            // ReceiveActor using ReceiveAsync with Sender inside a local function
            (
"""
// 4
using System;
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : ReceiveActor
{
    public MyActor()
    {
        ReceiveAsync<string>(async str =>
        {
            await Execute();
            return;
            
            async Task Execute()
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
                Sender.Tell(str);
            }
        });
    }
}
""", (18, 17, 18, 23)),
            
            // ReceiveActor using ReceiveAsync with Sender inside a local function, alternate version
            (
"""
// 5
using System;
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : ReceiveActor
{
    public MyActor()
    {
        ReceiveAsync<string>(str => true, async str =>
        {
            await Execute();
            return;

            async Task Execute()
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
                Sender.Tell(str);
            }
        });
    }
}
""", (18, 17, 18, 23)),
            
            // ReceiveActor using ReceiveAsync with Sender inside a local function, alternate version
            (
"""
// 6
using System;
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : ReceiveActor
{
    public MyActor()
    {
        ReceiveAnyAsync(async str =>
        {
            await Execute();
            return;

            async Task Execute()
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
                Sender.Tell(str);
            }
        });
    }
}
""", (18, 17, 18, 23)),
            
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
        (string testCode, (int startLine, int startColumn, int endLine, int endColumn) spanData) d)
    {
        var expected = Verify.Diagnostic()
            .WithSpan(d.spanData.startLine, d.spanData.startColumn, d.spanData.endLine, d.spanData.endColumn)
            .WithSeverity(DiagnosticSeverity.Error);

        return Verify.VerifyAnalyzer(d.testCode, expected);
    }
}