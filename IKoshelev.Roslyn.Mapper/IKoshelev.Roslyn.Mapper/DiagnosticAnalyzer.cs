using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Roslyn.Syntax.Util;
using IKoshelev.Mapper;

namespace IKoshelev.Roslyn.Mapper
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class IKoshelevRoslynMapperAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "IKoshelevRoslynMapper";

        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Localizing%20Analyzers.md for more on localization
        public static readonly string MappingDefinitionStructuralIntegrityRuleTitle = "Rolsyn.Mapper mapping has a problem. T";
        public static readonly string MappingDefinitionStructuralIntegrityRuleMessageFormat = "Roslyn mapping has a structural problem. {0}";
        public static readonly string MappingDefinitionStructuralIntegrityRuleDescription = @"Roslyn mapper definitions must follow strict structure.
Arguments can be passed by ordinal, by name or skipped. 
If they are present - they must be exactly inline defined lambdas or lambda arrays, nothing else.
  var mapper = new ExpressionMapper<Foo, Bar>(
                new ExpressionMappingComponents<Foo, Bar>(
                        (source) => new Bar() 
                        {
                            A = source.A
                        },
                        customMappings: (source) => new Bar()   // optional
                        {
                            C = 15
                        },
                        sourceIgnoredProperties: new Expression<Func<Foo, object>>[]    // optional
                        {
                            x => x.Ignore1
                        },
                        targetIgnoredProperties: new Expression<Func<Foo, object>>[]    // optional
                        {
                            x => x.Ignore2
                        }));
