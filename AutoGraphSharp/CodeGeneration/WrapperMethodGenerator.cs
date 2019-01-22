using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;

namespace AutoGraphSharp.CodeGeneration
{
    internal class WrapperMethodGenerator : IGenerator
    {
        public Settings Settings { get; set; }
        public WrapperMethodGenerator(Settings settings)
        {
            Settings = settings;
        }
        public MethodDeclarationSyntax Generate(MethodDeclarationSyntax originalMethod)
        {
            var method = GenerateSignature(originalMethod);
            method = GenerateBody(method);
            return method;
        }


        private MethodDeclarationSyntax GenerateSignature(MethodDeclarationSyntax originalMethod)
        {
            // Apply a suffix to the name of a copy of the class.
            var newMethod = originalMethod.WithIdentifier(SyntaxFactory.Identifier($"{Settings.Prefix}{originalMethod.Identifier.ValueText}"));

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
        private MethodDeclarationSyntax GenerateBody(MethodDeclarationSyntax method)
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
                    var newName = $"{Settings.AutoPrefix}{name}";
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

            var autoCall = $"return ({method.ReturnType.ToString()})runner.Run({Settings.AutoPrefix}{method.Identifier.ValueText}({string.Join(",", autoCallParams.ToArray())})).GetValue();";
            newStatements = newStatements.Add(SyntaxFactory.ParseStatement(autoCall));

            var newBody = method.Body.WithStatements(newStatements);
            method = method.WithBody(newBody);
            return method;
        }

    }
}
