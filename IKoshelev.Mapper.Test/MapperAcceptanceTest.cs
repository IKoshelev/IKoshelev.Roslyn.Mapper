﻿using LinqKit;
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
                    targetIgnoredProperties: new IgnoreList<Bar>(
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

        [Test]
        public void ExpressionMapper_MappingsCanBeNested()
        {
            var foo1 = new Foo1()
            {
                F = 20,
                Foo = new Foo()
                {
                    A = 5,
                    B = 10
                }
            };

            var mappingsNested = new ExpressionMappingComponents<Foo, Bar>(
                    defaultMappings: (Foo source) => new Bar()
                    {
                        A = source.A,
                        B = source.B,
                    },
                    customMappings: (source) => new Bar()
                    {
                        C = 15
                    },
                    targetIgnoredProperties: new IgnoreList<Bar>(
                        x => x.A
                    ));

            var FooToBarExpression = mappingsNested.CombinedMappingsWithConstructor;

            var mapper = new ExpressionMapper<Foo1, Bar1>(
                            new ExpressionMappingComponents<Foo1, Bar1>(
                                defaultMappings: (Foo1 source) => new Bar1()
                                {
                                    F = source.F,
                                    Bar = FooToBarExpression.Invoke(source.Foo),
                                }));

            var @new = mapper.Map(foo1);
            Assert.AreEqual(@new.F, 20);
            Assert.AreEqual(@new.Bar.A, 5);
            Assert.AreEqual(@new.Bar.B, 10);
            Assert.AreEqual(@new.Bar.C, 15);
            Assert.AreEqual(@new.Bar.E, null);

            var existing = new Bar1();

            mapper.Map(foo1, existing);
            Assert.AreEqual(existing.F, 20);
            Assert.AreEqual(existing.Bar.A, 5);
            Assert.AreEqual(existing.Bar.B, 10);
            Assert.AreEqual(existing.Bar.C, 15);
            Assert.AreEqual(existing.Bar.E, null);
        }

        [Test]
        public void ExpressionMapper_WorksWithJustDefaultMappings()
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
                    }));

            var @new = mapper.Map(foo);
            Assert.AreEqual(@new.A, 5);
            Assert.AreEqual(@new.B, 10);
            Assert.AreEqual(@new.C, 0);
            Assert.AreEqual(@new.E, null);

            var existing = new Bar();

            mapper.Map(foo, existing);
            Assert.AreEqual(existing.A, 5);
            Assert.AreEqual(existing.B, 10);
            Assert.AreEqual(existing.C, 0);
            Assert.AreEqual(existing.E, null);
        }
    }
}