";
        private const string Category = "Roslyn.Mapper";

        private static DiagnosticDescriptor MapperDefinitionStructuralIntegrityRule = new DiagnosticDescriptor(DiagnosticId, MappingDefinitionStructuralIntegrityRuleTitle, MappingDefinitionStructuralIntegrityRuleMessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: MappingDefinitionStructuralIntegrityRuleDescription);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(MapperDefinitionStructuralIntegrityRule); } }

        public override void Initialize(AnalysisContext context)
        {
            // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            // TODO: Replace the following code with your own analysis, generating Diagnostic objects for any issues you find
            //var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

            var syntaxReference = context.Symbol.DeclaringSyntaxReferences.Single();

            //var root = syntaxReference.SyntaxTree.GetRoot();

            var expressionMappingComponentsConstructor = syntaxReference
                                                            .GetSyntax()
                                                            .DescendantNodes()
                                                            .OfType<ObjectCreationExpressionSyntax>()
                                                            .ToArray();

            var semanticModel = context.Compilation.GetSemanticModel(syntaxReference.SyntaxTree, false);
            ObjectCreationExpressionSyntax[] relevantObjectCreations = GetRelevantObjectCreations(context, expressionMappingComponentsConstructor, semanticModel);

            if (relevantObjectCreations.Any() == false)
            {
                return;
            }

            ExpressionMappingComponent[] expressionMappings = 
                                                    relevantObjectCreations
                                                        .Select(creationSyntax =>
                                                                    ProcessCreationSyntaxIntoParts(creationSyntax, semanticModel))
                                                        .ToArray();

            DiagnoseMissingRequiredComponentParts(expressionMappings);

            var structuralProblems = expressionMappings
                                            .SelectMany(component => component.Diagnostics)
                                            .ToArray();

            if (structuralProblems.Any())
            {
                foreach(var problem in structuralProblems)
                {
                    context.ReportDiagnostic(problem);
                }
                return;
            }

            // Find just those named type symbols with names containing lowercase letters.
            //if (namedTypeSymbol.Name.ToCharArray().Any(char.IsLower))
            //{
            //    // For all such symbols, produce a diagnostic.
            //    var diagnostic = Diagnostic.Create(Rule, namedTypeSymbol.Locations[0], namedTypeSymbol.Name);

            //    context.ReportDiagnostic(diagnostic);
            //}
        }

        private static void DiagnoseMissingRequiredComponentParts(ExpressionMappingComponent[] components)
        {
            foreach(var component in components)
            {
                if(component.DefaultMappings == null)
                {
                    var diag = Diagnostic.Create(MapperDefinitionStructuralIntegrityRule, component.CreationExpressionSyntax.GetLocation(), "\"defaultMappings\" not found.");
                    component.Diagnostics.Add(diag);
                }
            }
        }

        private static ObjectCreationExpressionSyntax[] GetRelevantObjectCreations(SymbolAnalysisContext context, ObjectCreationExpressionSyntax[] expressionMappingComponentsConstructor, SemanticModel semanticModel)
        {
            var unboundGenericTypeName = typeof(ExpressionMappingComponents<,>).FullName;

            var mappingComponentsSymbol = context.Compilation.GetTypeByMetadataName(unboundGenericTypeName);

            var relevantObjectCreations = expressionMappingComponentsConstructor
                                        .Where(typeSyntax => IsTypeBoundGenericOf(typeSyntax, semanticModel, unboundGenericTypeName))
                                        .ToArray();

            return relevantObjectCreations;
        }

        private static ExpressionMappingComponent
                            ProcessCreationSyntaxIntoParts(
                                            ObjectCreationExpressionSyntax creationSyntax, 
                                            SemanticModel semanticModel)
        {
            var component = new ExpressionMappingComponent();
            component.CreationExpressionSyntax = creationSyntax;

            var typeArguments = creationSyntax
                                        .Type
                                        .ChildNodes()
                                        .OfType<TypeArgumentListSyntax>()
                                        .Single();

            var typeArgSyntaxes = typeArguments
                                        .ChildNodes()
                                        .OfType<IdentifierNameSyntax>()
                                        .ToArray();

            component.SourceTypeSyntax = typeArgSyntaxes[0];
            component.SourceTypeSymbol = semanticModel.GetSymbolInfo(component.SourceTypeSyntax);
            component.TargetTypeSyntax = typeArgSyntaxes[1];
            component.TargetTypeSymbol = semanticModel.GetSymbolInfo(component.TargetTypeSyntax);

            var arguments = creationSyntax.ArgumentList.ChildNodes().ToArray();

            FillInArguments(component, arguments);

            return component;
        }

        private static void FillInArguments(ExpressionMappingComponent component, SyntaxNode[] arguments)
        {
            component.DefaultMappings = TryGetArgumentValueSyntax<ParenthesizedLambdaExpressionSyntax>(0, "defaultMappings", arguments, component.Diagnostics);
            component.CustomMappings = TryGetArgumentValueSyntax<ParenthesizedLambdaExpressionSyntax>(1, "customMappings", arguments, component.Diagnostics);
            component.IgnoreInSource = TryGetArgumentValueSyntax<ArrayCreationExpressionSyntax>(2, "sourceIgnoredProperties", arguments, component.Diagnostics);
            component.IgnoreInTarget = TryGetArgumentValueSyntax<ArrayCreationExpressionSyntax>(3, "targetIgnoredProperties", arguments, component.Diagnostics);
        }

        private static T TryGetArgumentValueSyntax<T>(int index,string name, SyntaxNode[] arguments, List<Diagnostic> diagnostics) where T : class
        {
            var argSyntax = TryGetArgumentIfOrdinal(index, arguments)
                            ?? TryGetArgumentByName(name, arguments);

            var argValueSyntax = argSyntax?.ChildNodes().LastOrDefault();

            var argValueSyntaxCast = argValueSyntax as T;

            var argumentValueIsOfUnexpectedType = (argSyntax != null && argValueSyntaxCast == null);

            if (argumentValueIsOfUnexpectedType)
            {
                var diag = Diagnostic.Create(MapperDefinitionStructuralIntegrityRule, argSyntax.GetLocation(), $"Argument for \"{name}\" could not be processed.");
                diagnostics.Add(diag);
            }

            return argValueSyntaxCast;
        }

       private static  SyntaxNode TryGetArgumentIfOrdinal(int index, SyntaxNode[] arguments)
       {
            if (arguments.Length < (index + 1))
            {
                return null;
            }

            var arg = arguments[index];

            var isPassedByName = arg.ChildNodes().Any(x => x.Fits(SyntaxKind.NameColon));

            if (isPassedByName)
            {
                return null;
            }

            return arg;
       }

        private static SyntaxNode TryGetArgumentByName(string name, SyntaxNode[] arguments)
        {
            var namedArguments = arguments
                                        .Where(arg => arg
                                                        .ChildNodes()
                                                        .Any(x => x.Fits(SyntaxKind.NameColon)));

            var argWithMatchingName = 
                    namedArguments.Where(x => x
                                            .ChildNodes().OfType<NameColonSyntax>().Single()
                                            .ChildNodes().OfType<IdentifierNameSyntax>().Single()
                                            .GetText().ToString().Trim() == name)
                                  .SingleOrDefault();

            return argWithMatchingName;
        }

        private static bool IsTypeBoundGenericOf(
                                        ObjectCreationExpressionSyntax objectCreateSyntaxt,
                                        SemanticModel semanticModel, 
                                        string unboundGenericTypeFullName)
        {
            //todo better way to check deriving from generic base
            var symbolMatch = semanticModel.GetSymbolInfo(objectCreateSyntaxt.Type);
            var symbol = symbolMatch.Symbol;
            if (symbol == null)
            {
                return false;
            }
            var symbolFullName = $"{symbol.ContainingNamespace.ToString()}.{symbol.MetadataName}";
            return unboundGenericTypeFullName == symbolFullName;
        }
    }

    public class ExpressionMappingComponent
    {
        public List<Diagnostic> Diagnostics = new List<Diagnostic>();

        public ObjectCreationExpressionSyntax CreationExpressionSyntax { get; set; }
        public IdentifierNameSyntax SourceTypeSyntax { get; set; }
        public IdentifierNameSyntax TargetTypeSyntax { get; set; }
        public SymbolInfo SourceTypeSymbol { get; set; }
        public SymbolInfo TargetTypeSymbol { get; set; }

        public ParenthesizedLambdaExpressionSyntax DefaultMappings { get; set; }
        public ParenthesizedLambdaExpressionSyntax CustomMappings { get; set; }
        public ArrayCreationExpressionSyntax IgnoreInSource { get; set; }
        public ArrayCreationExpressionSyntax IgnoreInTarget { get; set; }
    }
}
