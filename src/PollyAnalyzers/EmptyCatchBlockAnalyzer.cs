using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PollyAnalyzers;

/// <summary>
/// PLY004: flags a <c>catch</c> block with no statements — the exception is caught and
/// silently discarded.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EmptyCatchBlockAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.EmptyCatchBlock);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.CatchClause);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var catchClause = (CatchClauseSyntax)context.Node;
        if (catchClause.Block.Statements.Count > 0)
        {
            return;
        }

        var exceptionTypeName = catchClause.Declaration?.Type?.ToString() ?? "Exception";
        context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.EmptyCatchBlock, catchClause.GetLocation(), exceptionTypeName));
    }
}
