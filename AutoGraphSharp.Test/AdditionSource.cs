namespace AutoGraphSharp.Test
{
    public class AdditionSource
    {
        [AutoGraph(Prefix = "_")]
        public int Addition(int a, int b)
        {
            int c = a + b;

            if (c != 0)
            {
                c = 1;
            }
            else
            {
                c = 2;
            }

            return c;
        }
    }
}
