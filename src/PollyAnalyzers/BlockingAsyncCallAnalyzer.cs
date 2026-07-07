using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PollyAnalyzers;

/// <summary>
/// PLY001: flags <c>.Result</c>, <c>.Wait()</c>, and <c>.GetAwaiter().GetResult()</c> on
/// <see cref="System.Threading.Tasks.Task"/>/<c>Task&lt;T&gt;</c>/<c>ValueTask</c>/<c>ValueTask&lt;T&gt;</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BlockingAsyncCallAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.BlockingAsyncCall);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;
        if (memberAccess.Name.Identifier.Text != "Result")
        {
            return;
        }

        // Skip if this ".Result" is itself the receiver of a further invocation we'd rather
        // report at (not applicable here, Result is a property so this is always the leaf).
        var typeInfo = context.SemanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken);
        if (IsTaskLike(typeInfo.Type))
        {
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.BlockingAsyncCall, memberAccess.GetLocation(), memberAccess.ToString()));
        }
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        var methodName = memberAccess.Name.Identifier.Text;

        if (methodName == "Wait" && invocation.ArgumentList.Arguments.Count == 0)
        {
            var typeInfo = context.SemanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken);
            if (IsTaskLike(typeInfo.Type))
            {
                context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.BlockingAsyncCall, invocation.GetLocation(), invocation.ToString()));
            }

            return;
        }

        if (methodName == "GetResult" &&
            memberAccess.Expression is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax innerMemberAccess } innerInvocation &&
            innerMemberAccess.Name.Identifier.Text == "GetAwaiter")
        {
            var typeInfo = context.SemanticModel.GetTypeInfo(innerMemberAccess.Expression, context.CancellationToken);
            if (IsTaskLike(typeInfo.Type))
            {
                context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.BlockingAsyncCall, invocation.GetLocation(), invocation.ToString()));
            }
        }
    }

    internal static bool IsTaskLike(ITypeSymbol? type)
    {
        if (type is null)
        {
            return false;
        }

        return type.OriginalDefinition.ToDisplayString() switch
        {
            "System.Threading.Tasks.Task" => true,
            "System.Threading.Tasks.Task<TResult>" => true,
            "System.Threading.Tasks.ValueTask" => true,
            "System.Threading.Tasks.ValueTask<TResult>" => true,
            _ => false,
        };
    }
}
