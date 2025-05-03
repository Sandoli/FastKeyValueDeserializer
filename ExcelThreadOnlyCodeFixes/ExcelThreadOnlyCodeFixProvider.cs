using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExcelThreadOnlyAnalyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ExcelThreadOnlyCodeFixes;

   [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ExcelThreadOnlyCodeFixProvider)), Shared]
    public class ExcelThreadOnlyCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => [ExcelThreadOnlyCallAnalyzer.DiagnosticId];

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root == null)
                return;

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var declaration = root.FindToken(diagnosticSpan.Start)
                .Parent?.AncestorsAndSelf()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault();

            if (declaration == null)
                return;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Add [ExcelThreadOnly]",
                    createChangedDocument: c => AddExcelThreadOnlyAttributeAsync(context.Document, declaration, c),
                    equivalenceKey: "AddExcelThreadOnly"),
                diagnostic);
        }

        private async Task<Document> AddExcelThreadOnlyAttributeAsync(Document document, MethodDeclarationSyntax methodDecl, CancellationToken cancellationToken)
        {
            var attribute = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("ExcelThreadOnly"));
            var list = methodDecl.AttributeLists.Add(
                SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute)));

            var newMethodDecl = methodDecl.WithAttributeLists(list);

            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var newRoot = root?.ReplaceNode(methodDecl, newMethodDecl);
            if (newRoot != null)
                return document.WithSyntaxRoot(newRoot);
            
            throw new InvalidOperationException("Impossible to build a new syntax root.");
        }
    }
