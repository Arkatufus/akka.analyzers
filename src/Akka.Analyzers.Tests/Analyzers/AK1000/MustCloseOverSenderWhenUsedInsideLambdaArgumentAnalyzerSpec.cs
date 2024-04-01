// -----------------------------------------------------------------------
//  <copyright file="MustCloseOverSenderWhenUsingReceiveAsyncAnalyzerSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2024 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Verify = Akka.Analyzers.Tests.Utility.AkkaVerifier<Akka.Analyzers.MustCloseOverSenderWhenUsedInsideLambdaArgumentAnalyzer>;

namespace Akka.Analyzers.Tests.Analyzers.AK1000;

public class MustCloseOverSenderWhenUsedInsideLambdaArgumentAnalyzerSpec
{
    public static readonly TheoryData<string> SuccessCases = new()
    {
        // ReceiveActor using ReceiveAsync with closed Sender
"""
// 01
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : ReceiveActor
{
    public MyActor()
    {
        var api = new ThirdPartyApi();
        ReceiveAsync<string>(async str => {
            var sender = Sender;
            await api.AsyncLambda(async () => {
                sender.Tell(new Message(str));
            });
        });
    }
}
""",

// ReceiveActor using ReceiveAsync with closed Sender, alternate version
"""
// 02
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : ReceiveActor
{
    public MyActor()
    {
        var api = new ThirdPartyApi();
        ReceiveAsync<string>(str => true, async str => {
            var sender = Sender;
            await api.AsyncLambda(async () => {
                sender.Tell(new Message(str));
            });
        });
    }
}
""",

// ReceiveActor using ReceiveAnyAsync with closed Sender
"""
// 03
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : ReceiveActor
{
    public MyActor()
    {
        var api = new ThirdPartyApi();
        ReceiveAnyAsync(async obj => {
            var sender = Sender;
            await api.AsyncLambda(async () => {
                sender.Tell(obj);
            });
        });
    }
}
""",

        // ReceiveActor using ReceiveAsync with closed Sender being accessed inside local function
"""
// 04
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : ReceiveActor
{
    public MyActor()
    {
        var api = new ThirdPartyApi();
        ReceiveAsync<string>(async str => {
            var sender = Sender;
            await Execute();
            return;
            
            async Task Execute()
            {
                await api.AsyncLambda(async () => {
                    sender.Tell(str);
                });
            }
        });
    }
}
""",

        // ReceiveActor using ReceiveAsync with closed Sender being accessed inside local function, alternate version
"""
// 05
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : ReceiveActor
{
    public MyActor()
    {
        var api = new ThirdPartyApi();
        ReceiveAsync<string>(str => true, async str => {
            var sender = Sender;
            await Execute();
            return;

            async Task Execute()
            {
                await api.AsyncLambda(async () => {
                    sender.Tell(str);
                });
            }
        });
    }
}
""",

// ReceiveActor using ReceiveAnyAsync with closed Sender being accessed inside local function
"""
// 06
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : ReceiveActor
{
    public MyActor()
    {
        var api = new ThirdPartyApi();
        ReceiveAnyAsync(async obj => {
            var sender = Sender;
            await Execute();
            return;
            
            async Task Execute(){
                await api.AsyncLambda(async () => {
                    sender.Tell(obj);
                });
            }
        });
    }
}
""",
        // Callback from a synchronous third party API, this fine (maybe)
"""
// 07
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : ReceiveActor
{
    public MyActor()
    {
        var api = new ThirdPartyApi();
        ReceiveAnyAsync(async obj => {
            api.SyncActionCallback(() => {
                    Sender.Tell(obj);
                });
        });
    }
}
""",

        // Identical Sender and Self property in non-Actor class 
"""
// 08
using System;
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyNonActor
{
    private IActorRef Self { get; }
    private IActorRef Sender { get; }
    
    public MyNonActor(IActorRef self, IActorRef sender)
    {
        Self = self;
        Sender = sender;
        var api = new ThirdPartyApi();
        
        ReceiveAnyAsync(async o => {
            await api.AsyncLambda(async () => {
                Sender.Tell(o, Self);
            });
        });
        ReceiveAsync<string>(async s => {
            await api.AsyncLambda(async () => {
                Sender.Tell(s, Self);
            });
        });
    }

    protected void ReceiveAsync<T>(Func<T, Task> handler, Predicate<T>? shouldHandle = null) { }
    protected void ReceiveAnyAsync(Func<object, Task> handler) { }
}
""",
    };

