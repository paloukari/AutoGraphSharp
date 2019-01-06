using CodeGeneration.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Validation;

namespace AutoGraphSharp
{
    class AutoGraphGenerator : ICodeGenerator
    {
        private readonly string Prefix;
        private readonly string AutoPrefix = "_";
        public AutoGraphGenerator(AttributeData attributeData)
        {
            Requires.NotNull(attributeData, nameof(attributeData));

            if (attributeData.NamedArguments != null && attributeData.NamedArguments.Length > 0)
            {
                var prefix = attributeData.NamedArguments.Where(e => e.Key == "Prefix").SingleOrDefault();
                if (prefix.Key != null)
                    Prefix = prefix.Value.Value.ToString();
            }
            else
                Prefix = "";
        }

        public Task<SyntaxList<MemberDeclarationSyntax>> GenerateAsync(
            TransformationContext context,
            IProgress<Diagnostic> progress,
            CancellationToken cancellationToken)
        {
            var results = SyntaxFactory.List<MemberDeclarationSyntax>();

            //get the current method
            var method = (MethodDeclarationSyntax)context.ProcessingNode;
            //get the containing class
            var parentClass = (ClassDeclarationSyntax)context.ProcessingNode.Parent;

            var autoMethod = GenerateAutoMethod(method);
            autoMethod = GenerateAutoMethodBody(autoMethod).NormalizeWhitespace();

            var wrapperMethod = GenerateWrapperMethod(method);
            wrapperMethod = GenerateWrapperMethodBody(wrapperMethod).NormalizeWhitespace();

            results = results.Add(wrapperMethod);
            results = results.Add(autoMethod);

            return Task.FromResult(results);
        }


        private MethodDeclarationSyntax GenerateWrapperMethod(MethodDeclarationSyntax originalMethod)
        {
            // Apply a suffix to the name of a copy of the class.
            var newMethod = originalMethod.WithIdentifier(SyntaxFactory.Identifier($"{Prefix}{originalMethod.Identifier.ValueText}"));

            var sessionParameter = SyntaxFactory.Parameter(new SyntaxList<AttributeListSyntax>(),
                new SyntaxTokenList(),
                SyntaxFactory.IdentifierName("TensorFlow.TFSession"),
                SyntaxFactory.Identifier("session"), null);

            //session = session.AddModifiers(SyntaxFactory.Token(SyntaxKind.ThisKeyword));

            var parameters = new SeparatedSyntaxList<ParameterSyntax>();

            parameters = parameters.AddRange(originalMethod.ParameterList.Parameters);
            parameters = parameters.Add(sessionParameter);

            var methodParameters = SyntaxFactory.ParameterList(parameters);
            newMethod = newMethod.WithParameterList(methodParameters);

            return newMethod;
        }
        private MethodDeclarationSyntax GenerateWrapperMethodBody(MethodDeclarationSyntax method)
        {
            var newStatements = new SyntaxList<StatementSyntax>();
            var mappedParams = new Dictionary<string, string>();
            var autoCallParams = new List<string>();

            newStatements = newStatements.Add(
                        SyntaxFactory.ParseStatement($"var runner = session.GetRunner();"));

            //add the Const variable for every predefined type param
            foreach (var param in method.ParameterList.Parameters)
                if (param.Type.IsKind(SyntaxKind.PredefinedType))
                {
                    var name = param.Identifier.ToString();
                    var newName = $"{AutoPrefix}{name}";
                    mappedParams[name] = newName;

                    newStatements = newStatements.Add(
                        SyntaxFactory.ParseStatement($"var {newName} = new AutoTFOutput(session.Graph.Placeholder(TFTensor.TensorTypeFromType({name}.GetType())), session);"));
                    newStatements = newStatements.Add(
                        SyntaxFactory.ParseStatement($"runner.AddInput({newName}, new TFTensor({name}));").WithLeadingTrivia(SyntaxFactory.Whitespace("\n")));

                    autoCallParams.Add($"{newName}");
                }
                else
                {
                    var name = param.Identifier.ToString();
                    autoCallParams.Add($"{name}");
                }

            var autoCall = $"return ({method.ReturnType.ToString()})runner.Run({AutoPrefix}{method.Identifier.ValueText}({string.Join(",", autoCallParams.ToArray())})).GetValue();";
            newStatements = newStatements.Add(SyntaxFactory.ParseStatement(autoCall));

            var newBody = method.Body.WithStatements(newStatements);
            method = method.WithBody(newBody);
            return method;
        }

