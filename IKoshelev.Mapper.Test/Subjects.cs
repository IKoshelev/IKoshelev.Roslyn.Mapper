using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IKoshelev.Mapper.Test
{
    public class Foo
    {
        public int A { get; set; }
        public int B { get; set; }
        public int C { get; set; }
        public int[] D { get; set; }
        public Bar E { get; set; }
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
    }

    public class Foo1
    {
        public int F { get; set; }
        public Foo Foo { get; set; }
    }

    public class Bar1
    {
        public int F { get; set; }
        public Bar Bar { get; set; }
    }
}
