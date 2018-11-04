using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Internal.Utilities
{
    public class CommandBuilder
    {
        private Action<string> _standardOutputHandler;
        private Action<string> _standardErrorHandler;
        private readonly ILogger _logger;

        public string ExecutablePath { get; }
        public IReadOnlyList<string> Arguments { get; }
        public bool LaunchInShell { get; }
        public string WorkingDirectory { get; private set; }

        public CommandBuilder(string executablePath, IReadOnlyList<string> arguments, bool launchInShell, ILogger logger = null)
        {
            ExecutablePath = executablePath;
            Arguments = arguments;
            LaunchInShell = launchInShell;
            _logger = logger ?? NullLogger.Instance;
        }

        public CommandBuilder InDirectory(string directory)
        {
            WorkingDirectory = directory;
            return this;
        }

        public CommandBuilder OnStandardOutput(Action<string> handler)
        {
            _standardOutputHandler = handler;
            return this;
        }

        public CommandBuilder OnStandardError(Action<string> handler)
        {
            _standardErrorHandler = handler;
            return this;
        }

        public Task<CommandResult> ExecuteAsync(bool throwOnFailure = false)
        {
            var tcs = new TaskCompletionSource<CommandResult>();
            var exeName = Path.GetFileNameWithoutExtension(ExecutablePath);
            var formattedArgs = ArgumentEscaper.EscapeAndConcatenate(Arguments);
            var process = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = ExecutablePath,
                    Arguments = formattedArgs,
                    WorkingDirectory = WorkingDirectory,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                }
            };

            process.EnableRaisingEvents = true;

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            process.ErrorDataReceived += (sender, a) => ProcessDataReceived(a, stderr, _standardErrorHandler);
            process.OutputDataReceived += (sender, a) => ProcessDataReceived(a, stdout, _standardOutputHandler);

            process.Exited += (sender, a) =>
            {
                _logger.LogDebug("'{Command} {Arguments}' exited with code {ExitCode}", exeName, formattedArgs, process.ExitCode);
                if (process.ExitCode != 0 && throwOnFailure)
                {
                    tcs.TrySetException(new CommandLineException($"Command '{exeName} {formattedArgs}' failed with exit code {process.ExitCode}!"));
                }
                else
                {
                    tcs.TrySetResult(new CommandResult(process.ExitCode, stdout.ToString(), stderr.ToString()));
                }
            };

            _logger.LogInformation("Running '{Command} {Arguments}'", exeName, formattedArgs);
            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
            return tcs.Task;

            void ProcessDataReceived(DataReceivedEventArgs args, StringBuilder buffer, Action<string> handler)
            {
                buffer.AppendLine(args.Data);
                handler?.Invoke(args.Data);
                _logger.LogDebug(args.Data);
            }
        }

        private string FormatArguments(IReadOnlyList<string> arguments)
        {
            // I know there are definitely issues with this...
            return string.Join(" ", arguments.Select(a => $"\"{a}\""));
        }
    }
}
