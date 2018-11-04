using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Microsoft.Internal.Utilities
{
    public class Tool
    {
        private readonly ILoggerFactory _loggerFactory;

        public string ExecutablePath { get; }
        public bool LaunchInShell { get; }

        public Tool(string executablePath, ILoggerFactory loggerFactory) : this(executablePath, loggerFactory, launchInShell: false) { }
        public Tool(string executablePath, ILoggerFactory loggerFactory, bool launchInShell)
        {
            ExecutablePath = executablePath;
            _loggerFactory = loggerFactory;
            LaunchInShell = launchInShell;
        }

        public CommandBuilder Arguments(params string[] arguments)
        {
            var exeName = Path.GetFileNameWithoutExtension(ExecutablePath);
            return new CommandBuilder(ExecutablePath, arguments, LaunchInShell, _loggerFactory?.CreateLogger(typeof(Tool).FullName + "::" + exeName));
        }

        public static Tool Locate(string name, ILoggerFactory loggerFactory = null)
        {
            // Search all PATH paths
            foreach (var path in Environment.GetEnvironmentVariable("PATH").Split(Path.PathSeparator))
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Search for an EXE first
                    var exe = Path.Combine(path, $"{name}.exe");
                    if (File.Exists(exe))
                    {
                        // Done!
                        return new Tool(exe, loggerFactory);
                    }

                    // Try a CMD/BAT file
                    var cmd = Path.Combine(path, $"{name}.cmd");
                    if (File.Exists(cmd))
                    {
                        return new Tool(cmd, loggerFactory, launchInShell: true);
                    }

                    var bat = Path.Combine(path, $"{name}.bat");
                    if (File.Exists(bat))
                    {
                        return new Tool(bat, loggerFactory, launchInShell: true);
                    }
                }
                else
                {
                    var exe = Path.Combine(path, name);
                    if (File.Exists(exe))
                    {
                        return new Tool(exe, loggerFactory);
                    }
                }
            }

            // Failed to locate the tool.
            return null;
        }
    }
}
