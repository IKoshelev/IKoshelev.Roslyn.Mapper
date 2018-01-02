using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace IKoshelev.Mapper.Test
{
    [TestFixture]
    public class MapperAcceptanceTest
    {
        [Test]
        public void DelegateMapper_Works()
        {
            var foo = new Foo()
            {
                A = 5,
                B = 10
            };

            var mapper = new DelegateMapper<Foo, Bar>((source, destination) =>
            {
                destination.A = source.A;
                destination.B = source.B;
                destination.C = 15;
            });

            var @new = mapper.Map(foo);
            Assert.AreEqual(@new.A, 5);
            Assert.AreEqual(@new.B, 10);
            Assert.AreEqual(@new.C, 15);
            Assert.AreEqual(@new.E, null);

            var existing = new Bar();

            mapper.Map(foo, existing);
            Assert.AreEqual(existing.A, 5);
            Assert.AreEqual(existing.B, 10);
            Assert.AreEqual(existing.C, 15);
            Assert.AreEqual(existing.E, null);
        }

        [Test]
        public void ExpressionMapper_Works()
        {
            var foo = new Foo()
            {
                A = 5,
                B = 10
            };

            var mapper = new ExpressionMapper<Foo, Bar>(
                new ExpressionMappingComponents<Foo, Bar>(
                    defaultMappings: (Foo source) => new Bar()
                    {
                        A = source.A,
                        B = source.B,
                    },
                    customMappings: (source) => new Bar()
                    {
                        C = 15
                    },
                    sourceIgnoredProperties: new IgnoreList<Foo>(
                        x => x.A
                    )));

            var @new = mapper.Map(foo);
            Assert.AreEqual(@new.A, 5);
            Assert.AreEqual(@new.B, 10);
            Assert.AreEqual(@new.C, 15);
            Assert.AreEqual(@new.E, null);

            var existing = new Bar();

            mapper.Map(foo, existing);
            Assert.AreEqual(existing.A, 5);
            Assert.AreEqual(existing.B, 10);
            Assert.AreEqual(existing.C, 15);
            Assert.AreEqual(existing.E, null);
        }
    }
}
