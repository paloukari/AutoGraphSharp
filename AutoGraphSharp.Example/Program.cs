using System;
using System.Diagnostics;
using TensorFlow;

namespace AutoGraphSharp.Example
{
    partial class Program
    {
        [AutoGraph]
        public static int Function(int a, int b)
        {
            int c = (a + b *3 )/a;

            if (a > b)
                c = 1;
            else
                c = 2;

            return c;
        }

        static void Main(string[] args)
        {
            using (var session = new TFSession())
            {
                var result1 = Function(1, -1);
                var result2 = Function(1, -1, session); 
                Debug.Assert(result1 == result2);
            }
        }
    }
}
