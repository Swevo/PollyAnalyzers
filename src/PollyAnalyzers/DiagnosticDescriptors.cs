using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace PollyAnalyzers;

/// <summary>
/// Centralized <see cref="DiagnosticDescriptor"/> definitions for all PollyAnalyzers rules.
/// </summary>
internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor BlockingAsyncCall = new(
        id: "PLY001",
        title: "Blocking call on async API may cause a deadlock",
        messageFormat: "'{0}' blocks synchronously on an async operation; use 'await' instead to avoid deadlocks and thread-pool starvation",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Calling .Result, .Wait(), or .GetAwaiter().GetResult() on a Task/ValueTask blocks the calling thread and can deadlock in contexts with a synchronization context (classic ASP.NET, WPF, WinForms) or starve the thread pool under load.");

    public static readonly DiagnosticDescriptor AsyncVoidMethod = new(
        id: "PLY002",
        title: "Avoid 'async void' methods",
        messageFormat: "'{0}' is 'async void'; exceptions thrown from it cannot be observed by the caller and will crash the process. Use 'async Task' unless this is an event handler.",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "async void methods propagate exceptions directly via the SynchronizationContext (or crash the process) instead of surfacing them on a returned Task, making failures unrecoverable and hard to test.");

    public static readonly DiagnosticDescriptor UnobservedTask = new(
        id: "PLY003",
        title: "Fire-and-forget task is not observed",
        messageFormat: "The result of '{0}' is not awaited, assigned, or discarded; exceptions from this call will be lost. Await it or assign it to '_' if the fire-and-forget is intentional.",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Calling an async method as a statement without awaiting or discarding the returned Task silently swallows any exception it throws.");

    public static readonly DiagnosticDescriptor EmptyCatchBlock = new(
        id: "PLY004",
        title: "Empty catch block swallows exceptions",
        messageFormat: "This catch block for '{0}' is empty and silently swallows the exception; at minimum log it or rethrow with 'throw;'",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "An empty catch block hides failures, making outages and data corruption far harder to diagnose. Log the exception, handle it explicitly, or rethrow it.");

    public static readonly DiagnosticDescriptor MissingCancellationToken = new(
        id: "PLY005",
        title: "Async call drops an available CancellationToken",
        messageFormat: "'{0}' has an overload accepting a CancellationToken, and '{1}' is available in scope, but it isn't being passed; the operation cannot be cancelled",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Not propagating an available CancellationToken to async calls means requests keep running after the caller has given up (e.g. an aborted HTTP request or a shutting-down host), wasting resources and delaying graceful shutdown.");

    public static ImmutableArray<DiagnosticDescriptor> All { get; } = ImmutableArray.Create(
        BlockingAsyncCall, AsyncVoidMethod, UnobservedTask, EmptyCatchBlock, MissingCancellationToken);
}
