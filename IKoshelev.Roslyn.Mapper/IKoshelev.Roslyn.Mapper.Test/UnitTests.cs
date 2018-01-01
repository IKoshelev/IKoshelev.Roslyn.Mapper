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
    public class Foo
    {
        public int A { get; set; }
        public int B { get; set; }
        public int C { get; set; }
        public int[] D { get; set; }
        public Bar E { get; set; }
        public int Ignore1 {get;set;}
    }

    public class Bar
    {
        public Bar() { }

        public Bar(int a)
        {
            A = a;
        }

        public int A { get; set; }
        public int B { get; set; }
        public int C { get; set; }
        public Bar E { get; set; }
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
                var test = new ExpressionMapper<Foo, Bar>(
                    new ExpressionMappingComponents<Foo, Bar>(
                        (source) => new Bar()
                        {
                            A = source.A,
                            B = source.B,
                        },
                        customMappings: (source) => new Bar()
                        {
                            C = 15
                        },
                        sourceIgnoredProperties: new Expression<Func<Foo, object>>[]
                        {
                            x => x.Ignore1
                        },
                        targetIgnoredProperties: new Expression<Func<Foo, object>>[]
                        {
                            x => x.Ignore2
                        }));
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
                var test = new ExpressionMapper<Foo, Bar>(
                    new ExpressionMappingComponents<Foo, Bar>(
                        (source) => new Bar()
                        {
                            A = source.A,
                            B = source.B,
                        }));
            }
        }
    }" + ClassDefinitions;

            VerifyCSharpDiagnostic(test);
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
                var bad =  new Expression<Func<Foo, object>>[0];

                var test = new ExpressionMapper<Foo, Bar>(
                    new ExpressionMappingComponents<Bad1, Bad2>(
                        null,
                        customMappings: null,
                        sourceIgnoredProperties: bad,
                        targetIgnoredProperties: null));
            }
        }
    }" + ClassDefinitions;

            DiagnosticResult Diagnostics(string message, int line, int column)
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
                Diagnostics("\"defaultMappings\" not found.", 19,21),
                Diagnostics("Source type could not be resolved.", 19, 21),
                Diagnostics("Target type could not be resolved.", 19, 21),
                Diagnostics("Argument for \"defaultMappings\" could not be processed.", 20, 25),
                Diagnostics("Argument for \"customMappings\" could not be processed.", 21, 25),
                Diagnostics("Argument for \"sourceIgnoredProperties\" could not be processed.", 22, 25),
                Diagnostics("Argument for \"targetIgnoredProperties\" could not be processed.", 23, 25));

    //        var fixtest = @"
    //using System;
    //using System.Collections.Generic;
    //using System.Linq;
    //using System.Text;
    //using System.Threading.Tasks;
    //using System.Diagnostics;

    //namespace ConsoleApplication1
    //{
    //    class TYPENAME
    //    {   
    //    }
    //}";
    //        VerifyCSharpFix(test, fixtest);
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