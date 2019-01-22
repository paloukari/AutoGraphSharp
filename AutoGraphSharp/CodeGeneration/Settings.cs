using System;
using System.Collections.Generic;
using System.Text;

namespace AutoGraphSharp.CodeGeneration
{
    public class Settings
    {
        public string Prefix { get; set; }
        public string AutoPrefix { get; set; }

        public Settings(string prefix, string autoPrefix)
        {
            Prefix = prefix;
            AutoPrefix = autoPrefix;
        }
    }
}
