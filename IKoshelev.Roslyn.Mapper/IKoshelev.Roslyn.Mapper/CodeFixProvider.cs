﻿using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SF = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

namespace IKoshelev.Roslyn.Mapper
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(IKoshelevRoslynMapperCodeFixProvider)), Shared]
    public class IKoshelevRoslynMapperCodeFixProvider : CodeFixProvider
    {
        public const string TitleRegenerateDefaultMappings = "Regenerate defaultMappings.";
        public const string TitleGenerateMappingArguments = "Generate mapping arguments.";
        public const string TitleAddIgnoreMembersSource = "Add unmapped source members to ignore list.";
        public const string TitleAddIgnoreMembersTarget = "Add unmapped target members to ignore list.";

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

            var passedProperties = diagnostic.Properties;
            if (passedProperties?.Any() != true)
            {
                return;
            }

            passedProperties.TryGetValue(
                                    IKoshelevRoslynMapperAnalyzer.CodeFixActionTypeDictKey,
                                    out string codeFixAction);

            if(codeFixAction == null)
            {
                return;
            }

            var root = await context
                                .Document
                                .GetSyntaxRootAsync(context.CancellationToken)
                                .ConfigureAwait(false);

            if (codeFixAction == IKoshelevRoslynMapperAnalyzer.CodeFixActionGenerateDefaultMappings)
            {
                var mappingCreation = root
                        .FindToken(diagnosticSpan.Start)
                        .Parent
                        .AncestorsAndSelf().OfType<ObjectCreationExpressionSyntax>().FirstOrDefault();

                passedProperties.TryGetValue(
                                IKoshelevRoslynMapperAnalyzer.AutomappableMembersDictKey,
                                out string automappableMembers);

                passedProperties.TryGetValue(
                                IKoshelevRoslynMapperAnalyzer.NonAutomappableSourceMembersDictKey,
                                out string nonAutomappableSourceMembers);

                passedProperties.TryGetValue(
                                IKoshelevRoslynMapperAnalyzer.NonAutomappableTargetMembersDictKey,
                                out string nonAutomappableTargetMembers);

                passedProperties.TryGetValue(
                                IKoshelevRoslynMapperAnalyzer.SourceTypeNameDictKey,
                                out string sourceTypeName);

                passedProperties.TryGetValue(
                                IKoshelevRoslynMapperAnalyzer.TargetTypeNameDictKey,
                                out string targetTypeName);


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

            if (codeFixAction == IKoshelevRoslynMapperAnalyzer.CodeFixActionRegenerateDefaultMappings)
            {
                var lambda = root
                                .FindToken(diagnosticSpan.Start)
                                .Parent
                                .AncestorsAndSelf().OfType<LambdaExpressionSyntax>().FirstOrDefault();

                passedProperties.TryGetValue(
                                    IKoshelevRoslynMapperAnalyzer.AutomappableMembersDictKey,
                                    out string automappableMembers);

                context.RegisterCodeFix(
                            CodeAction.Create(
                                title: TitleRegenerateDefaultMappings,
                                createChangedDocument: c => RegenerateDefaultMappingsAsync(context.Document, lambda, automappableMembers, c),
                                equivalenceKey: TitleRegenerateDefaultMappings),
                            diagnostic);
            }

            if (codeFixAction == IKoshelevRoslynMapperAnalyzer.CodeFixActionAddUnmappedMembersToIgnore)
            {
                var mappingCreation = root
                                        .FindToken(diagnosticSpan.Start)
                                        .Parent
                                        .AncestorsAndSelf().OfType<ObjectCreationExpressionSyntax>().FirstOrDefault();

                AddAppendIgnoreMembersFixesIfPossible(
                                                    context,
                                                    diagnostic,
                                                    diagnosticSpan,
                                                    passedProperties,
                                                    mappingCreation,
                                                    root);
            }
        }

        private void AddAppendIgnoreMembersFixesIfPossible(
                                            CodeFixContext context, 
                                            Diagnostic diagnostic, 
                                            TextSpan diagnosticSpan, 
                                            ImmutableDictionary<string, string> membersClassification,
                                            ObjectCreationExpressionSyntax mappingsCreation,
                                            SyntaxNode root)
        {
            var additionalLocation = diagnostic.AdditionalLocations?.FirstOrDefault();

            ObjectCreationExpressionSyntax existingIgnore = null;
            if (additionalLocation != null)
            {
                existingIgnore = root
                                    .FindToken(additionalLocation.SourceSpan.Start)
                                    .Parent
                                    .AncestorsAndSelf().OfType<ObjectCreationExpressionSyntax>().FirstOrDefault();
            }

            membersClassification.TryGetValue(
                 IKoshelevRoslynMapperAnalyzer.SourceMembersToIgnoreDictKey,
                 out string sourceUnmappedMembers);

            if (sourceUnmappedMembers != null)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: TitleAddIgnoreMembersSource,
                        createChangedDocument: c => AppendIgnoreList(context.Document, mappingsCreation, existingIgnore, "source", sourceUnmappedMembers, c),
                        equivalenceKey: TitleRegenerateDefaultMappings),
                    diagnostic);
            }

            membersClassification.TryGetValue(
                 IKoshelevRoslynMapperAnalyzer.TargeMembersToIgnoreDictKey,
                 out string targetUnmappedMembers);

            if (targetUnmappedMembers != null)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: TitleAddIgnoreMembersSource,
                        createChangedDocument: c => AppendIgnoreList(context.Document, mappingsCreation, existingIgnore, "target", targetUnmappedMembers, c),
                        equivalenceKey: TitleRegenerateDefaultMappings),
                    diagnostic);
            }
        }

        private async Task<Document> AppendIgnoreList(
            Document document,
            ObjectCreationExpressionSyntax wholeMappingCreation,
            ObjectCreationExpressionSyntax existingIgnoreList,
            string paramName,
            string membersToAppend,
            CancellationToken cancellationToken)
        {
            var parsedMembersToAppend = membersToAppend.Split(';');

            var lambdasToAppend = parsedMembersToAppend
                                        .Select(x => $"({paramName}) => {paramName}.{x}")
                                        .Select(x => SF.ParseExpression(x) as LambdaExpressionSyntax)
                                        .Select(x => SF.Argument(x).WithLeadingTrivia(SF.LineFeed))
                                        .ToArray();

            if(existingIgnoreList == null)
            {
                throw new NotImplementedException();
            }

            var existingIgnoreLambdas = existingIgnoreList
                                                .ArgumentList
                                                .Arguments
                                                .Select(x => x.Expression.WithoutTrivia())
                                                .Select(x => SF.Argument(x).WithLeadingTrivia(SF.LineFeed))
                                                .ToArray();

            var combinedLambdas = new SeparatedSyntaxList<ArgumentSyntax>();
            combinedLambdas = combinedLambdas.AddRange(existingIgnoreLambdas);
            combinedLambdas = combinedLambdas.AddRange(lambdasToAppend);

            var newArgumentList = SF.ArgumentList(combinedLambdas);

            var newIgnoreList = existingIgnoreList
                                        .WithArgumentList(newArgumentList);

             document = await ParseSyntaxTextAndReplaceNode(document, existingIgnoreList, newIgnoreList, cancellationToken);

            return document;
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

            document = await ParseSyntaxTextAndReplaceNode(document, objectCreation, newObjectCreationRaw, cancellationToken);

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
            LambdaExpressionSyntax lambda,
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

            document = await ParseSyntaxTextAndReplaceNode(document, lambda, newRawLambda, cancellationToken);

            return document;
        }

        private static async Task<Document> ParseSyntaxTextAndReplaceNode<T>
            (Document document, T oldNode, T newNode, CancellationToken cancellationToken)
            where T : SyntaxNode
        {
            newNode = newNode.WithAdditionalAnnotations(Formatter.Annotation);

            var workspace = document.Project.Solution.Workspace;

            var newObjectCreationFormatted = Formatter.Format(
                                                            newNode,
                                                            Formatter.Annotation,
                                                            workspace,
                                                            workspace.Options,
                                                            cancellationToken)
                                                            as ObjectCreationExpressionSyntax;

            var root = await document.GetSyntaxRootAsync(cancellationToken);

            var newDocumentRoot = root.ReplaceNode(oldNode, newNode);

            document = document.WithSyntaxRoot(newDocumentRoot);
            return document;
        }
    }
}