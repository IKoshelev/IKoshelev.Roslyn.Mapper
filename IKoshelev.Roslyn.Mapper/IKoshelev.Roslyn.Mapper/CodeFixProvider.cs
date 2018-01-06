using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using SF = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using Microsoft.CodeAnalysis.Formatting;

namespace IKoshelev.Roslyn.Mapper
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(IKoshelevRoslynMapperCodeFixProvider)), Shared]
    public class IKoshelevRoslynMapperCodeFixProvider : CodeFixProvider
    {
        public const string Title = "Regenerate defaultMappings.";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(IKoshelevRoslynMapperAnalyzer.DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            string automappableMembers = null;
            diagnostic.Properties?.TryGetValue(IKoshelevRoslynMapperAnalyzer.AutomappableMembersDictKey, out automappableMembers);

            if(automappableMembers == null)
            {
                return;
            }

            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var lambda = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<ParenthesizedLambdaExpressionSyntax>().First();

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: Title,
                    createChangedDocument: c => RegenerateDefaultMappingsAsync(context.Document, lambda, automappableMembers, c),
                    equivalenceKey: Title),
                diagnostic);
        }

        private async Task<Document> RegenerateDefaultMappingsAsync(
            Document document, 
            ParenthesizedLambdaExpressionSyntax lambda,
            string automappableMembers,
            CancellationToken cancellationToken)
        {
            var parsedMappableMembers = automappableMembers.Split(';');

            var sourceIdentifierName = lambda
                            .ChildNodes().OfType<ParameterListSyntax>().Single()
                            .ChildNodes().OfType<ParameterSyntax>().Single()
                            .ChildTokens().Single().ToString().Trim();

            var preparedNames = parsedMappableMembers
                                        .Select(x => $"{x}={sourceIdentifierName}.{x},\r\n")
                                        .ToArray();

            var preparedJoinedNames = string.Join("", preparedNames);

            var newInitializerText =
$@"new X{{
{preparedJoinedNames}}}";

                var newInitializer = ((ObjectCreationExpressionSyntax)SF.ParseExpression(newInitializerText))
                                       .ChildNodes().OfType<InitializerExpressionSyntax>().Single();

                var oldInitializer = lambda
                                    .ChildNodes().OfType<ObjectCreationExpressionSyntax>().Single()
                                    .ChildNodes().OfType<InitializerExpressionSyntax>().Single();

                var newRawLambda = lambda.ReplaceNode(oldInitializer, newInitializer);

                newRawLambda = newRawLambda.WithAdditionalAnnotations(Formatter.Annotation);

                var workspace = document.Project.Solution.Workspace;

                var newFormattedLambda = Formatter.Format(newRawLambda,
                                                        Formatter.Annotation,
                                                        workspace,
                                                        workspace.Options,
                                                        cancellationToken)
                                                        as ParenthesizedLambdaExpressionSyntax;

                var root = await document.GetSyntaxRootAsync(cancellationToken);

                var newDocumentRoot = root.ReplaceNode(lambda, newFormattedLambda);

                document = document.WithSyntaxRoot(newDocumentRoot);

                return document;
        }
    }
}