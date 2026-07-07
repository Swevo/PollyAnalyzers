using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PollyAnalyzers;

/// <summary>
/// PLY002: flags <c>async void</c> methods other than conventional event handlers
/// (<c>(object sender, TEventArgs e)</c>) and overrides (which must match a base signature).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AsyncVoidMethodAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.AsyncVoidMethod);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.MethodDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;

        if (!method.Modifiers.Any(SyntaxKind.AsyncKeyword))
        {
            return;
        }

        if (method.ReturnType is not PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.VoidKeyword })
        {
            return;
        }

        if (method.Modifiers.Any(SyntaxKind.OverrideKeyword))
        {
            return;
        }

        if (IsEventHandlerSignature(context, method))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.AsyncVoidMethod, method.Identifier.GetLocation(), method.Identifier.Text));
    }

    private static bool IsEventHandlerSignature(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax method)
    {
        var parameters = method.ParameterList.Parameters;
        if (parameters.Count != 2 || parameters[0].Type is null || parameters[1].Type is null)
        {
            return false;
        }

        var senderType = context.SemanticModel.GetTypeInfo(parameters[0].Type!, context.CancellationToken).Type;
        if (senderType?.SpecialType != SpecialType.System_Object)
        {
            return false;
        }

        var eventArgsType = context.SemanticModel.GetTypeInfo(parameters[1].Type!, context.CancellationToken).Type;
        for (var current = eventArgsType; current is not null; current = current.BaseType)
        {
            if (current.ToDisplayString() == "System.EventArgs")
            {
                return true;
            }
        }

        return false;
    }
}
