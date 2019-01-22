using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AutoGraphSharp.CodeGeneration.Processor
{
    public class VariableDevlarationStatementsProcessor : IProcessor
    {
        public SyntaxList<StatementSyntax> Refactor(SyntaxList<StatementSyntax> statements)
        {
            var newStatements = new SyntaxList<StatementSyntax>();

            int position = 0;
            while (position < statements.Count)
            {
                var statement = statements[position];
                var variableDeclarationStatements = statement.DescendantNodes().Where(e => e.IsKind(SyntaxKind.VariableDeclaration));

                foreach (var literal in variableDeclarationStatements)
                {
                    var declaration = (VariableDeclarationSyntax)literal;

                    if (!declaration.Type.IsVar)
                        statement = statement.ReplaceNode(literal, ((VariableDeclarationSyntax)literal).WithType(SyntaxFactory.IdentifierName("var")));
                }


                newStatements = newStatements.Add(statement);
                position++;
            }
            return newStatements;

        }
    }
}
