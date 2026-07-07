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
/// Offers to change an <c>async void</c> method's return type to <c>async Task</c>.
/// Only rewrites the declaration — callers that depend on the method being fire-and-forget
/// (e.g. assigning it to a delegate expecting <c>void</c>) may need manual follow-up.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AsyncVoidMethodCodeFixProvider)), Shared]
public sealed class AsyncVoidMethodCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create("PLY002");

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics.First();
        var methodDeclaration = root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (methodDeclaration is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Change return type to 'Task'",
                createChangedDocument: ct => ChangeToTaskAsync(context.Document, methodDeclaration, ct),
                equivalenceKey: "PollyAnalyzers.ChangeToTask"),
            diagnostic);
    }

    private static async Task<Document> ChangeToTaskAsync(Document document, MethodDeclarationSyntax methodDeclaration, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var taskType = SyntaxFactory.ParseTypeName("System.Threading.Tasks.Task")
            .WithLeadingTrivia(methodDeclaration.ReturnType.GetLeadingTrivia())
            .WithTrailingTrivia(methodDeclaration.ReturnType.GetTrailingTrivia());

        var newMethod = methodDeclaration.WithReturnType(taskType);
        var newRoot = root.ReplaceNode(methodDeclaration, newMethod);
        return document.WithSyntaxRoot(newRoot);
    }
}
