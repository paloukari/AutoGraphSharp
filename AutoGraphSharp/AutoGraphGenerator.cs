using AutoGraphSharp.CodeGeneration;
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
        private readonly Settings Settings;
        
        public AutoGraphGenerator(AttributeData attributeData)
        {
            Requires.NotNull(attributeData, nameof(attributeData));

            var prefix = "";
            var autoPrefix = "_";

            if (attributeData.NamedArguments != null && attributeData.NamedArguments.Length > 0)
            {
                var prefixData = attributeData.NamedArguments.Where(e => e.Key == "Prefix").SingleOrDefault();
                var autoPrefixData = attributeData.NamedArguments.Where(e => e.Key == "AutoPrefix").SingleOrDefault();
                if (prefixData.Key != null)
                    prefix = prefixData.Value.Value.ToString();

                if (autoPrefixData.Key != null)
                    autoPrefix = autoPrefixData.Value.Value.ToString();
            }

            Settings = new Settings(prefix, autoPrefix);
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

            var autoMethod = new AutoMethodGenerator(Settings).Generate(method);
            var wrapperMethod = new WrapperMethodGenerator(Settings).Generate(method);

            results = results.Add(wrapperMethod);
            results = results.Add(autoMethod);

            return Task.FromResult(results);
        }
    }
}