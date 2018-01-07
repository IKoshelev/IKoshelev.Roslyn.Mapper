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
        public const string TitleRegenerateDefaultMappings = "Regenerate defaultMappings.";
        public const string TitleGenerateMappingArguments = "Generate mapping arguments.";

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

            var membersClassification = diagnostic.Properties;
            if(membersClassification.Any() == false)
            {
                return;
            }

            var root = await context
                                .Document
                                .GetSyntaxRootAsync(context.CancellationToken)
                                .ConfigureAwait(false);

            var lambda = root
                            .FindToken(diagnosticSpan.Start)
                            .Parent
                            .AncestorsAndSelf().OfType<ParenthesizedLambdaExpressionSyntax>().FirstOrDefault();

            var mappingCreation = root
                            .FindToken(diagnosticSpan.Start)
                            .Parent
                            .AncestorsAndSelf().OfType<ObjectCreationExpressionSyntax>().FirstOrDefault();

            membersClassification.TryGetValue(
                            IKoshelevRoslynMapperAnalyzer.AutomappableMembersDictKey,
                            out string automappableMembers);

            membersClassification.TryGetValue(
                            IKoshelevRoslynMapperAnalyzer.NonAutomappableSourceMembersDictKey,
                            out string nonAutomappableSourceMembers);

            membersClassification.TryGetValue(
                            IKoshelevRoslynMapperAnalyzer.NonAutomappableTargetMembersDictKey,
                            out string nonAutomappableTargetMembers);

            membersClassification.TryGetValue(
                            IKoshelevRoslynMapperAnalyzer.SourceTypeNameDictKey,
                            out string sourceTypeName);

            membersClassification.TryGetValue(
                            IKoshelevRoslynMapperAnalyzer.TargetTypeNameDictKey,
                            out string targetTypeName);

            if (lambda == null)
            {
                context.RegisterCodeFix(
                   CodeAction.Create(
                       title: TitleGenerateMappingArguments,
                       createChangedDocument: c => RegenerateFull(
                                                            context.Document,
                                                            mappingCreation, 
                                                            automappableMembers,
                                                            nonAutomappableSourceMembers,
                                                            nonAutomappableTargetMembers,
                                                            sourceTypeName,
                                                            targetTypeName,
                                                            c),
                       equivalenceKey: TitleRegenerateDefaultMappings),
                   diagnostic);
            }

            if (lambda != null && automappableMembers != null)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: TitleRegenerateDefaultMappings,
                        createChangedDocument: c => RegenerateDefaultMappingsAsync(context.Document, lambda, automappableMembers, c),
                        equivalenceKey: TitleRegenerateDefaultMappings),
                    diagnostic);
            }
        }

        private async Task<Document> RegenerateFull(
                                          Document document,
                                          ObjectCreationExpressionSyntax objectCreation,
                                          string automappableMembers,
                                          string nonAutomappableSourceMembers,
                                          string nonAutomappableTargetMembers,
                                          string sourceTypeName,
                                          string targetTypeName,
                                          CancellationToken cancellationToken)
        {
            var sourceIdentifierName = "source";
            var targetIdentifierName = "target";
            var mappableMembersText = prepareMembersText(automappableMembers, (name) => $"{name}={sourceIdentifierName}.{name}");
            var sourceIgnoreMembersText = prepareMembersText(nonAutomappableSourceMembers, (name) => $"({sourceTypeName} {sourceIdentifierName}) => {sourceIdentifierName}.{name}");
            var targetIgnoreMembersText = prepareMembersText(nonAutomappableTargetMembers, (name) => $"({targetTypeName} {targetIdentifierName}) => {targetIdentifierName}.{name}");

            var newObjectCreationText = $@"
new ExpressionMappingComponents<{sourceTypeName}, {targetTypeName}>(
    defaultMappings: ({sourceTypeName} {sourceIdentifierName}) => new {targetTypeName}()
    {{
        {mappableMembersText}}},
    customMappings: ({sourceTypeName} {sourceIdentifierName}) => new {targetTypeName}()
    {{
    }},
    sourceIgnoredProperties: new IgnoreList<{sourceTypeName}>(
        {sourceIgnoreMembersText}),
    targetIgnoredProperties: new IgnoreList<{targetTypeName}>(
        {targetIgnoreMembersText}))";

            var newObjectCreationRaw = ((ObjectCreationExpressionSyntax)SF.ParseExpression(newObjectCreationText));

            newObjectCreationRaw = newObjectCreationRaw.WithAdditionalAnnotations(Formatter.Annotation);

            var workspace = document.Project.Solution.Workspace;

            var newObjectCreationFormatted = Formatter.Format(
                                                            newObjectCreationRaw,
                                                            Formatter.Annotation,
                                                            workspace,
                                                            workspace.Options,
                                                            cancellationToken)
                                                            as ObjectCreationExpressionSyntax;

            var root = await document.GetSyntaxRootAsync(cancellationToken);

            var newDocumentRoot = root.ReplaceNode(objectCreation, newObjectCreationFormatted);

            document = document.WithSyntaxRoot(newDocumentRoot);

            return document;

            string prepareMembersText(string unsplitNames, Func<string, string> transform, string separator = ",")
            {
                var transformed = unsplitNames?
                                            .Split(';')
                                            .Select(x => transform(x) + separator + "\r\n")
                                            .ToArray();

                var preparedJoinedNames = string.Join("", transformed);

                var lengthWithoutLastSeparator = preparedJoinedNames.Length - (separator.Length + 2);

                preparedJoinedNames = preparedJoinedNames.Substring(0, lengthWithoutLastSeparator) + "\r\n";

                return preparedJoinedNames;
            }
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