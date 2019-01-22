using CodeGeneration.Roslyn;
using System;
using System.Diagnostics;
using Validation;

namespace AutoGraphSharp
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    [CodeGenerationAttribute(typeof(CodeGeneration.AutoGraphGenerator))]
    [Conditional("CodeGeneration")]
    public class AutoGraphAttribute : Attribute
    {
        public AutoGraphAttribute()
        {

        }
        public string Prefix { get; set; }
    }
}
