using System;
using System.Collections.Generic;
using System.Text;

namespace monacoPreview
{
    class FileHandler
    {
        // This class has all functions in it, which are used to handle files
        public string GetLanguage(string fileExtension)
        {
            // This function returns the name of a file by taking a file extension
            String language;
            switch (fileExtension.ToLower())
            {
                case "abap":
                    language = "abap";
                    break;
                case "aes":
                    language = "aes";
                    break;
                case "cls":
                    language = "apex";
                    break;
                // TO-DO: AZCLI 
                case "bat":
                case "cmd":
                case "btm":
                    language = "bat";
                    break;
                case "c":
                case "h":
                    language = "c";
                    break;
                case "ligo":
                    // TO-DO: differentiate the different ligo languages
                    language = "cameligo";
                    break;
                case "clj":
                case "cljs":
                case "cljc":
                case "edn":
                    language = "clojure";
                    break;
                case "coffee":
                case "litcoffee":
                    language = "coffeescript";
                    break;
                case "cc":
                case "cpp":
                case "cxx":
                case "c++":
                case "hh":
                case "hpp":
                case "hxx":
                case "h++":
                    language = "cpp";
                    break;
                case "cs":
                case "csx":
                    language = "csharp";
                    break;
                case "css":
                    language = "css";
                    break;
                case "dart":
                    language = "dart";
                    break;
                case "dockerfile":
                    language = "dockerfile";
                    break;
                case "fs":
                case "fsi":
                case "fsx":
                case "fsscript":
                    language = "fsharp";
                    break;
                case "go":
                    language = "go";
                    break;
                case "graphql":
                    language = "graphql";
                    break;
                case "html":
                case "htm":
                    language = "html";
                    break;
                case "ini":
                    language = "ini";
                    break;
                case "java":
                case "class":
                case "jar":
                    language = "java";
                    break;
                case "js":
                case "cjs":
                case "mjs":
                    language = "javascript";
                    break;
                case "json":
                    language = "json";
                    break;
                case "jl":
                    language = "julia";
                    break;
                case "kt":
                case "kts":
                case "ktm":
                    language = "kotlin";
                    break;
                case "less":
                    language = "less";
                    break;
                case "lua":
                    language = "lua";
                    break;
                case "i3":
                case "m3":
                    language = "m3";
                    break;
                // Markdown already implemented. Don't uncomment
                /*
                case "md":
                case "markdown":
                    Language = "markdown";
                    break;
                */
                case "s":
                    language = "mips";
                    break;
                case "sql":
                    // TO-DO: differentiate the different sql languages
                    language = "sql";
                    break;
                case "m":
                case "mm":
                    language = "objective-c";
                    break;
                case "pp":
                case "pas":
                    language = "pascal";
                    break;
                case "pl":
                case "plx":
                case "pm":
                case "xs":
                case "t":
                case "pod":
                    language = "perl";
                    break;
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
                    language = "php";
                    break;
                case "pq":
                    language = "powerquery";
                    break;
                case "ps1":
                case "ps1xml":
                case "psc1":
                case "psd1":
                case "psm1":
                case "pssc":
                case "psrc":
                case "cdxml":
                    language = "powershell";
                    break;
                case "py":
                case "pyi":
                case "pyc":
                case "pyd":
                case "pyo":
                case "pyw":
                case "pyz":
                    language = "python";
                    break;
                case "r":
                case "rdata":
                case "rds":
                case "rda":
                    language = "r";
                    break;
                case "razor":
                case "cshtml":
                case "vbhtml":
                    language = "razor";
                    break;
                case "rst":
                    language = "restructuredtext";
                    break;
                case "rb":
                    language = "ruby";
                    break;
                case "rs":
                    language = "rust";
                    break;
                case "sb":
                case "smallbasic":
                    language = "sb";
                    break;
                case "sc":
                case "scala":
                    language = "scala";
                    break;
                case "scm":
                case "ss":
                    language = "scheme";
                    break;
                case "sass":
                case "scss":
                    language = "scss";
                    break;
                case "sh":
                    language = "shell";
                    break;
                case "st":
                case "stx":
                    language = "st";
                    break;
                case "swift":
                    language = "swift";
                    break;
                case "sv":
                case "svh":
                    language = "systemverilog";
                    break;
                case "tcl":
                case "tbc":
                    language = "tcl";
                    break;
                case "ts":
                case "tsx":
                    language = "typescript";
                    break;
                case "vb":
                    language = "vb";
                    break;
                case "v":
                case "vh":
                    language = "verilog";
                    break;
                case "xml":
                    language = "xml";
                    break;
                case "yaml":
                case "yml":
                    language = "yaml";
                    break;
                default:
                    language = "plaintext";
                    break;
            }
            return language;
        }
    }
}
