using System;
using System.Diagnostics;
using TensorFlow;

namespace AutoGraphSharp.Example
{
    partial class Program
    {
        [AutoGraph]
        public static int Addition(int a, int b)
        {
            var c = (((a + b) * a + b * a) / b - a - 2) / 5;

            if (c == 0)
            {
                c = a + 1;
                if (a > 0)
                    c++;
            }

            return c;
        }

       
        static void Main(string[] args)
        {
            using (var session = new TFSession())
            {
                var add1 = Addition(2, 3, session);
                var add2 = Addition(2, 3);
                Debug.Assert(add1 == add2);
            }
        }
    }
}
