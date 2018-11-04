using Microsoft.Extensions.Logging;

namespace Microsoft.Internal.Utilities
{
    public class ToolSet
    {
        private Tool _git;

        public Tool Git => _git ?? throw new CommandLineException("Unable to locate 'git' executable!");

        public ToolSet(ILoggerFactory loggerFactory)
        {
            _git = Tool.Locate("git", loggerFactory);
        }
    }
}
