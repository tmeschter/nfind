using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks.Dataflow;

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

            // Create the Dataflow blocks
            var fileReaderBlock = GetFileReaderBlock();
            var matchingBlock = GetMatchBlock(regex);
            var outputBlock = GetOutputBlock();

            // Link the blocks
            var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };
            fileReaderBlock.LinkTo(matchingBlock, linkOptions);
            matchingBlock.LinkTo(outputBlock, linkOptions);

            // Find the applicable files and pass them to the file reader.
            DirectoryInfo di = new DirectoryInfo(initialDirectory);

            foreach (var matchingFile in GetMatchingFiles(initialDirectory, patterns, recurse))
            {
                fileReaderBlock.Post(matchingFile);
            }

            // Inform the file reader that there will be no more input, and wait for the output
            // block to finish.
            fileReaderBlock.Complete();
            outputBlock.Completion.Wait();
        }

        public static IEnumerable<File> GetMatchingFiles(string initialDirectory, IEnumerable<string> patterns, bool recurse)
        {
            IEnumerable<string> directories = new[] { initialDirectory };
            if (recurse)
            {
                directories = directories.Concat(Directory.EnumerateDirectories(initialDirectory, "*", SearchOption.AllDirectories));
            }

            var filePaths = new List<string>();
            foreach (var directory in directories)
            {
                filePaths.Clear();

                foreach (var pattern in patterns)
                {
                    filePaths.AddRange(Directory.EnumerateFiles(directory, pattern));
                }
                filePaths.Sort(StringComparer.CurrentCulture);

                foreach (var filePath in filePaths)
                {
                    var relativeFilePath = filePath.Substring(initialDirectory.Length + 1);
                    yield return new File
                    {
                        SearchDirectory = initialDirectory,
                        RelativeFilePath = relativeFilePath
                    };
                }
            }
        }

        private static TransformManyBlock<File, MatchingLine> GetFileReaderBlock()
        {
            var dataflowBlockOptions = new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded,
                NameFormat = "File readering"
            };
            return new TransformManyBlock<File, MatchingLine>((Func<File, IEnumerable<MatchingLine>>)GetLines, dataflowBlockOptions);
        }

        private static IEnumerable<MatchingLine> GetLines(File file)
        {
            int lineNumber = 1;
            foreach (var line in System.IO.File.ReadLines(Path.Combine(file.SearchDirectory, file.RelativeFilePath)))
            {
                yield return new MatchingLine
                {
                    File = file,
                    LineNumber = lineNumber,
                    Text = line
                };

                lineNumber++;
            }
        }

        private static TransformBlock<MatchingLine, MatchingLine> GetMatchBlock(Regex regex)
        {
            var dataflowBlockOptions = new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded,
                NameFormat = "Matching"
            };
            return new TransformBlock<MatchingLine, MatchingLine>(line =>
            {
                line.Matches = regex.Matches(line.Text)
                    .Cast<Match>()
                    .Select(m => new MatchSpan(m.Index, m.Length))
                    .ToArray();
                return line;
            },
            dataflowBlockOptions);
        }

        private static ActionBlock<MatchingLine> GetOutputBlock()
        {
            var dataflowBlockOptions = new ExecutionDataflowBlockOptions
            {
                NameFormat = "Output"
            };
            return new ActionBlock<MatchingLine>(m =>
            {
                if (m.Matches.Length == 0)
                {
                    return;
                }

                var foregroundColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"{m.File.RelativeFilePath}, {m.LineNumber}: ");
                Console.ForegroundColor = foregroundColor;

                int unmatchedStart = 0;
                for (int i = 0; i < m.Matches.Length; i++)
                {
                    var match = m.Matches[i];

                    Console.Write(m.Text.Substring(unmatchedStart, match.Index - unmatchedStart));

                    Console.ForegroundColor = (int)foregroundColor > 7 ? foregroundColor - 8 : foregroundColor + 8;
                    Console.Write(m.Text.Substring(match.Index, match.Length));
                    Console.ForegroundColor = foregroundColor;

                    unmatchedStart = match.Index + match.Length;
                }

                Console.WriteLine(m.Text.Substring(unmatchedStart));
            },
            dataflowBlockOptions);
        }
    }

    public sealed class File
    {
        public string SearchDirectory { get; set; }
        public string RelativeFilePath { get; set; }
    }

    public sealed class MatchingLine
    {
        public File File { get; set; }
        public int LineNumber { get; set; }
        public string Text { get; set; }
        public MatchSpan[] Matches { get; set; }
    }

    public struct MatchSpan
    {
        public MatchSpan(int index, int length)
        {
            Index = index;
            Length = length;
        }

        public int Index { get; }
        public int Length { get; }
    }
}
