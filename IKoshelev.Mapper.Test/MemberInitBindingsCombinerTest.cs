using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using IKoshelev.Mapper.MemberInitBindingsCombiner;

namespace IKoshelev.Mapper.Test
{
    [TestFixture]
    public class MemberInitBindingsCombinerTest
    {
        [Test]
        public void DoesNotThrowOnEmptyInitializers()
        {
            var combiner = new MemberInitBindingsCombiner<Foo, Bar>();

            Expression<Func<Foo, Bar>> source = (x) => new Bar { };
            Expression<Func<Foo, Bar>> target = (x) => new Bar { };

            var combined = combiner.CombineIntoMapperWithConstructor(source, target);
            Assert.AreEqual(combined.ToString(), "x => new Bar() {}");
        }

        [Test]
        public void CanMergeTwoBasciMemberInitLambdas()
        {
            var combiner = new MemberInitBindingsCombiner<Foo, Bar>();

            Expression<Func<Foo, Bar>> source = (x) => new Bar { A = x.A };
            Expression<Func<Foo, Bar>> target = (x) => new Bar { B = x.B };

            var combined = combiner.CombineIntoMapperWithConstructor(source, target);
            Assert.AreEqual(combined.ToString(), "x => new Bar() {A = x.A, B = x.B}");

            Expression<Func<Foo, Bar>> source2 = (x) => new Bar() { A = x.A };
            Expression<Func<Foo, Bar>> target2 = (x) => new Bar() { B = x.B };

            var combined2 = combiner.CombineIntoMapperWithConstructor(source2, target2);
            Assert.AreEqual(combined.ToString(), "x => new Bar() {A = x.A, B = x.B}");
        }

        [Test]
        public void CanMergeTwoAdvancedMemberInitLambdas()
        {
            var combiner = new MemberInitBindingsCombiner<Foo, Bar>();

            Expression<Func<Foo, Bar>> source = (x) => new Bar { E = new Bar { A = 1 } };            
            Expression<Func<Foo, Bar>> target = (x) => new Bar { A = x.D.FirstOrDefault() };

            var combined = combiner.CombineIntoMapperWithConstructor(source, target);
            Assert.AreEqual(combined.ToString(), "x => new Bar() {E = new Bar() {A = 1}, A = x.D.FirstOrDefault()}");
        }

        [Test]
        public void WillThrowOnIncompatibleExpressions()
        {
            var combiner = new MemberInitBindingsCombiner<Foo, Bar>();

            Expression<Func<Foo, Bar>> source = (x) => new Bar { A = x.A };
            Expression<Func<Foo, Bar>> target = (x) => null;

            Assert.Throws<ArgumentException>(() =>
            {
                combiner.CombineIntoMapperWithConstructor(source, target);
            });

            var existing = new Bar();
            Expression<Func<Foo, Bar>> source2 = (x) => existing;
            Expression<Func<Foo, Bar>> target2 = (x) => new Bar { B = x.B };

            Assert.Throws<ArgumentException>(() =>
            {
                combiner.CombineIntoMapperWithConstructor(source, target);
            });
        }

        [Test]
        public void WillThrowOnConstructorsWithArguments()
        {
            var combiner = new MemberInitBindingsCombiner<Foo, Bar>();

            Expression<Func<Foo, Bar>> source = (x) => new Bar(1) { B = x.B };
            Expression<Func<Foo, Bar>> target = (x) => new Bar() { C = x.C };

            Assert.Throws<ArgumentException>(() =>
            {
                combiner.CombineIntoMapperWithConstructor(source, target);
            });
        }

        [Test]
        public void CanProduceMapperExpressionForExistingClasses()
        {
            var combiner = new MemberInitBindingsCombiner<Foo, Bar>();

            Expression<Func<Foo, Bar>> a = (x) => new Bar
            {
                A = x.A,
                E = new Bar { A = 1 }
            };
            Expression<Func<Foo, Bar>> b = (x) => new Bar { B = x.B };

            var combined = combiner.CombineIntoMapperForExisting(a, b);

            var foo = new Foo()
            {
                A = 5,
                B = 10
            };
            var bar = new Bar();

            combined.Compile().Invoke(foo, bar);

            Assert.AreEqual(bar.A, 5);
            Assert.AreEqual(bar.B, 10);
            Assert.NotNull(bar.E);
            Assert.AreEqual(bar.E.A, 1);
        }
    }
}
