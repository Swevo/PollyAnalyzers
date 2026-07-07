using System.Threading.Tasks;
using Xunit;

namespace PollyAnalyzers.Tests;

public class AsyncVoidMethodAnalyzerTests
{
    [Fact]
    public async Task AsyncVoidMethod_ReportsPLY002()
    {
        const string source = """
            using System.Threading.Tasks;

            class C
            {
                async void DoWork() => await Task.Delay(1);
            }
            """;

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(source, new AsyncVoidMethodAnalyzer());
        Assert.Contains(diagnostics, d => d.Id == "PLY002");
    }

    [Fact]
    public async Task AsyncVoid_EventHandlerSignature_DoesNotReport()
    {
        const string source = """
            using System;
            using System.Threading.Tasks;

            class C
            {
                async void Button_Click(object sender, EventArgs e) => await Task.Delay(1);
            }
            """;

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(source, new AsyncVoidMethodAnalyzer());
        Assert.DoesNotContain(diagnostics, d => d.Id == "PLY002");
    }

    [Fact]
    public async Task AsyncTask_DoesNotReport()
    {
        const string source = """
            using System.Threading.Tasks;

            class C
            {
                async Task DoWork() => await Task.Delay(1);
            }
            """;

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(source, new AsyncVoidMethodAnalyzer());
        Assert.DoesNotContain(diagnostics, d => d.Id == "PLY002");
    }

    [Fact]
    public async Task CodeFix_ChangesReturnTypeToTask()
    {
        const string source = """
            using System.Threading.Tasks;

            class C
            {
                async void DoWork() => await Task.Delay(1);
            }
            """;

        var fixedSource = await AnalyzerTestHelper.ApplyFixAsync(
            source, new AsyncVoidMethodAnalyzer(), new AsyncVoidMethodCodeFixProvider());

        Assert.DoesNotContain("async void", fixedSource);
        Assert.Contains("async System.Threading.Tasks.Task DoWork", fixedSource);
    }
}
