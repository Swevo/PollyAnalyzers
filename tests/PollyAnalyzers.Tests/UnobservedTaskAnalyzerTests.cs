using System.Threading.Tasks;
using Xunit;

namespace PollyAnalyzers.Tests;

public class UnobservedTaskAnalyzerTests
{
    [Fact]
    public async Task FireAndForgetCall_ReportsPLY003()
    {
        const string source = """
            using System.Threading.Tasks;

            class C
            {
                async Task M()
                {
                    DoWorkAsync();
                }

                Task DoWorkAsync() => Task.CompletedTask;
            }
            """;

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(source, new UnobservedTaskAnalyzer());
        Assert.Contains(diagnostics, d => d.Id == "PLY003");
    }

    [Fact]
    public async Task AwaitedCall_DoesNotReport()
    {
        const string source = """
            using System.Threading.Tasks;

            class C
            {
                async Task M()
                {
                    await DoWorkAsync();
                }

                Task DoWorkAsync() => Task.CompletedTask;
            }
            """;

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(source, new UnobservedTaskAnalyzer());
        Assert.DoesNotContain(diagnostics, d => d.Id == "PLY003");
    }

    [Fact]
    public async Task DiscardedCall_DoesNotReport()
    {
        const string source = """
            using System.Threading.Tasks;

            class C
            {
                void M()
                {
                    _ = DoWorkAsync();
                }

                Task DoWorkAsync() => Task.CompletedTask;
            }
            """;

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(source, new UnobservedTaskAnalyzer());
        Assert.DoesNotContain(diagnostics, d => d.Id == "PLY003");
    }

    [Fact]
    public async Task CodeFix_Discard_AddsUnderscoreAssignment()
    {
        const string source = """
            using System.Threading.Tasks;

            class C
            {
                void M()
                {
                    DoWorkAsync();
                }

                Task DoWorkAsync() => Task.CompletedTask;
            }
            """;

        var fixedSource = await AnalyzerTestHelper.ApplyFixAsync(
            source, new UnobservedTaskAnalyzer(), new UnobservedTaskCodeFixProvider(), "PollyAnalyzers.Discard");

        Assert.Contains("_ = DoWorkAsync();", fixedSource);
    }
}
