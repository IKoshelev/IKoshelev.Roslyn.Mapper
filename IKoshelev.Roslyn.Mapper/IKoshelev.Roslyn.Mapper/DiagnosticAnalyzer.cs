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

        public static readonly string MappingDefinitionStructuralIntegrityRuleTitle = "Rolsyn.Mapper mapping has structural a problem.";
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
                            B = 15
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

        private static DiagnosticDescriptor MapperDefinitionStructuralIntegrityRule = new DiagnosticDescriptor(
                                                                                                        DiagnosticId, 
                                                                                                        MappingDefinitionStructuralIntegrityRuleTitle, 
                                                                                                        MappingDefinitionStructuralIntegrityRuleMessageFormat, 
                                                                                                        Category, 
                                                                                                        DiagnosticSeverity.Error, 
                                                                                                        isEnabledByDefault: true, 
                                                                                                        description: MappingDefinitionStructuralIntegrityRuleDescription);

        public static readonly string MappingDefinitionMissingMembergRuleTitle = "Rolsyn.Mapper mapping does not cover all members.";
        public static readonly string MappingDefinitionMissingMembergRuleMessageFormat = "Rolsyn.Mapper mapping does not cover all members. {0}";
        public static readonly string MappingDefinitionMissingMembergRuleDescription = "Rolsyn.Mapper mappings must mention every property of both "
                                                                                    + "source type and target type in at least one of: defaultMappings, customMappings or ignore lists.";

        private static DiagnosticDescriptor MapperDefinitionMissingMemberRule = new DiagnosticDescriptor(
                                                                                                DiagnosticId,
                                                                                                MappingDefinitionMissingMembergRuleTitle,
                                                                                                MappingDefinitionMissingMembergRuleMessageFormat, 
                                                                                                Category, DiagnosticSeverity.Error, 
                                                                                                isEnabledByDefault: true, 
                                                                                                description: MappingDefinitionMissingMembergRuleDescription);


        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(MapperDefinitionStructuralIntegrityRule, MapperDefinitionMissingMemberRule); } }

        public override void Initialize(AnalysisContext context)
        {
            // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            try
            {
                var syntaxReference = context.Symbol.DeclaringSyntaxReferences.Single();

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

                ExpressionMappingComponent[] mappings =
                                                        relevantObjectCreations
                                                            .Select(creationSyntax =>
                                                                        ProcessCreationSyntaxIntoParts(creationSyntax, semanticModel))
                                                            .ToArray();

                DiagnoseMissingRequiredComponentParts(mappings);

                var anyDiagnosticsEncountered = NotifyDiagnostics(context, mappings);
                if (anyDiagnosticsEncountered)
                {
                    return;
                }

                foreach (var expr in mappings)
                {
                    ParseSymbolsFromMappingsAndIgnores(expr);
                }

                anyDiagnosticsEncountered = NotifyDiagnostics(context, mappings);
                if (anyDiagnosticsEncountered)
                {
                    return;
                }

                foreach (var expr in mappings)
                {
                    CheckAndNotifyMissingMembers(context, expr);
                }
            }
            catch(Exception ex)
            {

            }
        }

        private static void CheckAndNotifyMissingMembers(SymbolAnalysisContext context, ExpressionMappingComponent expr)
        {
            CheckAndNotifyMissingMembers(
                                        expr.SourceTypeSymbol,
                                        expr.SymbolsMappedInSource,
                                        expr.SymbolsIgnoredInSource,
                                        missing =>
                                        {
                                            var diag = Diagnostic.Create(
                                                                    MapperDefinitionMissingMemberRule,
                                                                    expr.CreationExpressionSyntax.GetLocation(), 
                                                                    $"Source member {missing.Name} is not mapped.");
                                            context.ReportDiagnostic(diag);
                                        });

            CheckAndNotifyMissingMembers(
                            expr.TargetTypeSymbol,
                            expr.SymbolsMappedInTarget,
                            expr.SymbolsIgnoredInTarget,
                            missing =>
                            {
                                var diag = Diagnostic.Create(
                                                        MapperDefinitionMissingMemberRule,
                                                        expr.CreationExpressionSyntax.GetLocation(),
                                                        $"Target member {missing.Name} is not mapped.");
                                context.ReportDiagnostic(diag);
                            });
        }

        private static void CheckAndNotifyMissingMembers(INamedTypeSymbol type, ISymbol[] mapped, ISymbol[] ignored, Action<ISymbol> notifyFoSingle)
        {
            var allPublicFieldsAndProps = type
                                            .GetMembers()
                                            .Where(symbol => symbol.DeclaredAccessibility == Accessibility.Public
                                                            && IsFieldOrFullProp(symbol))
                                            .ToArray();

            var missing = allPublicFieldsAndProps
                                .Where(symbol => mapped.Contains(symbol) == false
                                                && ignored.Contains(symbol) == false)
                                .ToArray();

            foreach(var symbol in missing)
            {
                notifyFoSingle(symbol);        
            }            
        }

        private static void ParseSymbolsFromMappingsAndIgnores(ExpressionMappingComponent expr)
        {
            expr.SymbolsIgnoredInSource = ParseIgnoreList(
                                                    expr.SourceTypeSymbol,
                                                    expr.IgnoreInSource,
                                                    expr.Diagnostics);

            expr.SymbolsIgnoredInTarget = ParseIgnoreList(
                                                  expr.TargetTypeSymbol,
                                                  expr.IgnoreInTarget,
                                                  expr.Diagnostics);

            var touchedPropsInDefault = ParseTouchedPropsFromMapping(
                                                    expr.SourceTypeSymbol,
                                                    expr.TargetTypeSymbol,
                                                    expr.DefaultMappings,
                                                    expr.Diagnostics);

            var touchedPropsInCustom = ParseTouchedPropsFromMapping(
                                                    expr.SourceTypeSymbol,
                                                    expr.TargetTypeSymbol,
                                                    expr.CustomMappings,
                                                    expr.Diagnostics);

            expr.SymbolsMappedInSource = touchedPropsInDefault.sourceProps
                                            .Union(touchedPropsInCustom.sourceProps)
                                            .ToArray();

            expr.SymbolsMappedInTarget = touchedPropsInDefault.targetProps
                                            .Union(touchedPropsInCustom.targetProps)
                                            .ToArray();
        }

        private static bool NotifyDiagnostics(SymbolAnalysisContext context, ExpressionMappingComponent[] components)
        {
            var structuralProblems = components
                                         .SelectMany(component => component.Diagnostics)
                                         .ToArray();

            if (structuralProblems.Any() == false)
            {          
                return false;
            }

            foreach (var problem in structuralProblems)
            {
                context.ReportDiagnostic(problem);
            }

            return true;
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

                if(component.SourceTypeSyntax == null || component.SourceTypeSymbol == null)
                {
                    var diag = Diagnostic.Create(MapperDefinitionStructuralIntegrityRule, component.CreationExpressionSyntax.GetLocation(), "Source type could not be resolved.");
                    component.Diagnostics.Add(diag);
                }

                if (component.TargetTypeSyntax == null || component.TargetTypeSymbol == null)
                {
                    var diag = Diagnostic.Create(MapperDefinitionStructuralIntegrityRule, component.CreationExpressionSyntax.GetLocation(), "Target type could not be resolved.");
                    component.Diagnostics.Add(diag);
                }
            }
        }

        public static ObjectCreationExpressionSyntax[] GetRelevantObjectCreations(SymbolAnalysisContext context, ObjectCreationExpressionSyntax[] expressionMappingComponentsConstructor, SemanticModel semanticModel)
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
            component.SourceTypeSymbol = semanticModel.GetSymbolInfo(component.SourceTypeSyntax).Symbol as INamedTypeSymbol;
            component.TargetTypeSyntax = typeArgSyntaxes[1];
            component.TargetTypeSymbol = semanticModel.GetSymbolInfo(component.TargetTypeSyntax).Symbol as INamedTypeSymbol;

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

        public static bool IsFieldOrFullProp(ISymbol symbol)
        {
           if(symbol is IFieldSymbol)
            {
                return true;
            }

            var propSymbol = symbol as IPropertySymbol;
            if(propSymbol == null)
            {
                return false;
            }

            var hasGet = propSymbol.GetMethod != null;
            var hasSet = propSymbol.SetMethod != null;

            return hasGet && hasSet;
        }

        public static ISymbol[] GetPublicFieldsAndProperties(INamedTypeSymbol symbol)
        {
            var members = symbol
                            .GetMembers()
                            .Where(subSymbol =>
                                    subSymbol.DeclaredAccessibility == Accessibility.Public
                                    && IsFieldOrFullProp(subSymbol))
                            .ToArray();

            return members;
        }

        public static ISymbol[] ParseIgnoreList(INamedTypeSymbol ownerType, ArrayCreationExpressionSyntax arraySyntax, List<Diagnostic> diagnostics)
        {         
            try
            {
                var lambdas = arraySyntax
                                     ?.Initializer
                                     .ChildNodes()
                                     .ToArray() ?? new SyntaxNode[0];

                var impropperLambdas = lambdas
                                            .Where(x => x.Fits(SyntaxKind.SimpleLambdaExpression) == false)
                                            .ToArray();

                if (impropperLambdas.Any())
                {
                    foreach (var impropperLambda in impropperLambdas)
                    {
                        var lambdaText = impropperLambda.GetText().ToString().Trim();
                        var diag = Diagnostic.Create(MapperDefinitionStructuralIntegrityRule, arraySyntax.GetLocation(),
                                                    $"Array contains impropper lambdas {lambdaText}");
                        diagnostics.Add(diag);
                    }
                    return new ISymbol[0];
                }

                var propperLambdas = lambdas.OfType<SimpleLambdaExpressionSyntax>();

                var memberSymbols = propperLambdas
                                        .Select(lambda => GetFieldOrPropertySymbolFromSimpleLambda(
                                                                                                ownerType,
                                                                                                lambda,
                                                                                                diagnostics))
                                        .ToArray();

                return memberSymbols;
            }
            catch
            {
                var arrayText = arraySyntax.GetText().ToString().Trim();
                var diag = Diagnostic.Create(MapperDefinitionStructuralIntegrityRule, arraySyntax.GetLocation(),
                                            $"Could not process array: {arrayText}");
                diagnostics.Add(diag);
                return new ISymbol[0];
            }
        }

        public static 
            (ISymbol[] sourceProps, ISymbol[] targetProps) 
                ParseTouchedPropsFromMapping(INamedTypeSymbol sourceType, 
                                             INamedTypeSymbol targetType, 
                                             ParenthesizedLambdaExpressionSyntax lambda, 
                                             List<Diagnostic> diagnostics)
        {
            try
            {
                var sourceIdentifierName = lambda
                                            .ChildNodes().OfType<ParameterListSyntax>().Single()
                                            .ChildNodes().OfType<ParameterSyntax>().Single()
                                            .ChildTokens().Single().ToString().Trim();

                var assignments = lambda
                                    .ChildNodes().OfType<ObjectCreationExpressionSyntax>().Single()
                                    .ChildNodes().OfType<InitializerExpressionSyntax>().Single()
                                    .ChildNodes().OfType<AssignmentExpressionSyntax>()
                                    .ToArray();

                var touchedTargetProps = assignments
                                                .Select(x => x.ChildNodes().OfType<IdentifierNameSyntax>().First())
                                                .Select(x => x.GetText().ToString().Trim())
                                                .Select(name => targetType.GetMembers(name).Single())
                                                .ToArray();

                var touchedSourceProps = lambda
                                            .DescendantNodes().OfType<MemberAccessExpressionSyntax>()
                                            .Where(node => node.Expression.Fits(SyntaxKind.IdentifierName)
                                                            && node.Expression.ToString().Trim() == sourceIdentifierName)
                                            .Select(x => x.Name.GetText().ToString().Trim())
                                            .Select(name => sourceType.GetMembers(name).Single())
                                            .ToArray();

                return (touchedSourceProps, touchedTargetProps);
            }
            catch
            {
                var lambdaText = lambda.GetText().ToString().Trim();
                var diag = Diagnostic.Create(MapperDefinitionStructuralIntegrityRule, lambda.GetLocation(),
                                            $"Could not process lambda: {lambdaText}");
                diagnostics.Add(diag);
                return (new ISymbol[0], new ISymbol[0]);
            }           
        }

            public static ISymbol GetFieldOrPropertySymbolFromSimpleLambda(INamedTypeSymbol ownerType, SimpleLambdaExpressionSyntax lambda, List<Diagnostic> diagnostics)
        {
            var lambdaText = lambda.GetText().ToString().Trim();
            string memberName = null;

            try
            {
                memberName = lambda
                                .ChildNodes()
                                .OfType<MemberAccessExpressionSyntax>()
                                .Single()
                                .Name
                                .GetText()
                                .ToString()
                                .Trim();
            }
            catch
            {
                var diag = Diagnostic.Create(MapperDefinitionStructuralIntegrityRule, lambda.GetLocation(), 
                                            $"Lambda must be simple, i.e. x => x.A; Could not process lambda {lambdaText}");
                diagnostics.Add(diag);
            }

            if(memberName == null)
            {
                return null;
            }

            ISymbol memberSymbol = null; 
            try
            { 
                memberSymbol = ownerType
                                    .GetMembers(memberName)
                                    .Single();
            }
            catch
            {            
                var diag = Diagnostic.Create(MapperDefinitionStructuralIntegrityRule, lambda.GetLocation(), 
                                                $"Could not find single member by name {memberName} in type {ownerType.Name}");
                diagnostics.Add(diag);
            }

            if(IsFieldOrFullProp(memberSymbol))
            {
                return memberSymbol;
            }

            var diag2 = Diagnostic.Create(MapperDefinitionStructuralIntegrityRule, lambda.GetLocation(), 
                                          $"{ownerType.Name} resolves to symbol {memberSymbol.GetType().ToString()}. " +
                                          $"Only fields and properties allowed.");
            diagnostics.Add(diag2);

            return null;
        }
    }

    public class ExpressionMappingComponent
    {
        public List<Diagnostic> Diagnostics = new List<Diagnostic>();

        public ObjectCreationExpressionSyntax CreationExpressionSyntax { get; set; }
        public IdentifierNameSyntax SourceTypeSyntax { get; set; }
        public IdentifierNameSyntax TargetTypeSyntax { get; set; }
        public INamedTypeSymbol SourceTypeSymbol { get; set; }
        public INamedTypeSymbol TargetTypeSymbol { get; set; }

        public ParenthesizedLambdaExpressionSyntax DefaultMappings { get; set; }
        public ParenthesizedLambdaExpressionSyntax CustomMappings { get; set; }
        public ArrayCreationExpressionSyntax IgnoreInSource { get; set; }
        public ArrayCreationExpressionSyntax IgnoreInTarget { get; set; }

        public ISymbol[] SymbolsIgnoredInSource { get; set; }
        public ISymbol[] SymbolsIgnoredInTarget { get; set; }
        public ISymbol[] SymbolsMappedInSource { get; set; }
        public ISymbol[] SymbolsMappedInTarget { get; set; }
    }
}
