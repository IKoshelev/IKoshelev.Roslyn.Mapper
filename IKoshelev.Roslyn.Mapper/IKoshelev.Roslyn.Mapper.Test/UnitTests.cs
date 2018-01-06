using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using TestHelper;
using IKoshelev.Roslyn.Mapper;

namespace IKoshelev.Roslyn.Mapper.Test
{
    [TestClass]
    public class UnitTest : CodeFixVerifier
    {
        public const string ClassDefinitions =
@"
namespace ConsoleApplication1
{
    public class Src
    {
        public int A { get; set; }
        public int B { get; set; }       
        public int Ignore1 {get;set;}
    }

    public class Trg
    {
        public Trg() { }

        public Trg(int a)
        {
            A = a;
        }

        public int A { get; set; }
        public int B { get; set; }
        public int C { get; set; }
        public int Ignore2 {get;set;}
    }
}";

        //No diagnostics expected to show up
        [TestMethod]
        public void OnEmptyFileNoDiagnostics()
        {
            var test = @"";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void OnPropperStructureEverythingIsOk()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;
    using IKoshelev.Mapper; 

    namespace ConsoleApplication1
    {
        class Test
        {
            public void Test()
            {
                var test = new ExpressionMapper<Src, Trg>(
                    new ExpressionMappingComponents<Src, Trg>(
                        (source) => new Trg()
                        {
                            A = source.A,
                            B = source.B,
                        },
                        customMappings: (source) => new Trg()
                        {
                            C = 15
                        },
                        sourceIgnoredProperties: new IgnoreList<Src>(
                            x => x.Ignore1
                        ),
                        targetIgnoredProperties: new IgnoreList<Trg>(
                            x => x.Ignore2
                        )));
            }
        }
    }" + ClassDefinitions;

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void OnlyDefaultMappingsAreRequired()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;
    using IKoshelev.Mapper; 

    namespace ConsoleApplication1
    {
        class Test
        {
            public void Test()
            {
                var test = new ExpressionMapper<Src, Trg>(
                    new ExpressionMappingComponents<Src, Trg>(
                        (source) => new Trg()
                        {
                            A = source.A,
                            B = source.B,
                        }));
            }
        }
    }" + ClassDefinitions;

            VerifyCSharpDiagnostic(test,
                MappingProblem("Source member Ignore1 is not mapped.", 17,21),
                MappingProblem("Target member C is not mapped.", 17, 21),
                MappingProblem("Target member Ignore2 is not mapped.", 17, 21));
        }

        [TestMethod]
        public void OnStructuralProblemItIsReporpted()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;
    using IKoshelev.Mapper; 

    namespace ConsoleApplication1
    {
        class Test
        {
            public void Test()
            {
                var bad =  new Expression<Func<Src, object>>[0];

                var test = new ExpressionMapper<Src, Trg>(
                    new ExpressionMappingComponents<Bad1, Bad2>(
                        null,
                        customMappings: null,
                        sourceIgnoredProperties: bad,
                        targetIgnoredProperties: null));
            }
        }
    }" + ClassDefinitions;

            DiagnosticResult StructuralProblem(string message, int line, int column)
            {
                return new DiagnosticResult
                {
                    Id = "IKoshelevRoslynMapper",
                    Message = String.Format(IKoshelevRoslynMapperAnalyzer.MappingDefinitionStructuralIntegrityRuleMessageFormat, message),
                    Severity = DiagnosticSeverity.Error,
                    Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", line, column)
                        }
                };
            }

            VerifyCSharpDiagnostic(test, 
                StructuralProblem("\"defaultMappings\" not found.", 19,21),
                StructuralProblem("Source type could not be resolved.", 19, 21),
                StructuralProblem("Target type could not be resolved.", 19, 21),
                StructuralProblem("Argument for \"defaultMappings\" could not be processed.", 20, 25),
                StructuralProblem("Argument for \"customMappings\" could not be processed.", 21, 25),
                StructuralProblem("Argument for \"sourceIgnoredProperties\" could not be processed.", 22, 25),
                StructuralProblem("Argument for \"targetIgnoredProperties\" could not be processed.", 23, 25));
        }

        [TestMethod]
        public void WhenMissingMappingsFoundAFixWillBeProposedForCompatibleMembers()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;
    using IKoshelev.Mapper; 

    namespace ConsoleApplication1
    {
        class Test
        {
            public void Test()
            {
                var test = new ExpressionMapper<Src, Trg>(
                    new ExpressionMappingComponents<Src, Trg>(
                        (source) => new Trg()
                        {
                        },
                        customMappings: (source) => new Trg()
                        {
                            C = 15
                        },
                        sourceIgnoredProperties: new IgnoreList<Src>(
                            x => x.Ignore1
                        ),
                        targetIgnoredProperties: new IgnoreList<Trg>(
                            x => x.Ignore2
                        )));
            }
        }
    }" + ClassDefinitions;

            VerifyCSharpDiagnostic(test,
                MappingProblem("Some membmers with identical names are not mapped. Please choose 'Regenerate defaultMappings.'"+ 
                                " or manually handle missing members: A;B.", 18, 25));

            var fixTest = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;
    using IKoshelev.Mapper; 

    namespace ConsoleApplication1
    {
        class Test
        {
            public void Test()
            {
                var test = new ExpressionMapper<Src, Trg>(
                    new ExpressionMappingComponents<Src, Trg>(
(source) => new Trg()
{
    A = source.A,
    B = source.B,
},
                        customMappings: (source) => new Trg()
                        {
                            C = 15
                        },
                        sourceIgnoredProperties: new IgnoreList<Src>(
                            x => x.Ignore1
                        ),
                        targetIgnoredProperties: new IgnoreList<Trg>(
                            x => x.Ignore2
                        )));
            }
        }
    }" + ClassDefinitions;

            VerifyCSharpFix(test, fixTest);

        }

        DiagnosticResult MappingProblem(string message, int line, int column)
        {
            return new DiagnosticResult
            {
                Id = "IKoshelevRoslynMapper",
                Message = String.Format(IKoshelevRoslynMapperAnalyzer.MappingDefinitionMissingMembergRuleMessageFormat, message),
                Severity = DiagnosticSeverity.Error,
                Locations =
                new[] {
                            new DiagnosticResultLocation("Test0.cs", line, column)
                    }
            };
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new IKoshelevRoslynMapperCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new IKoshelevRoslynMapperAnalyzer();
        }
    }
}