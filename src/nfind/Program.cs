using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace nfind
{
    class Program
    {
        static void Main(string[] args)
        {
            var showHelp = false;
            var insensitive = false;
            var recurse = false;
            var initialDirectory = Environment.CurrentDirectory;

            var options = new Mono.Options.OptionSet
            {
                { $"nfind.exe, version {ThisAssembly.AssemblyInformationalVersion}" },
                { "" },
                { "Searches text files for strings matching a given regular expression." },
                { "" },
                { "Usage:" },
                { "  nfind [<options>] <regex> <file pattern> [<file pattern> ...]" },
                { "" },
                { "Options:" },
                { "h|?|help", "Show help and exit", v => showHelp = v != null },
                { "i|insensitive", "Perform a case-insensitive match", v => insensitive = v != null },
                { "r|recurse", "Recursively search subdirectories", v => recurse = v != null },
                { "d=|directory=", "Directory to search",  v => initialDirectory = v },
                { "" },
                { "Further Reading:" },
                { "  https://docs.microsoft.com/en-us/dotnet/standard/base-types/regular-expression-language-quick-reference" }
            };

            var positionalArgs = options.Parse(args);

            if (showHelp)
            {
                options.WriteOptionDescriptions(Console.Out);
                return;
            }

            var regexOptions = RegexOptions.Compiled | RegexOptions.Singleline;
            if (insensitive)
            {
                regexOptions |= RegexOptions.IgnoreCase;
            }
            var regex = new Regex(positionalArgs[0], regexOptions);

            var patterns = positionalArgs.Skip(1);

            initialDirectory = Path.GetFullPath(initialDirectory);

            IEnumerable<string> directories = new[] { initialDirectory };
            if (recurse)
            {
                directories = directories.Concat(Directory.EnumerateDirectories(initialDirectory, "*", SearchOption.AllDirectories));
            }

            foreach (var directory in directories)
            {
                var filePaths = new List<string>();
                foreach (var pattern in patterns)
                {
                    filePaths.AddRange(Directory.EnumerateFiles(directory, pattern));
                }
                filePaths.Sort(StringComparer.CurrentCulture);

                foreach (var filePath in filePaths)
                {
                    int lineNumber = 1;
                    foreach (var line in File.ReadLines(filePath))
                    {
                        if (regex.IsMatch(line))
                        {
                            Console.WriteLine($"{filePath}, {lineNumber}: {line}");
                        }

                        lineNumber++;
                    }
                }
            }
        }
    }
}
