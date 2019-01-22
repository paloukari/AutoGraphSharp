using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoGraphSharp.CodeGeneration
{
    internal interface IGenerator
    {
        Settings Settings { get; set; }

        MethodDeclarationSyntax Generate(MethodDeclarationSyntax originalMethod);
    }
}