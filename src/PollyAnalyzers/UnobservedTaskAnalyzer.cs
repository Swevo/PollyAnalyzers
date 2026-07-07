using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PollyAnalyzers;

/// <summary>
/// PLY003: flags an expression-statement invocation of an async (Task/ValueTask-returning)
/// method whose result is neither awaited, assigned, returned, nor discarded via <c>_ = </c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnobservedTaskAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.UnobservedTask);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ExpressionStatement);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var statement = (ExpressionStatementSyntax)context.Node;

        // `await X();` is an ExpressionStatement wrapping an AwaitExpression, not a bare
        // InvocationExpression, so it's naturally excluded here.
        if (statement.Expression is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
        if (symbol is null || !BlockingAsyncCallAnalyzer.IsTaskLike(symbol.ReturnType))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.UnobservedTask, invocation.GetLocation(), invocation.ToString()));
    }
}
