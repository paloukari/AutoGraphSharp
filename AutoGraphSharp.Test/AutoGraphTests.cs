using System.IO;
using Xunit;

namespace AutoGraphSharp.Test
{
    public class AutoGraphTests : CompilationTestsBase
    {
        [Fact]
        public void EmptyFile_NoGenerators()
        {
            AssertGeneratedAsExpected("", "");
        }

        [Fact]
        public void Addition()
        {
            var source = File.ReadAllText("../../../AdditionSource.cs");
            var expected = "";

            AssertGeneratedAsExpected(source, expected);
        }
    }
}
