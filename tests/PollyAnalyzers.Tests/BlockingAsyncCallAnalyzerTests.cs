using System.Threading.Tasks;
using Xunit;

namespace PollyAnalyzers.Tests;

public class BlockingAsyncCallAnalyzerTests
{
    [Fact]
    public async Task DotResult_OnTaskOfT_ReportsPLY001()
    {
        const string source = """
            using System.Threading.Tasks;

            class C
            {
                int M(Task<int> task) => task.Result;
            }
            """;

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(source, new BlockingAsyncCallAnalyzer());
        Assert.Contains(diagnostics, d => d.Id == "PLY001");
    }

    [Fact]
    public async Task Wait_OnTask_ReportsPLY001()
    {
        const string source = """
            using System.Threading.Tasks;

            class C
            {
                void M(Task task) => task.Wait();
            }
            """;

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(source, new BlockingAsyncCallAnalyzer());
        Assert.Contains(diagnostics, d => d.Id == "PLY001");
    }

    [Fact]
    public async Task GetAwaiterGetResult_OnTask_ReportsPLY001()
    {
        const string source = """
            using System.Threading.Tasks;

            class C
            {
                void M(Task<int> task)
                {
                    var value = task.GetAwaiter().GetResult();
                }
            }
            """;

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(source, new BlockingAsyncCallAnalyzer());
        Assert.Contains(diagnostics, d => d.Id == "PLY001");
    }

    [Fact]
    public async Task Await_OnTask_DoesNotReport()
    {
        const string source = """
            using System.Threading.Tasks;

            class C
            {
                async Task<int> M(Task<int> task) => await task;
            }
            """;

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(source, new BlockingAsyncCallAnalyzer());
        Assert.DoesNotContain(diagnostics, d => d.Id == "PLY001");
    }

    [Fact]
    public async Task CodeFix_InAsyncMethod_ReplacesResultWithAwait()
    {
        const string source = """
            using System.Threading.Tasks;

            class C
            {
                async Task<int> M(Task<int> task)
                {
                    return task.Result;
                }
            }
            """;

        var fixedSource = await AnalyzerTestHelper.ApplyFixAsync(
            source, new BlockingAsyncCallAnalyzer(), new BlockingAsyncCallCodeFixProvider());

        Assert.Contains("await task", fixedSource);
        Assert.DoesNotContain("task.Result", fixedSource);
    }
}