        private MethodDeclarationSyntax GenerateAutoMethod(MethodDeclarationSyntax method)
        {
            var newMethod = method.WithIdentifier(
                SyntaxFactory.Identifier($"{AutoPrefix}{method.Identifier.ValueText}"));

            var parameters = new SeparatedSyntaxList<ParameterSyntax>();
            foreach (var parameter in method.ParameterList.Parameters)
            {
                if (!parameter.Type.IsKind(SyntaxKind.PredefinedType))
                    parameters = parameters.Add(parameter);
                else
                {
                    var autoParam = parameter.WithType(SyntaxFactory.IdentifierName("AutoTFOutput"));
                    parameters = parameters.Add(autoParam);
                }
            }

            var sessionParameter = SyntaxFactory.Parameter(new SyntaxList<AttributeListSyntax>(),
                new SyntaxTokenList(),
                SyntaxFactory.IdentifierName("TensorFlow.TFSession"),
                SyntaxFactory.Identifier("session"), null);

            parameters = parameters.Add(sessionParameter);
            newMethod = newMethod.WithParameterList(SyntaxFactory.ParameterList(parameters));
            newMethod = newMethod.WithReturnType(SyntaxFactory.IdentifierName("AutoTFOutput"));

            return newMethod;
        }
        private MethodDeclarationSyntax GenerateAutoMethodBody(MethodDeclarationSyntax method)
        {
            var mappedParams = new Dictionary<string, string>();

            //replace all if statements
            var statements = new SyntaxList<StatementSyntax>();

            //statements = statements.Add(SyntaxFactory.ParseStatement("Func<AutoTFOutput> body = () => {"));

            var bodyStatements = RefactorIfStatements(method.Body.Statements, 0);

            statements = statements.AddRange(RefactorNumericLiteralsAssinmentExpressions(bodyStatements));

            //statements = statements.Add(SyntaxFactory.ParseStatement("};"));

            //statements = statements.Add(SyntaxFactory.ParseStatement("var result = body();"));
            //statements = statements.Add(SyntaxFactory.ParseStatement("return result;"));

            var newBody = method.Body.WithStatements(statements);
            method = method.WithBody(newBody);
            return method;
        }

        private SyntaxList<StatementSyntax> RefactorNumericLiteralsAssinmentExpressions(SyntaxList<StatementSyntax> statements)
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

            //add the continuation code
            //newStatements = newStatements.Add(SyntaxFactory.ParseStatement($"Func <AutoTFOutput> res{ifCounter} = () => {{"));
            newStatements = newStatements.AddRange(RefactorIfStatements(new SyntaxList<StatementSyntax>(nextStatements), ifCounter));
            //if (ifCounter > 1)
            //    newStatements = newStatements.Add(SyntaxFactory.ParseStatement($"return res{ifCounter - 1}();"));
            //newStatements = newStatements.Add(SyntaxFactory.ParseStatement("};"));
            return newStatements;
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

        void GenerateStaticClass()
        {
            //var staticExtensionsClass = SyntaxFactory.ClassDeclaration($"AutoGraphExtensions_{parentClass.Identifier.ToString()}");
            //staticExtensionsClass = staticExtensionsClass.AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword),
            //    SyntaxFactory.Token(SyntaxKind.StaticKeyword),
            //    SyntaxFactory.Token(SyntaxKind.PartialKeyword));
        }


    }
}