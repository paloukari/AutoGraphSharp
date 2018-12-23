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
            autoMethod = GenerateAutoMethodBody(autoMethod);

            var wrapperMethod = GenerateWrapperMethod(method);
            wrapperMethod = GenerateWrapperMethodBody(wrapperMethod);

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
                if (param.Type.Kind() == SyntaxKind.PredefinedType)
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
                if (parameter.Type.Kind() != SyntaxKind.PredefinedType)
                {
                    parameters = parameters.Add(parameter);
                }
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
            var statements = RefactorIfStatements(method.Body.Statements, 0);
            var newBody = method.Body.WithStatements(statements);
            method = method.WithBody(newBody);
            return method;
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
                    newStatements = newStatements.AddRange(ConvertIfStatement(statement as IfStatementSyntax, new SyntaxList<StatementSyntax>(statements.Skip(position + 1)), ++ifCounter));
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

        private SyntaxList<StatementSyntax> ConvertIfStatement(IfStatementSyntax ifStatement, SyntaxList<StatementSyntax> statements, int ifCounter)
        {
            var newStatements = new SyntaxList<StatementSyntax>();

            newStatements = newStatements.Add(SyntaxFactory.ParseStatement($"var predicate{ifCounter} = {ifStatement.Condition.ToString()};"));

            //create the continuation Func
            newStatements = newStatements.Add(SyntaxFactory.ParseStatement($"Func <AutoTFOutput> res{ifCounter} = () => {{"));
            newStatements = newStatements.AddRange(RefactorIfStatements(new SyntaxList<StatementSyntax>(statements), ifCounter));
            if (ifCounter > 1)
                newStatements = newStatements.Add(SyntaxFactory.ParseStatement($"return res{ifCounter - 1}();"));
            newStatements = newStatements.Add(SyntaxFactory.ParseStatement("};"));

            //create the true Func
            newStatements = newStatements.Add(SyntaxFactory.ParseStatement($"Func <AutoTFOutput> if_true{ifCounter} = () => {{"));
            if (ifStatement.Statement is BlockSyntax)
                newStatements = newStatements.AddRange(RefactorIfStatements(((BlockSyntax)ifStatement.Statement).Statements, ifCounter));
            else
                newStatements = newStatements.AddRange(RefactorIfStatements(new SyntaxList<StatementSyntax>(ifStatement.Statement), ifCounter));

            newStatements = newStatements.Add(SyntaxFactory.ParseStatement($"return res{ifCounter}();"));
            newStatements = newStatements.Add(SyntaxFactory.ParseStatement("};"));

            //create the false Func
            newStatements = newStatements.Add(SyntaxFactory.ParseStatement($"Func <AutoTFOutput> if_false{ifCounter} = () => {{"));
            if (ifStatement.Else != null)
            {
                if (ifStatement.Else.Statement is BlockSyntax)
                {
                    newStatements = newStatements.AddRange(RefactorIfStatements(new SyntaxList<StatementSyntax>(((BlockSyntax)ifStatement.Else.Statement).Statements), ifCounter));
                }
                else
                {
                    newStatements = newStatements.AddRange(RefactorIfStatements(new SyntaxList<StatementSyntax>(new SyntaxList<StatementSyntax>(ifStatement.Else.Statement)), ifCounter));
                }
            }
            newStatements = newStatements.Add(SyntaxFactory.ParseStatement($"return res{ifCounter}();"));
            newStatements = newStatements.Add(SyntaxFactory.ParseStatement("};"));

            //var condition = $"({ifStatement.Condition.ToString()})";
            //var thenStatement = $"() => {{ return {ifStatement.Statement} }}";
            //var elseStatement = $"() => {{ return {ifStatement.Else.Statement} }}";

            var refactoredIf = SyntaxFactory.ParseStatement(
                $"return new AutoCond(predicate{ifCounter}, if_true{ifCounter}, if_false{ifCounter}, session);");

            newStatements = newStatements.Add(refactoredIf);
            return newStatements;
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