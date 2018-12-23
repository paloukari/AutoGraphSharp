
namespace AutoGraphSharp.Test
{
    public class AdditionSource
    {
        [AutoGraph(Prefix = "_")]
        public int Addition(int a, int b)
        {
            var c = a + b;
            if (c > 0)
            {
                c = a + 1;
                if (a > 0)
                    c++;
            }
            else
                c++;

            return c;
        }
    }
}
