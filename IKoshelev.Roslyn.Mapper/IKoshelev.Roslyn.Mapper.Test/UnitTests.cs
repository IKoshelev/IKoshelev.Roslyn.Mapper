using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using TestHelper;
using IKoshelev.Roslyn.Mapper;
using System.Linq.Expressions;
using System.Collections.Generic;

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
    }

namespace ConsoleApplication1
    {
        public class Src
        {
            public int A { get; set; }
            public int B { get; set; }
        }

        public class Trg
        {
            public int A { get; set; }
            public int B { get; set; }
        }
    }";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void WhenMembersAreMissingFromIgnoreACodeFixWillBeOfferedToAddThem_AddToEmptyIgnoreListCase()
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
                        (source) => new Trg()
                        {
                            C = 10
                        },
                        sourceIgnoredProperties: new IgnoreList<Src>(
                            x => x.Ignore1
                        ),
                        targetIgnoredProperties: new IgnoreList<Trg>(                           
                        )));
            }
        }
    }" + ClassDefinitions;

            VerifyCSharpDiagnostic(test,
                MappingProblem("Target member Ignore2 is not mapped.", 17, 21, (30, 50)));

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
                        (source) => new Trg()
                        {
                            C = 10
                        },
                        sourceIgnoredProperties: new IgnoreList<Src>(
                            x => x.Ignore1
                        ),
                        targetIgnoredProperties: new IgnoreList<Trg>(
(target) => target.Ignore2)));
            }
        }
    }" + ClassDefinitions;

            VerifyCSharpFix(test, fixTest);
        }

        [TestMethod]
        public void WhenMembersAreMissingFromIgnoreACodeFixWillBeOfferedToAddThem_AddToNonEmptyIgnoreListCase()
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
                        },
                        (source) => new Trg()
                        {
                            C = 10
                        },
                        sourceIgnoredProperties: new IgnoreList<Src>(
                            x => x.Ignore1,
                            x => x.B
                        ),
                        targetIgnoredProperties: new IgnoreList<Trg>(
x => x.B));
            }
        }
    }" + ClassDefinitions;

            VerifyCSharpDiagnostic(test,
                MappingProblem("Target member Ignore2 is not mapped.", 17, 21, (30, 50)));

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
                        },
                        (source) => new Trg()
                        {
                            C = 10
                        },
                        sourceIgnoredProperties: new IgnoreList<Src>(
                            x => x.Ignore1,
                            x => x.B
                        ),
                        targetIgnoredProperties: new IgnoreList<Trg>(
x => x.B,
(target) => target.Ignore2));
            }
        }
    }" + ClassDefinitions;

            VerifyCSharpFix(test, fixTest);
        }

        [TestMethod]
        public void WhenMembersAreMissingFromIgnoreACodeFixWillBeOfferedToAddThem_AddNewIgnoreListCase()
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
                        (source) => new Trg()
                        {
                            C = 10
                        },
                        sourceIgnoredProperties: new IgnoreList<Src>(
                            x => x.Ignore1
                        )));
            }
        }
    }" + ClassDefinitions;

            VerifyCSharpDiagnostic(test,
                MappingProblem("Target member Ignore2 is not mapped.", 17, 21));

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
                        (source) => new Trg()
                        {
                            C = 10
                        },
                        sourceIgnoredProperties: new IgnoreList<Src>(
                            x => x.Ignore1
                        ),
targetIgnoredProperties: new IgnoreList<Trg>(
(target) => target.Ignore2)));
            }
        }
    }" + ClassDefinitions;

            VerifyCSharpFix(test, fixTest, allowNewCompilerDiagnostics: true);
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

            VerifyCSharpDiagnostic(test,            
                StructuralProblem("Source type could not be resolved.", 19, 21),
                StructuralProblem("Target type could not be resolved.", 19, 21),
                StructuralProblem("\"defaultMappings\" not found.", 19, 21),
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
                            (x) => x.Ignore2
                        )));
            }
        }
    }" + ClassDefinitions;

            VerifyCSharpDiagnostic(test,
                 MappingProblem("Source member A;B are not mapped.", 17, 21, (25, 50)),
                 MappingProblem("Target member A;B are not mapped.", 17, 21, (28, 50)),
                 MappingProblem("Some membmers with identical names are not mapped. Please choose 'Regenerate defaultMappings.'" +
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
                            (x) => x.Ignore2
                        )));
            }
        }
    }" + ClassDefinitions;

            VerifyCSharpFix(test, fixTest, diagnosticsIndex: 2);

        }

        [TestMethod]
        public void WhenMappingWithoudDefaultFoundWillOfferToGenerateAllMappings()
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
                var test = new ExpressionMappingComponents<Src, Trg>();
            }
        }
    }" + ClassDefinitions;

            VerifyCSharpDiagnostic(test,
                StructuralProblem("\"defaultMappings\" not found.", 16, 28));

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
                var test =
new ExpressionMappingComponents<Src, Trg>(
    defaultMappings: (Src source) => new Trg()
    {
        A = source.A,
        B = source.B
    },
    customMappings: (Src source) => new Trg()
    {
    },
    sourceIgnoredProperties: new IgnoreList<Src>(
        (Src source) => source.Ignore1
),
    targetIgnoredProperties: new IgnoreList<Trg>(
        (Trg target) => target.C,
(Trg target) => target.Ignore2
));
            }
        }
    }" + ClassDefinitions;

            VerifyCSharpFix(test, fixTest, allowNewCompilerDiagnostics: true);
        }

        [TestMethod]
        public void GeneratingDefaultMappingsHasNoProblemWithEmptyClass()
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
                var test =
new ExpressionMappingComponents<Src, Trg>();
            }
        }
    }

namespace ConsoleApplication1
    {
        public class Src
        {
        }

        public class Trg
        {
        }
    }";

            VerifyCSharpDiagnostic(test,
                StructuralProblem("\"defaultMappings\" not found.", 17, 1));

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
                var test =

new ExpressionMappingComponents<Src, Trg>(
    defaultMappings: (Src source) => new Trg()
    {
    },
    customMappings: (Src source) => new Trg()
    {
    },
    sourceIgnoredProperties: new IgnoreList<Src>(
        ),
    targetIgnoredProperties: new IgnoreList<Trg>(
        ));
            }
        }
    }

namespace ConsoleApplication1
    {
        public class Src
        {
        }

        public class Trg
        {
        }
    }";

            VerifyCSharpFix(test, fixTest, allowNewCompilerDiagnostics: true);
        }

        [TestMethod]
        public void AnalyzersCorrectlyPicksUpMembersTouchedInNonSimpleWaysInCustomMapings()
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
                            B = source.B,
                        },
                        customMappings: (source) => new Trg()
                        {
                            C = 15,
                            A = (100 * source.A) - 10
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

        DiagnosticResult MappingProblem(
                                string message, 
                                int line, 
                                int column, 
                                (int line, int column)? additionalLocation = null)
        {
            var locations = new List<DiagnosticResultLocation>()
                            {
                                new DiagnosticResultLocation("Test0.cs", line, column)
                            };

            if(additionalLocation != null)
            {
                locations.Add(new DiagnosticResultLocation(
                                        "Test0.cs",
                                        additionalLocation.Value.line,
                                        additionalLocation.Value.column));
            }

            return new DiagnosticResult
            {
                Id = "IKoshelevRoslynMapper",
                Message = String.Format(IKoshelevRoslynMapperAnalyzer.MappingDefinitionMissingMembergRuleMessageFormat, message),
                Severity = DiagnosticSeverity.Error,
                Locations = locations.ToArray()
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