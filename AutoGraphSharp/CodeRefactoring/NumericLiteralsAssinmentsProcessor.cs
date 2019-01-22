using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AutoGraphSharp.CodeRefactoring
{
    public class NumericLiteralsAssinmentsProcessor : IProcessor
    {
        public SyntaxList<StatementSyntax> Refactor(SyntaxList<StatementSyntax> statements)
        {
            var newStatements = new SyntaxList<StatementSyntax>();

            int position = 0;
            while (position < statements.Count)
            {
                var statement = statements[position];
                var numericLiteralTokens = statement.DescendantNodes().Where(e => e.IsKind(SyntaxKind.NumericLiteralExpression));

                foreach (var literal in numericLiteralTokens)
                {
                    var kind = literal.Parent.Kind();
                    if (kind >= SyntaxKind.SimpleAssignmentExpression && kind <= SyntaxKind.RightShiftAssignmentExpression)
                        statement = statement.ReplaceNode(literal, SyntaxFactory.ParseExpression($"new AutoTFOutput(session.Graph.Const({((LiteralExpressionSyntax)literal).Token.Text}), session)"));

                    if (kind == SyntaxKind.EqualsValueClause)
                    {
                        var variableDecl = literal.Parent.Parent.Parent;
                        if (variableDecl.IsKind(SyntaxKind.VariableDeclaration))
                        {
                            if (((VariableDeclarationSyntax)variableDecl).Type.IsVar)
                            {
                                statement = statement.ReplaceNode(literal, SyntaxFactory.ParseExpression($"new AutoTFOutput(session.Graph.Const({((LiteralExpressionSyntax)literal).Token.Text}), session)"));
                            }
                        }
                    }
                }


                newStatements = newStatements.Add(statement);
                position++;
            }
            return newStatements;

        }
    }
}
