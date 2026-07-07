using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PollyAnalyzers;

/// <summary>
/// PLY005: flags a call to an async method that has a sibling overload accepting an
/// additional trailing <see cref="System.Threading.CancellationToken"/> parameter, when a
/// CancellationToken is available in the enclosing scope (a method parameter) but not passed.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MissingCancellationTokenAnalyzer : DiagnosticAnalyzer
{
    private const string CancellationTokenTypeName = "System.Threading.CancellationToken";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.MissingCancellationToken);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method)
        {
            return;
        }

        // Already passing a CancellationToken to this exact overload? Nothing to fix.
        if (method.Parameters.Any(p => p.Type.ToDisplayString() == CancellationTokenTypeName))
        {
            return;
        }

        if (!HasCancellationTokenOverload(method))
        {
            return;
        }

        var containingMethod = invocation.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        var availableToken = FindAvailableCancellationToken(context, containingMethod);
        if (availableToken is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.MissingCancellationToken,
            invocation.GetLocation(),
            method.Name,
            availableToken));
    }

    private static bool HasCancellationTokenOverload(IMethodSymbol method)
    {
        return method.ContainingType
            .GetMembers(method.Name)
            .OfType<IMethodSymbol>()
            .Any(candidate =>
                candidate.Parameters.Length == method.Parameters.Length + 1 &&
                candidate.Parameters[candidate.Parameters.Length - 1].Type.ToDisplayString() == CancellationTokenTypeName &&
                HasCompatiblePrefix(candidate.Parameters, method.Parameters));
    }

    private static bool HasCompatiblePrefix(ImmutableArray<IParameterSymbol> candidateParameters, ImmutableArray<IParameterSymbol> methodParameters)
    {
        for (var i = 0; i < methodParameters.Length; i++)
        {
            if (!SymbolEqualityComparer.Default.Equals(candidateParameters[i].Type, methodParameters[i].Type))
            {
                return false;
            }
        }

        return true;
    }

    private static string? FindAvailableCancellationToken(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax? containingMethod)
    {
        if (containingMethod is null)
        {
            return null;
        }

        foreach (var parameter in containingMethod.ParameterList.Parameters)
        {
            if (parameter.Type is null)
            {
                continue;
            }

            var type = context.SemanticModel.GetTypeInfo(parameter.Type, context.CancellationToken).Type;
            if (type?.ToDisplayString() == CancellationTokenTypeName)
            {
                return parameter.Identifier.Text;
            }
        }

        return null;
    }
}
