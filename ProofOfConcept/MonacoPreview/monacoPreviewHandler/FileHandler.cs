using System;
using System.Collections.Generic;
using System.Text;

namespace monacoPreview
{
    class FileHandler
    {
        /// <summary>
        /// Converts a file extension to a language monaco id.
        /// </summary>
        /// <param name="fileExtension">The extension of the file (without the dot).</param>
        /// <returns>The monaco language id</returns>
        public string GetLanguage(string fileExtension)
        {
            switch (fileExtension.ToLower())
            {
                case "abap":
                    return "abap";
                case "aes":
                    return "aes";
                case "cls":
                    return "apex";
                // TO-DO: AZCLI 
                case "bat":
                case "cmd":
                case "btm":
                    return "bat";
                case "c":
                case "h":
                    return "c";
                case "ligo":
                    // TO-DO: differentiate the different ligo languages
                    return "cameligo";
                case "clj":
                case "cljs":
                case "cljc":
                case "edn":
                    return "clojure";
                case "coffee":
                case "litcoffee":
                    return "coffeescript";
                case "cc":
                case "cpp":
                case "cxx":
                case "c++":
                case "hh":
                case "hpp":
                case "hxx":
                case "h++":
                    return "cpp";
                case "cs":
                case "csx":
                    return "csharp";
                case "css":
                    return "css";
                case "dart":
                    return "dart";
                case "dockerfile":
                    return "dockerfile";
                case "fs":
                case "fsi":
                case "fsx":
                case "fsscript":
                    return "fsharp";
                case "go":
                    return "go";
                case "graphql":
                    return "graphql";
                case "html":
                case "htm":
                    return "html";
                case "ini":
                    return "ini";
                case "java":
                case "class":
                case "jar":
                    return "java";
                case "js":
                case "cjs":
                case "mjs":
                    return "javascript";
                case "json":
                    return "json";
                case "jl":
                    return "julia";
                case "kt":
                case "kts":
                case "ktm":
                    return "kotlin";
                case "less":
                    return "less";
                case "lua":
                    return "lua";
                case "i3":
                case "m3":
                    return "m3";
                // Markdown already implemented. Don't uncomment
                /*
                case "md":
                case "markdown":
                    return "markdown";
                    
                */
                case "s":
                    return "mips";
                case "sql":
                    // TO-DO: differentiate the different sql languages
                    return "sql";
                case "m":
                case "mm":
                    return "objective-c";
                case "pp":
                case "pas":
                    return "pascal";
                case "pl":
                case "plx":
                case "pm":
                case "xs":
                case "t":
                case "pod":
                    return "perl";
                case "php":
                case "phtml":
                case "php3":
                case "php4":
                case "php5":
                case "php7":
                case "phps":
                case "php-s":
                case "pht":
                case "phar":
                    return "php";
                case "pq":
                    return "powerquery";
                case "ps1":
                case "ps1xml":
                case "psc1":
                case "psd1":
                case "psm1":
                case "pssc":
                case "psrc":
                case "cdxml":
                    return "powershell";
                case "py":
                case "pyi":
                case "pyc":
                case "pyd":
                case "pyo":
                case "pyw":
                case "pyz":
                    return "python";
                case "r":
                case "rdata":
                case "rds":
                case "rda":
                    return "r";
                case "razor":
                case "cshtml":
                case "vbhtml":
                    return "razor";
                case "rst":
                    return "restructuredtext";
                case "rb":
                    return "ruby";
                case "rs":
                    return "rust";
                case "sb":
                case "smallbasic":
                    return "sb";
                case "sc":
                case "scala":
                    return "scala";
                case "scm":
                case "ss":
                    return "scheme";
                case "sass":
                case "scss":
                    return "scss";
                case "sh":
                    return "shell";
                case "st":
                case "stx":
                    return "st";
                case "swift":
                    return "swift";
                case "sv":
                case "svh":
                    return "systemverilog";
                case "tcl":
                case "tbc":
                    return "tcl";
                case "ts":
                case "tsx":
                    return "typescript";
                case "vb":
                    return "vb";
                case "v":
                case "vh":
                    return "verilog";
                case "xml":
                    return "xml";
                case "yaml":
                case "yml":
                    return "yaml";
                default:
                    return "plaintext";
            }
        }
    }
}