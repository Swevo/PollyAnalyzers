using System.Threading.Tasks;
using Xunit;

namespace PollyAnalyzers.Tests;

public class MissingCancellationTokenAnalyzerTests
{
    [Fact]
    public async Task AvailableToken_NotPassed_ReportsPLY005()
    {
        const string source = """
            using System.Net.Http;
            using System.Threading;
            using System.Threading.Tasks;

            class C
            {
                async Task M(HttpClient client, string url, CancellationToken cancellationToken)
                {
                    await client.GetAsync(url);
                }
            }
            """;

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(source, new MissingCancellationTokenAnalyzer());
        Assert.Contains(diagnostics, d => d.Id == "PLY005");
    }

    [Fact]
    public async Task TokenAlreadyPassed_DoesNotReport()
    {
        const string source = """
            using System.Net.Http;
            using System.Threading;
            using System.Threading.Tasks;

            class C
            {
                async Task M(HttpClient client, string url, CancellationToken cancellationToken)
                {
                    await client.GetAsync(url, cancellationToken);
                }
            }
            """;

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(source, new MissingCancellationTokenAnalyzer());
        Assert.DoesNotContain(diagnostics, d => d.Id == "PLY005");
    }

    [Fact]
    public async Task NoTokenAvailableInScope_DoesNotReport()
    {
        const string source = """
            using System.Net.Http;
            using System.Threading.Tasks;

            class C
            {
                async Task M(HttpClient client, string url)
                {
                    await client.GetAsync(url);
                }
            }
            """;

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(source, new MissingCancellationTokenAnalyzer());
        Assert.DoesNotContain(diagnostics, d => d.Id == "PLY005");
    }

    [Fact]
    public async Task CodeFix_AppendsCancellationTokenArgument()
    {
        const string source = """
            using System.Net.Http;
            using System.Threading;
            using System.Threading.Tasks;

            class C
            {
                async Task M(HttpClient client, string url, CancellationToken cancellationToken)
                {
                    await client.GetAsync(url);
                }
            }
            """;

        var fixedSource = await AnalyzerTestHelper.ApplyFixAsync(
            source, new MissingCancellationTokenAnalyzer(), new MissingCancellationTokenCodeFixProvider());

        Assert.Contains("client.GetAsync(url, cancellationToken)", fixedSource);
    }
}
