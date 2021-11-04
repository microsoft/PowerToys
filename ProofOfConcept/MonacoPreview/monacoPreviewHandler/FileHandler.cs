using System;
using System.Collections.Generic;
using System.Text;

namespace monacoPreview
{
    class FileHandler
    {
        // This class has all functions in it, which are used to handle files
        public string GetLanguage(string FileExtension)
        {
            // This function returns the name of a file by taking a file extension
            String Language;
            switch (FileExtension.ToLower())
            {
                case "abap":
                    Language = "abap";
                    break;
                case "aes":
                    Language = "aes";
                    break;
                case "cls":
                    Language = "apex";
                    break;
                // TO-DO: AZCLI 
                case "bat":
                case "cmd":
                case "btm":
                    Language = "bat";
                    break;
                case "c":
                case "h":
                    Language = "c";
                    break;
                case "ligo":
                    // TO-DO: differentiate the different ligo languages
                    Language = "cameligo";
                    break;
                case "clj":
                case "cljs":
                case "cljc":
                case "edn":
                    Language = "clojure";
                    break;
                case "coffee":
                case "litcoffee":
                    Language = "coffeescript";
                    break;
                case "cc":
                case "cpp":
                case "cxx":
                case "c++":
                case "hh":
                case "hpp":
                case "hxx":
                case "h++":
                    Language = "cpp";
                    break;
                case "cs":
                case "csx":
                    Language = "csharp";
                    break;
                // TO-DO: Add Communicating sequential processes file extension
                case "css":
                    Language = "css";
                    break;
                case "dart":
                    Language = "dart";
                    break;
                case "dockerfile":
                    Language = "dockerfile";
                    break;
                // TO-DO: add ecl
                case "fs":
                case "fsi":
                case "fsx":
                case "fsscript":
                    Language = "fsharp";
                    break;
                case "go":
                    Language = "go";
                    break;
                case "graphql":
                    Language = "graphql";
                    break;
                // TO-DO: Add handlebars
                // TO-DO: Add hashicorp configuration language
                case "html":
                case "htm":
                    Language = "html";
                    break;
                case "ini":
                    Language = "ini";
                    break;
                case "java":
                case "class":
                case "jar":
                    Language = "java";
                    break;
                case "js":
                case "cjs":
                case "mjs":
                    Language = "javascript";
                    break;
                case "json":
                    Language = "json";
                    break;
                case "jl":
                    Language = "julia";
                    break;
                case "kt":
                case "kts":
                case "ktm":
                    Language = "kotlin";
                    break;
                case "less":
                    Language = "less";
                    break;
                // TO-DO: Add lexon
                case "lua":
                    Language = "lua";
                    break;
                case "i3":
                case "m3":
                    Language = "m3";
                    break;
                // Markdown already implemented. Don't uncomment
                /*
                case "md":
                case "markdown":
                    Language = "markdown";
                    break;
                */
                case "s":
                    Language = "mips";
                    break;
                // TO-DO: Add msdax
                case "sql":
                    // TO-DO: differentiate the different sql languages
                    Language = "sql";
                    break;
                case "m":
                case "mm":
                    Language = "objective-c";
                    break;
                case "pp":
                case "pas":
                    Language = "pascal";
                    break;
                // TO-DO: Pascaligo
                case "pl":
                case "plx":
                case "pm":
                case "xs":
                case "t":
                case "pod":
                    Language = "perl";
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
                    Language = "php";
                    break;
                // TO-DO: Postiats
                case "pq":
                    Language = "powerquery";
                    break;
                case "ps1":
                case "ps1xml":
                case "psc1":
                case "psd1":
                case "psm1":
                case "pssc":
                case "psrc":
                case "cdxml":
                    Language = "powershell";
                    break;
                // TO-DO: Pug
                case "py":
                case "pyi":
                case "pyc":
                case "pyd":
                case "pyo":
                case "pyw":
                case "pyz":
                    Language = "python";
                    break;
                case "r":
                case "rdata":
                case "rds":
                case "rda":
                    Language = "r";
                    break;
                case "razor":
                case "cshtml":
                case "vbhtml":
                    Language = "razor";
                    break;
                // TO-DO: redis
                // TO-DO: amazon redshift
                case "rst":
                    Language = "restructuredtext";
                    break;
                case "rb":
                    Language = "ruby";
                    break;
                case "rs":
                    Language = "rust";
                    break;
                case "sb":
                case "smallbasic":
                    Language = "sb";
                    break;
                case "sc":
                case "scala":
                    Language = "scala";
                    break;
                case "scm":
                case "ss":
                    Language = "scheme";
                    break;
                case "sass":
                case "scss":
                    Language = "scss";
                    break;
                case "sh":
                    Language = "shell";
                    break;
                // TO-DO: sol
                case "st":
                case "stx":
                    Language = "st";
                    break;
                case "swift":
                    Language = "swift";
                    break;
                case "sv":
                case "svh":
                    Language = "systemverilog";
                    break;
                case "tcl":
                case "tbc":
                    Language = "tcl";
                    break;
                // TO-DO: twig
                case "ts":
                case "tsx":
                    Language = "typescript";
                    break;
                case "vb":
                    Language = "vb";
                    break;
                case "v":
                case "vh":
                    Language = "verilog";
                    break;
                case "xml":
                    // TO-DO: Take every file that starts with "<?xml" or just add more xml formats
                    Language = "xml";
                    break;
                case "yaml":
                case "yml":
                    Language = "yaml";
                    break;
                default:
                    Language = "plaintext";
                    break;
            }
            return Language;
        }
    }
}
