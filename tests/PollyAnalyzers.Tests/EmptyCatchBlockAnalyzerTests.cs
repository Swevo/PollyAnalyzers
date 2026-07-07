using System.Threading.Tasks;
using Xunit;

namespace PollyAnalyzers.Tests;

public class EmptyCatchBlockAnalyzerTests
{
    [Fact]
    public async Task EmptyCatchBlock_ReportsPLY004()
    {
        const string source = """
            using System;

            class C
            {
                void M()
                {
                    try
                    {
                        DoWork();
                    }
                    catch (Exception)
                    {
                    }
                }

                void DoWork() { }
            }
            """;

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(source, new EmptyCatchBlockAnalyzer());
        Assert.Contains(diagnostics, d => d.Id == "PLY004");
    }

    [Fact]
    public async Task CatchBlockWithLogging_DoesNotReport()
    {
        const string source = """
            using System;

            class C
            {
                void M()
                {
                    try
                    {
                        DoWork();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }

                void DoWork() { }
            }
            """;

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(source, new EmptyCatchBlockAnalyzer());
        Assert.DoesNotContain(diagnostics, d => d.Id == "PLY004");
    }

    [Fact]
    public async Task CodeFix_AddsThrow()
    {
        const string source = """
            using System;

            class C
            {
                void M()
                {
                    try
                    {
                        DoWork();
                    }
                    catch (Exception)
                    {
                    }
                }

                void DoWork() { }
            }
            """;

        var fixedSource = await AnalyzerTestHelper.ApplyFixAsync(
            source, new EmptyCatchBlockAnalyzer(), new EmptyCatchBlockCodeFixProvider());

        Assert.Contains("throw;", fixedSource);
    }
}
