using AutoGraphSharp.CodeRefactoring;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;


namespace AutoGraphSharp.CodeGeneration
{
    internal class AutoMethodGenerator : IGenerator
    {
        public Settings Settings { get; set; }
        public AutoMethodGenerator(Settings settings)
        {
            Settings = settings;
        }
        public MethodDeclarationSyntax Generate(MethodDeclarationSyntax originalMethod)
        {
            var method = GenerateSignature(originalMethod);
            method = GenerateBody(method);
            return method;
        }


        private MethodDeclarationSyntax GenerateSignature(MethodDeclarationSyntax method)
        {
            var newMethod = method.WithIdentifier(
                SyntaxFactory.Identifier($"{Settings.AutoPrefix}{method.Identifier.ValueText}"));

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
        private MethodDeclarationSyntax GenerateBody(MethodDeclarationSyntax method)
        {
            var mappedParams = new Dictionary<string, string>();

            //replace all if statements
            var statements = new SyntaxList<StatementSyntax>();

            //statements = statements.Add(SyntaxFactory.ParseStatement("Func<AutoTFOutput> body = () => {"));

            var bodyStatements = new IfStatementsProcessor().Refactor(method.Body.Statements);
            statements = statements.AddRange(new NumericLiteralsAssinmentsProcessor().Refactor(bodyStatements));

            //statements = statements.Add(SyntaxFactory.ParseStatement("};"));

            //statements = statements.Add(SyntaxFactory.ParseStatement("var result = body();"));
            //statements = statements.Add(SyntaxFactory.ParseStatement("return result;"));

            var newBody = method.Body.WithStatements(statements);
            method = method.WithBody(newBody);
            return method;
        }


    }
}