    private const string ThirdPartyApiClass = """
using System;
using System.Threading.Tasks;

public sealed class ThirdPartyApi
{
    public async Task ActionLambda(Action callback)
    {
        await AsyncContextBreaker().ConfigureAwait(false);
        callback();
    }

    public async Task AsyncLambda(Func<Task> callback)
    {
        await AsyncContextBreaker().ConfigureAwait(false);
        await callback();
    }
    
    public void SyncActionCallback(Action callback)
    {
        callback();
    }
    
    private async Task AsyncContextBreaker()
    { }
}
""";

    private const string MessageClass = """
public sealed class Message
{
    public Message(string payload)
    {
        Payload = payload;
    }
    
    public string Payload { get; }
}
""";
    
    public static readonly
        TheoryData<(string testData, (int startLine, int startColumn, int endLine, int endColumn) spanData, string arg)>
        FailureCases = new()
        {
            // ReceiveActor using ReceiveAsync with Sender inside lambda block
            (
"""
// 01
using System;
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : ReceiveActor
{
    public MyActor()
    {
        var api = new ThirdPartyApi();
        ReceiveAsync<string>(async str =>
        {
            await api.AsyncLambda(async () => {
                Sender.Tell(new Message(str));
            });
        });
    }
}
""", (14, 17, 14, 23), "Sender"),
            
            // ReceiveActor using ReceiveAsync with Sender inside lambda expression body
            (
"""
// 02
using System;
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : ReceiveActor
{
    public MyActor()
    {
        var api = new ThirdPartyApi();
        ReceiveAsync<string>(async str =>
        {
            await api.AsyncLambda(async () => Sender.Tell(new Message(str)));
        });
    }
}
""", (13, 47, 13, 53), "Sender"),
            
            // ReceiveActor using ReceiveAsync with Context.Sender inside lambda block
            (
"""
// 03
using System;
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : ReceiveActor
{
    public MyActor()
    {
        var api = new ThirdPartyApi();
        ReceiveAsync<string>(async str =>
        {
            await api.AsyncLambda(async () => {
                Context.Sender.Tell(new Message(str));
            });
        });
    }
}
""", (14, 17, 14, 31), "Sender"),
            
            // ReceiveActor using ReceiveAsync with Context.Sender inside lambda expression body
            (
"""
// 04
using System;
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : ReceiveActor
{
    public MyActor()
    {
        var api = new ThirdPartyApi();
        ReceiveAsync<string>(async str =>
        {
            await api.AsyncLambda(async () => Context.Sender.Tell(new Message(str)));
        });
    }
}
""", (13, 47, 13, 61), "Sender"),
            
            // ReceiveActor using ReceiveAsync with Sender inside lambda expression body
            (
"""
// 05
using System;
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : ReceiveActor
{
    public MyActor()
    {
        var api = new ThirdPartyApi();
        ReceiveAsync<string>(async str => await api.AsyncLambda(async () => Sender.Tell(new Message(str))));
    }
}
""", (11, 77, 11, 83), "Sender"),
            
            // ReceiveActor using ReceiveAsync with Context.Sender inside lambda expression body
            (
"""
// 06
using System;
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : ReceiveActor
{
    public MyActor()
    {
        var api = new ThirdPartyApi();
        ReceiveAsync<string>(async str => await api.AsyncLambda(async () => Context.Sender.Tell(new Message(str))));
    }
}
""", (11, 77, 11, 91), "Sender"),
            
            // ReceiveActor using ReceiveAsync with Self inside lambda block
            (
"""
// 07
using System;
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : ReceiveActor
{
    public MyActor()
    {
        var api = new ThirdPartyApi();
        ReceiveAsync<string>(async str =>
        {
            await api.AsyncLambda(async () => {
                Self.Tell(new Message(str));
            });
        });
    }
}
""", (14, 17, 14, 21), "Self"),

            // ReceiveActor using ReceiveAsync with Self inside lambda expression body
            (
"""
// 08
using System;
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : ReceiveActor
{
    public MyActor()
    {
        var api = new ThirdPartyApi();
        ReceiveAsync<string>(async str =>
        {
            await api.AsyncLambda(async () => Self.Tell(new Message(str)));
        });
    }
}
""", (13, 47, 13, 51), "Self"),
            
            // ReceiveActor using ReceiveAsync with Context.Self inside lambda block
            (
"""
// 09
using System;
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : ReceiveActor
{
    public MyActor()
    {
        var api = new ThirdPartyApi();
        ReceiveAsync<string>(async str =>
        {
            await api.AsyncLambda(async () => {
                Context.Self.Tell(new Message(str));
            });
        });
    }
}
""", (14, 17, 14, 29), "Self"),

            // ReceiveActor using ReceiveAsync with Context.Self inside lambda expression body
            (
"""
// 10
using System;
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : ReceiveActor
{
    public MyActor()
    {
        var api = new ThirdPartyApi();
        ReceiveAsync<string>(async str =>
        {
            await api.AsyncLambda(async () => Context.Self.Tell(new Message(str)));
        });
    }
}
""", (13, 47, 13, 59), "Self"),
            
            // ReceiveActor using ReceiveAsync with Self inside lambda expression body
            (
"""
// 11
using System;
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : ReceiveActor
{
    public MyActor()
    {
        var api = new ThirdPartyApi();
        ReceiveAsync<string>(async str => await api.AsyncLambda(async () => Self.Tell(new Message(str))));
    }
}
""", (11, 77, 11, 81), "Self"),
            
            // ReceiveActor using ReceiveAsync with Context.Self inside lambda expression body
            (
"""
// 12
using System;
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : ReceiveActor
{
    public MyActor()
    {
        var api = new ThirdPartyApi();
        ReceiveAsync<string>(async str => await api.AsyncLambda(async () => Context.Self.Tell(new Message(str))));
    }
}
""", (11, 77, 11, 89), "Self"),
            
            // ReceiveActor using ReceiveAsync with Sender inside local function
            (
"""
// 13
using System;
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : ReceiveActor
{
    public MyActor()
    {
        var api = new ThirdPartyApi();
        ReceiveAsync<string>(async str =>
        {
            await AsyncLocalFunction(str);
            
            async Task AsyncLocalFunction(string msg)
            {
                await api.AsyncLambda(async () => Sender.Tell(new Message(msg)));
            }
        });
    }
}
""", (17, 51, 17, 57), "Sender"),
            
            // ReceiveActor using ReceiveAsync with Self inside local function
            (
"""
// 14
using System;
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : ReceiveActor
{
    public MyActor()
    {
        var api = new ThirdPartyApi();
        ReceiveAsync<string>(async str =>
        {
            await AsyncLocalFunction(str);
            
            async Task AsyncLocalFunction(string msg)
            {
                await api.AsyncLambda(async () => Self.Tell(new Message(msg)));
            }
        });
    }
}
""", (17, 51, 17, 55), "Self"),
            
            // ReceiveActor using sync over async Receive with Sender
            (
"""
// 15
using System;
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : ReceiveActor
{
    public MyActor()
    {
        var api = new ThirdPartyApi();
        Receive<string>(str =>
        {
            api.AsyncLambda(async () => Sender.Tell(new Message(str))).Wait();
        });
    }
}
""", (13, 41, 13, 47), "Sender"),
            
            // ReceiveActor using sync over async Receive with Self
            (
"""
// 16
using System;
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : ReceiveActor
{
    public MyActor(IActorRef actor)
    {
        var api = new ThirdPartyApi();
        Receive<string>(str =>
        {
            api.AsyncLambda(async () => Self.Tell(new Message(str))).Wait();
        });
    }
}
""", (13, 41, 13, 45), "Self"),
            
            // ReceiveActor using ReceiveAsync with Sender inside an Action callback
            (
"""
// 17
using System;
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : ReceiveActor
{
    public MyActor()
    {
        var api = new ThirdPartyApi();
        ReceiveAsync<string>(async str =>
        {
            await api.ActionLambda(() => Sender.Tell(str));
        });
    }
}
""", (13, 42, 13, 48), "Sender"),
            
            // ReceiveActor using ReceiveAsync with Self inside an Action callback
            (
"""
// 18
using System;
using Akka.Actor;
using System.Threading.Tasks;

public sealed class MyActor : ReceiveActor
{
    public MyActor(IActorRef actor)
    {
        var api = new ThirdPartyApi();
        ReceiveAsync<string>(async str =>
        {
            await api.ActionLambda(() => Self.Tell(str));
        });
    }
}
""", (13, 42, 13, 46), "Self"),
            
        };

    [Theory]
    [MemberData(nameof(SuccessCases))]
    public Task SuccessCase(string testCode)
    {
        return Verify.VerifyAnalyzer([testCode, ThirdPartyApiClass, MessageClass]);
    }

    [Theory]
    [MemberData(nameof(FailureCases))]
    public Task FailureCase(
        (string testCode, (int startLine, int startColumn, int endLine, int endColumn) spanData, string arg) d)
    {
        var expected = Verify.Diagnostic()
            .WithSpan(d.spanData.startLine, d.spanData.startColumn, d.spanData.endLine, d.spanData.endColumn)
            .WithSeverity(DiagnosticSeverity.Error)
            .WithArguments(d.arg);

        return Verify.VerifyAnalyzer([d.testCode, ThirdPartyApiClass, MessageClass], expected);
    }
}