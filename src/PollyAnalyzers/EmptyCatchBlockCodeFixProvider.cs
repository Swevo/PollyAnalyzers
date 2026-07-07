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

/// <summary>Offers to insert a <c>throw;</c> statement into an empty catch block.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(EmptyCatchBlockCodeFixProvider)), Shared]
public sealed class EmptyCatchBlockCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create("PLY004");

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics.First();
        var catchClause = root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<CatchClauseSyntax>();
        if (catchClause is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Add 'throw;' to rethrow",
                createChangedDocument: ct => AddThrowAsync(context.Document, catchClause, ct),
                equivalenceKey: "PollyAnalyzers.AddThrow"),
            diagnostic);
    }

    private static async Task<Document> AddThrowAsync(Document document, CatchClauseSyntax catchClause, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var throwStatement = SyntaxFactory.ThrowStatement();
        var newBlock = catchClause.Block.WithStatements(SyntaxFactory.SingletonList<StatementSyntax>(throwStatement));
        var newRoot = root.ReplaceNode(catchClause.Block, newBlock);
        return document.WithSyntaxRoot(newRoot);
    }
}
