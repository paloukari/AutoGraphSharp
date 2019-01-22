using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoGraphSharp.CodeGeneration.Processor
{
    public class IfStatementsProcessor : IProcessor
    {
        public SyntaxList<StatementSyntax> Refactor(SyntaxList<StatementSyntax> statements)
        {
            return RefactorIfStatements(statements, 0);
        }
        private SyntaxList<StatementSyntax> RefactorIfStatements(SyntaxList<StatementSyntax> statements, int ifCounter)
        {
            var newStatements = new SyntaxList<StatementSyntax>();

            int position = 0;
            while (position < statements.Count)
            {
                var statement = statements[position];
                if (statement.IsKind(SyntaxKind.IfStatement))
                {
                    newStatements = newStatements.AddRange(ConvertIfStatement(statement as IfStatementSyntax, new SyntaxList<StatementSyntax>(statements.Skip(position + 1)), new SyntaxList<StatementSyntax>(statements.Take(position)), ++ifCounter));
                    break;
                }
                else
                {
                    newStatements = newStatements.Add(statement);
                    position++;
                }
            }
            return newStatements;
        }

        private SyntaxList<StatementSyntax> ConvertIfStatement(IfStatementSyntax ifStatement,
             SyntaxList<StatementSyntax> nextStatements,
             SyntaxList<StatementSyntax> previousStatements,
             int ifCounter)
        {
            var newStatements = new SyntaxList<StatementSyntax>();
            var parentScopeVariables = GetDeclaredVariables(previousStatements);

            var predicateStatement = SyntaxFactory.ParseStatement($"var predicate{ifCounter} = {ifStatement.Condition.ToString()};");
            newStatements = newStatements.Add(predicateStatement);

            //create the true Func
            SyntaxList<StatementSyntax> ifTrueBlockStatements = ExpandBlock(ifStatement.Statement, ifCounter);
            var ifTrueAssignedVariables = GetAssignedVariables(ifTrueBlockStatements);

            //create the false Func
            SyntaxList<StatementSyntax> ifFalseBlockStatements = ExpandBlock(ifStatement.Else.Statement, ifCounter);
            var ifFalseAssignedVariables = GetAssignedVariables(ifFalseBlockStatements);

            var mappedVariables = parentScopeVariables.Union(ifTrueAssignedVariables).Concat(parentScopeVariables.Union(ifTrueAssignedVariables)).Distinct().ToList();

            var ifTrueStatements = CreateConditionFunc(ifTrueBlockStatements, true, ifCounter, mappedVariables);
            var ifFalseStatements = CreateConditionFunc(ifFalseBlockStatements, false, ifCounter, mappedVariables);

            newStatements = newStatements.AddRange(ifTrueStatements);
            newStatements = newStatements.AddRange(ifFalseStatements);

            var return_Variables = GetReturnVariablesString(mappedVariables);
            var refactoredIf1 = SyntaxFactory.ParseStatement(
            $"var res = new AutoCond<Tuple<{string.Join(",", Enumerable.Repeat("AutoTFOutput", Math.Max(1, mappedVariables.Count)).ToArray())}>>(predicate{ifCounter}, ifTrue{ifCounter}, ifFalse{ifCounter}, session);");
            var refactoredIf2 = SyntaxFactory.ParseStatement(
            $"res.Deconstruct({string.Join(", ", mappedVariables.Select(e => $"out {e}"))});");

            newStatements = newStatements.Add(refactoredIf1);
            newStatements = newStatements.Add(refactoredIf2);

            newStatements = newStatements.AddRange(RefactorIfStatements(new SyntaxList<StatementSyntax>(nextStatements), ifCounter));
            return newStatements;
        }

        private List<string> GetDeclaredVariables(SyntaxList<StatementSyntax> statements)
        {
            var variables = new List<string>();
            foreach (var statement in statements)
            {
                var variableDecls = statement.DescendantNodes().Where(e => e.IsKind(SyntaxKind.VariableDeclaration));
                foreach (var variableDecl in variableDecls)

                    if (variableDecl.IsKind(SyntaxKind.VariableDeclaration))
                        if (((VariableDeclarationSyntax)variableDecl).Type.IsVar)
                            variables.AddRange(((VariableDeclarationSyntax)variableDecl).Variables.Select(e => e.Identifier.ToString()));
            }
            return variables;
        }

        private SyntaxList<StatementSyntax> CreateConditionFunc(SyntaxList<StatementSyntax> statements, bool type, int ifCounter, List<string> mappedVariables)
        {
            SyntaxList<StatementSyntax> newStatements = new SyntaxList<StatementSyntax>();
            StatementSyntax ifReturnStatement = null;
            SyntaxList<StatementSyntax> variableMapping = new SyntaxList<StatementSyntax>(mappedVariables.Select(e => SyntaxFactory.ParseStatement($"var _{e} = {e};")));

            var templateSignature = $"Func<Tuple<{string.Join(", ", Enumerable.Repeat("AutoTFOutput", Math.Max(mappedVariables.Count, 1)))}>>";

            var ifTrueStatement = SyntaxFactory.ParseStatement($"{templateSignature} if{type}{ifCounter} = () => {{");

            if (!statements.Any(e => { return e.IsKind(SyntaxKind.ReturnStatement); }))
            {
                if (mappedVariables.Count > 0)
                    ifReturnStatement = SyntaxFactory.ParseStatement($"return Tuple.Create({GetReturnVariablesString(mappedVariables, "_")});");
                else
                    ifReturnStatement = SyntaxFactory.ParseStatement($"return new AutoTFOutput(session.Graph.Const(1), session);");
            }

            newStatements = newStatements.Add(ifTrueStatement);
            newStatements = newStatements.AddRange(variableMapping);
            newStatements = newStatements.AddRange(ReplaceVariables(statements, mappedVariables));
            newStatements = newStatements.Add(ifReturnStatement);
            newStatements = newStatements.Add(SyntaxFactory.ParseStatement("};"));

            return newStatements;
        }

        private string GetReturnVariablesString(List<string> mappedVariables, string prefix = "")
        {
            if (mappedVariables == null || mappedVariables.Count == 0)
                return "";
            return $"{string.Join(", ", mappedVariables.Select(e => $"{prefix}{e}"))}";
        }

        private SyntaxList<StatementSyntax> ReplaceVariables(SyntaxList<StatementSyntax> statements, List<string> mappedVariables)
        {
            var replacedStatements = new SyntaxList<StatementSyntax>();
            foreach (var statement in statements)
            {
                var identifiers = statement.DescendantNodes().Where(e => e.IsKind(SyntaxKind.IdentifierName));
                var newStatement = statement;
                foreach (var identifier in identifiers)
                {
                    if (mappedVariables.Contains(((IdentifierNameSyntax)identifier).Identifier.ToString()))
                        newStatement = statement.ReplaceNode(identifier, SyntaxFactory.IdentifierName($"_{((IdentifierNameSyntax)identifier).Identifier.ToString()}"));
                }
                replacedStatements = replacedStatements.Add(newStatement);
            }
            return replacedStatements;
        }

        private SyntaxList<StatementSyntax> ExpandBlock(StatementSyntax statement, int ifCounter)
        {
            SyntaxList<StatementSyntax> ifBlockStatements;
            if (statement != null)
                if (statement is BlockSyntax)
                    ifBlockStatements = RefactorIfStatements(((BlockSyntax)statement).Statements, ifCounter);
                else
                    ifBlockStatements = RefactorIfStatements(new SyntaxList<StatementSyntax>(statement), ifCounter);
            return ifBlockStatements;
        }

        private List<string> GetAssignedVariables(SyntaxList<StatementSyntax> statements)
        {
            var variables = new List<string>();
            foreach (var statement in statements)
            {
                var identifiers = statement.DescendantNodes().Where(e => e.IsKind(SyntaxKind.IdentifierName));
                foreach (var identifier in identifiers)
                {
                    var kind = identifier.Parent.Kind();
                    if (kind >= SyntaxKind.SimpleAssignmentExpression && kind <= SyntaxKind.RightShiftAssignmentExpression)
                        variables.Add(((IdentifierNameSyntax)identifier).Identifier.ToString());
                }
            }
            return variables.Distinct().ToList();
        }


    }

}
