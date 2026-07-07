using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace PollyAnalyzers.Tests;

/// <summary>
/// Minimal in-process harness for running a single <see cref="DiagnosticAnalyzer"/> and,
/// optionally, applying a <see cref="CodeFixProvider"/>'s first offered fix — without pulling
/// in the full Microsoft.CodeAnalysis.Testing package set.
/// </summary>
internal static class AnalyzerTestHelper
{
    private static readonly Lazy<ImmutableArray<MetadataReference>> References = new(() =>
        ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            ?.Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray()
        ?? ImmutableArray<MetadataReference>.Empty);

    public static CSharpCompilation CreateCompilation(string source)
    {
        return CSharpCompilation.Create(
            assemblyName: "PollyAnalyzersTestAssembly",
            syntaxTrees: new[] { CSharpSyntaxTree.ParseText(source) },
            references: References.Value,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source, DiagnosticAnalyzer analyzer)
    {
        var compilation = CreateCompilation(source);
        var withAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create(analyzer));
        var diagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
        return diagnostics;
    }

    /// <summary>
    /// Runs <paramref name="analyzer"/> on <paramref name="source"/>, then applies the first
    /// registered code fix from <paramref name="codeFixProvider"/> (matching <paramref name="equivalenceKey"/>
    /// if given) for the first diagnostic found, returning the resulting source text.
    /// </summary>
    public static async Task<string> ApplyFixAsync(string source, DiagnosticAnalyzer analyzer, CodeFixProvider codeFixProvider, string? equivalenceKey = null)
    {
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);

        var solution = workspace.CurrentSolution
            .AddProject(projectId, "TestProject", "TestProject", LanguageNames.CSharp)
            .AddMetadataReferences(projectId, References.Value)
            .AddDocument(documentId, "Test.cs", SourceText.From(source));

        var document = solution.GetDocument(documentId) ?? throw new InvalidOperationException("Document not found.");
        var compilation = await document.Project.GetCompilationAsync().ConfigureAwait(false)
            ?? throw new InvalidOperationException("Compilation not found.");

        var withAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create(analyzer));
        var diagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
        var diagnostic = diagnostics.First();

        CodeAction? registeredAction = null;
        var context = new CodeFixContext(document, diagnostic, (action, _) =>
        {
            if (registeredAction is null && (equivalenceKey is null || action.EquivalenceKey == equivalenceKey))
            {
                registeredAction = action;
            }
        }, System.Threading.CancellationToken.None);

        await codeFixProvider.RegisterCodeFixesAsync(context).ConfigureAwait(false);
        if (registeredAction is null)
        {
            throw new InvalidOperationException("No code fix was registered.");
        }

        var operations = await registeredAction.GetOperationsAsync(System.Threading.CancellationToken.None).ConfigureAwait(false);
        var applyOperation = operations.OfType<ApplyChangesOperation>().First();
        var newDocument = applyOperation.ChangedSolution.GetDocument(documentId) ?? throw new InvalidOperationException("Fixed document not found.");
        var newText = await newDocument.GetTextAsync().ConfigureAwait(false);
        return newText.ToString();
    }
}
