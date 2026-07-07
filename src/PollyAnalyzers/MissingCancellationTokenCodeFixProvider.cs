using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PollyAnalyzers;

/// <summary>Offers to append the available CancellationToken as the call's last argument.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MissingCancellationTokenCodeFixProvider)), Shared]
public sealed class MissingCancellationTokenCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create("PLY005");

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics.First();
        var invocation = root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation is null)
        {
            return;
        }

        // The token's parameter name is embedded in the diagnostic message args isn't directly
        // accessible here, so re-derive it the same way the analyzer did: the nearest enclosing
        // method's CancellationToken-typed parameter.
        var containingMethod = invocation.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        var tokenParameterName = containingMethod?.ParameterList.Parameters
            .FirstOrDefault(p => p.Type is IdentifierNameSyntax { Identifier.Text: "CancellationToken" }
                               or QualifiedNameSyntax { Right.Identifier.Text: "CancellationToken" })
            ?.Identifier.Text;

        if (tokenParameterName is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Pass '{tokenParameterName}'",
                createChangedDocument: ct => AddTokenArgumentAsync(context.Document, invocation, tokenParameterName, ct),
                equivalenceKey: "PollyAnalyzers.AddCancellationToken"),
            diagnostic);
    }

    private static async Task<Document> AddTokenArgumentAsync(Document document, InvocationExpressionSyntax invocation, string tokenParameterName, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var newArgument = SyntaxFactory.Argument(SyntaxFactory.IdentifierName(tokenParameterName));
        var newArgumentList = invocation.ArgumentList.AddArguments(newArgument);
        var newInvocation = invocation.WithArgumentList(newArgumentList);
        var newRoot = root.ReplaceNode(invocation, newInvocation);
        return document.WithSyntaxRoot(newRoot);
    }
}
