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
/// Offers to replace a blocking <c>.Result</c>/<c>.Wait()</c>/<c>.GetAwaiter().GetResult()</c>
/// call with an <c>await</c> expression. Only offered when the containing method is already
/// <c>async</c>, since making a method async can change its signature and ripple to callers.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(BlockingAsyncCallCodeFixProvider)), Shared]
public sealed class BlockingAsyncCallCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create("PLY001");

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics.First();
        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

        var containingMethod = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        var isAsyncMethod = containingMethod?.Modifiers.Any(SyntaxKind.AsyncKeyword) == true;
        if (!isAsyncMethod)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Replace with 'await'",
                createChangedDocument: ct => ReplaceWithAwaitAsync(context.Document, node, ct),
                equivalenceKey: "PollyAnalyzers.ReplaceWithAwait"),
            diagnostic);
    }

    private static async Task<Document> ReplaceWithAwaitAsync(Document document, SyntaxNode node, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        ExpressionSyntax? taskExpression = node switch
        {
            // X.Result
            MemberAccessExpressionSyntax { Name.Identifier.Text: "Result" } resultAccess => resultAccess.Expression,
            // X.Wait()
            InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Wait" } waitAccess } => waitAccess.Expression,
            // X.GetAwaiter().GetResult()
            InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax
                {
                    Name.Identifier.Text: "GetResult",
                    Expression: InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "GetAwaiter" } getAwaiterAccess },
                }
            } => getAwaiterAccess.Expression,
            _ => null,
        };

        if (taskExpression is null)
        {
            return document;
        }

        var awaitExpression = SyntaxFactory.AwaitExpression(taskExpression.WithoutTrivia())
            .WithTriviaFrom(node);

        var newRoot = root.ReplaceNode(node, awaitExpression);
        return document.WithSyntaxRoot(newRoot);
    }
}
