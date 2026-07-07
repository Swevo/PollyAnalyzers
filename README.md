# PollyAnalyzers

[![NuGet](https://img.shields.io/nuget/v/Swevo.PollyAnalyzers.svg)](https://www.nuget.org/packages/Swevo.PollyAnalyzers/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Swevo.PollyAnalyzers.svg)](https://www.nuget.org/packages/Swevo.PollyAnalyzers/)
[![CI](https://github.com/Swevo/PollyAnalyzers/actions/workflows/build.yml/badge.svg)](https://github.com/Swevo/PollyAnalyzers/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**Free, MIT-licensed Roslyn analyzers for async/resilience anti-patterns.** No per-seat
license required — ever.

## Why PollyAnalyzers?

ReSharper (~$249+/seat/year) and similar commercial tools are broad style/inspection suites.
Free tools like Roslynator and SonarLint already cover general C# code smells well. What none
of them specifically targets is **reliability**: the handful of async/exception-handling
mistakes that cause deadlocks, silent failures, and un-cancellable requests in production.
PollyAnalyzers is a small, focused analyzer pack for exactly that gap, built by the same team
that maintains 28+ Polly v8 resilience integration packages.

```csharp
var data = httpClient.GetAsync(url).Result;              // PLY001: blocking call, deadlock risk
async void OnTimerElapsed(object? state) { ... }          // PLY002: async void, unobservable exceptions
SendWelcomeEmailAsync(order);                             // PLY003: fire-and-forget, exception lost
catch (Exception) { }                                     // PLY004: swallowed exception
await httpClient.GetAsync(url);                           // PLY005: cancellationToken in scope, not passed
```

## Install

```bash
dotnet add package Swevo.PollyAnalyzers
```

Works automatically in Visual Studio, Rider, and VS Code (via the C# Dev Kit/OmniSharp
Roslyn workspace) the moment the package is referenced — no separate IDE extension to install.

## Rules

| ID | Severity | Title |
|---|---|---|
| PLY001 | Warning | Blocking call on async API (`.Result`, `.Wait()`, `.GetAwaiter().GetResult()`) may cause a deadlock |
| PLY002 | Warning | Avoid `async void` methods (except conventional event handlers) |
| PLY003 | Warning | Fire-and-forget task is not awaited, assigned, or discarded |
| PLY004 | Warning | Empty catch block swallows exceptions |
| PLY005 | Info | Async call drops an available `CancellationToken` |

Each rule ships with one or more code fixes (Ctrl+. / Alt+Enter):

- **PLY001** — replace with `await` (offered only inside an already-`async` method, since the
  fix can't safely make a sync method async and update every caller for you).
- **PLY002** — change the return type to `Task`.
- **PLY003** — discard with `_ = ` (always safe), or `await` the call (offered inside async methods).
- **PLY004** — insert `throw;` to rethrow instead of swallowing.
- **PLY005** — append the in-scope `CancellationToken` as the call's last argument.

## Suppressing a rule

Standard Roslyn suppression applies — per line:

```csharp
#pragma warning disable PLY001
var data = httpClient.GetAsync(url).Result;
#pragma warning restore PLY001
```

or per-project via `.editorconfig`:

```ini
dotnet_diagnostic.PLY001.severity = none
```

## Design goals

- **MIT licensed, forever.** No commercial tier, no per-seat fees.
- **Narrow and precise.** Five rules that catch real production incidents, not hundreds of
  style opinions — pairs cleanly alongside Roslynator/SonarLint rather than competing with them.
- **Actionable.** Every rule ships with a code fix, not just a warning.

## Roadmap

- Additional rules are being considered based on real-world false-positive/false-negative
  feedback: HttpClient calls with no timeout or retry policy configured, and `ConfigureAwait(false)`
  guidance for library code.

## 💼 Need .NET consulting?

I'm the author of PollyAnalyzers and a suite of compile-time source generators
([AutoWire](https://github.com/Swevo/AutoWire), [AutoMap.Generator](https://github.com/Swevo/AutoMap.Generator))
and 28+ Polly v8 resilience packages. I'm available for consulting on **Polly v8 resilience**,
**Azure cloud architecture**, and **clean .NET design**.

**[→ solidqualitysolutions.com](https://www.solidqualitysolutions.com/)** · **[LinkedIn](https://www.linkedin.com/in/justbannister/)**

## Also by the same author

> 🌐 Full suite overview: **[swevo.github.io](https://swevo.github.io/)**

| Package | Description |
|---|---|
| [**FluentPdf**](https://github.com/Swevo/FluentPdf) | Free, MIT-licensed fluent PDF generation — alternative to QuestPDF's commercial license. |
| [**AutoBus**](https://github.com/Swevo/AutoBus) | Free, MIT-licensed message bus — alternative to MassTransit's commercial license. |
| [**AutoArchitecture**](https://github.com/Swevo/AutoArchitecture) | Free, MIT-licensed compile-time architecture rule enforcement — alternative to NDepend. |
| [**AutoAssert**](https://github.com/Swevo/AutoAssert) | Free, MIT-licensed fluent assertions — alternative to FluentAssertions' commercial license. |
| [**AutoWire**](https://github.com/Swevo/AutoWire) | Compile-time DI auto-registration — `[Scoped]`/`[Singleton]`/`[Transient]` generates `IServiceCollection` registration code. |
| [**PollyAction**](https://github.com/Swevo/PollyAction) | Free retry/backoff GitHub Action — wrap any CI step with exponential-backoff retries. |

## License

MIT © Justin Bannister
