using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace nfind
{
    class Program
    {
        static void Main(string[] args)
        {
            var showHelp = false;
            var insensitive = false;
            var recurse = false;
            var directory = Environment.CurrentDirectory;

            var options = new Mono.Options.OptionSet
            {
                { "h|help", "Show help", v => showHelp = v != null },
                { "i", "Perform a case-insensitive match", v => insensitive = v != null },
                { "r|recurse", "Recursively search subdirectories", v => recurse = v != null },
                { "d=|directory=", "Directory to search",  v => directory = v }
            };

            var positionalArgs = options.Parse(args);

            if (showHelp)
            {
                throw new NotImplementedException();
            }

            var regexOptions = RegexOptions.Compiled | RegexOptions.Singleline;
            if (insensitive)
            {
                regexOptions |= RegexOptions.IgnoreCase;
            }
            var regex = new Regex(positionalArgs[0], regexOptions);

            var patterns = positionalArgs.Skip(1);

            directory = Path.GetFullPath(directory);
            var searchOptions = recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            foreach (var pattern in patterns)
            {
                foreach (var filePath in Directory.EnumerateFiles(directory, pattern, searchOptions))
                {
                    foreach (var line in File.ReadLines(filePath))
                    {
                        if (regex.IsMatch(line))
                        {
                            Console.WriteLine($"{filePath}: {line}");
                        }
                    }
                }
            }
        }
    }
}
