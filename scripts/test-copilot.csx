#!/usr/bin/env dotnet-script
// Minimal Copilot SDK test — matches Chat.cs sample exactly.
// Run on Windows: dotnet script test-copilot.csx
// Or copy the code into a new Console app if dotnet-script isn't installed.
//
// This tests whether the Copilot CLI can reach the model API at all,
// with zero extras (no tools, no system message, no streaming).

#r "nuget: GitHub.Copilot.SDK, 0.1.26"
using GitHub.Copilot.SDK;

Console.WriteLine("Creating CopilotClient...");
await using var client = new CopilotClient();

Console.WriteLine("Creating session...");
await using var session = await client.CreateSessionAsync(new SessionConfig
{
    OnPermissionRequest = PermissionHandler.ApproveAll
});

Console.WriteLine("Session created. Sending test message...");

using var _ = session.On(evt =>
{
    Console.WriteLine($"  [event] {evt.GetType().Name}");
});

var reply = await session.SendAndWaitAsync(new MessageOptions
{
    Prompt = "Say hello in one sentence"
});

Console.WriteLine($"Reply: {reply?.Data.Content ?? "(null)"}");
Console.WriteLine("Done.");
