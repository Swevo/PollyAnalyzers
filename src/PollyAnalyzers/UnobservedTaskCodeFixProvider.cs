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

/// <summary>
/// Offers two fixes for an unobserved fire-and-forget task: discard it explicitly with
/// <c>_ = </c> (always safe), or <c>await</c> it if the containing method is already async.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnobservedTaskCodeFixProvider)), Shared]
public sealed class UnobservedTaskCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create("PLY003");

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics.First();
        var statement = root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<ExpressionStatementSyntax>();
        if (statement is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Discard with '_ = '",
                createChangedDocument: ct => DiscardAsync(context.Document, statement, ct),
                equivalenceKey: "PollyAnalyzers.Discard"),
            diagnostic);

        var containingMethod = statement.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (containingMethod?.Modifiers.Any(SyntaxKind.AsyncKeyword) == true)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Await the call",
                    createChangedDocument: ct => AwaitAsync(context.Document, statement, ct),
                    equivalenceKey: "PollyAnalyzers.Await"),
                diagnostic);
        }
    }

    private static async Task<Document> DiscardAsync(Document document, ExpressionStatementSyntax statement, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var discardAssignment = SyntaxFactory.AssignmentExpression(
            SyntaxKind.SimpleAssignmentExpression,
            SyntaxFactory.IdentifierName(SyntaxFactory.Identifier("_")),
            statement.Expression.WithoutTrivia());

        var newStatement = statement.WithExpression(discardAssignment);
        var newRoot = root.ReplaceNode(statement, newStatement);
        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Document> AwaitAsync(Document document, ExpressionStatementSyntax statement, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var awaitExpression = SyntaxFactory.AwaitExpression(statement.Expression.WithoutTrivia());
        var newStatement = statement.WithExpression(awaitExpression);
        var newRoot = root.ReplaceNode(statement, newStatement);
        return document.WithSyntaxRoot(newRoot);
    }
}
