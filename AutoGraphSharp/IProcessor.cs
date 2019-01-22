using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoGraphSharp
{
    public interface IProcessor
    {
        SyntaxList<StatementSyntax> Refactor(SyntaxList<StatementSyntax> statements);
    }
}