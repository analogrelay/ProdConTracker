using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LibGit2Sharp;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Internal.Utilities;
using TrackerGen.Data;

namespace TrackerGen
{
    [Command(Name, Description = "Generate/update json data files")]
    internal class ImportCommand
    {
        public const string Name = "import";

        private static readonly string DefaultRepositoryUrl = "https://github.com/dotnet/versions";
        private static readonly string DefaultRepositoryPath = "build-info/dotnet/product/cli";

        private readonly IConsole _console;
        private readonly ILogger<ImportCommand> _logger;
        private readonly ToolSet _toolSet;

        [Option("--url <REPO>", Description = "The URL of the dotnet/versions repository to clone. Defaults to 'https://github.com/dotnet/versions'.")]
        public string RepositoryUrl { get; set; }

        [Option("--path <PATH>", Description = "The path within the repository containing the version data. Defaults to 'build-info/dotnet/product/cli'.")]
        public string RepositoryPath { get; set; }

        [Option("--work <PATH>", Description = "The path to a directory to use for working in. Defaults to a new temporary directory. If specified, the directory is NOT cleaned up afterwards.")]
        public string WorkDirectory { get; set; }

        [Option("--keep-work-directory", Description = "Specify this switch to stop the working directory from being cleaned up. Only applies when '--work-dir' is NOT specified.")]
        public bool KeepWorkDirectory { get; set; }

        [Option("--sql <SQL_CONNECTION_STRING>", Description = "A SQL Server Connection string for the database in which to write the data.")]
        public string SqlConnectionString { get; set; }

        public ImportCommand(IConsole console, ILogger<ImportCommand> logger, ToolSet toolSet)
        {
            _console = console;
            _logger = logger;
            _toolSet = toolSet;
        }

        public async Task<int> OnExecuteAsync()
        {
            // Initialize defaults
            RepositoryUrl = string.IsNullOrEmpty(RepositoryUrl) ? DefaultRepositoryUrl : RepositoryUrl;
            RepositoryPath = string.IsNullOrEmpty(RepositoryPath) ? DefaultRepositoryPath : RepositoryPath;

            if (string.IsNullOrEmpty(CosmosUrl))
            {
                _logger.LogError("Missing required option: '--cosmos-url'.");
                return 1;
            }

            if (string.IsNullOrEmpty(CosmosKey))
            {
                _logger.LogError("Missing required option: '--cosmos-key'.");
                return 1;
            }

            if (string.IsNullOrEmpty(CosmosDb))
            {
                _logger.LogError("Missing required option: '--cosmos-db'.");
                return 1;
            }

            if (string.IsNullOrEmpty(WorkDirectory))
            {
                WorkDirectory = Path.Combine(Path.GetTempPath(), $"TrackerGen_Work_{Guid.NewGuid().ToString("N")}");
            }
            else
            {
                KeepWorkDirectory = true;
            }

            try
            {
                // First, clone the repos
                _logger.LogInformation("Cloning {RepositoryUrl} to {WorkDirectory}...", RepositoryUrl, WorkDirectory);
                var repoPath = Repository.Clone(RepositoryUrl, WorkDirectory);

                using (var repo = new Repository(repoPath))
                {
                    // Create EF context
                    var options = new DbContextOptionsBuilder()
                        .UseSqlServer()
                        .Options
                    using (var context = new ProdConTrackerDbContext())
                    {
                        await context.Database.EnsureCreatedAsync();

                        // Open the catalog
                        await ImportCommitsAsync(context, repo);
                    }
                }
            }
            finally
            {
                if (!KeepWorkDirectory)
                {
                    _logger.LogDebug("Cleaning {WorkDirectory}...", WorkDirectory);
                    if (Directory.Exists(WorkDirectory))
                    {
                        DeleteDirectory(WorkDirectory);
                    }
                }
            }

            return 0;
        }

        private async Task ImportCommitsAsync(ProdConTrackerDbContext db, Repository repo)
        {
            // Local cache of builds that have been loaded in this pass.
            var loadedBuilds = new HashSet<string>();
            foreach (var commit in repo.Commits)
            {
                // Import the current commit
                _logger.LogInformation("Scanning for builds in commit: {Commit}", commit.Id.Sha);
                await ImportCommitAsync(db, repo, commit, loadedBuilds);
            }
        }

        private async Task ImportCommitAsync(ProdConTrackerDbContext db, Repository repo, Commit commit, HashSet<string> loadedBuilds)
        {
            // Search the tree for the files we care about
            var entry = commit.Tree[RepositoryPath];

            // Dive through the tree's children looking for build.xmls
            foreach (var child in entry.Target.Peel<Tree>())
            {
                await ProcessTreeAsync(db, child, prefix: string.Empty, commit, loadedBuilds);
            }
        }

        private async Task ProcessTreeAsync(ProdConTrackerDbContext db, TreeEntry entry, string prefix, Commit commit, HashSet<string> loadedBuilds)
        {
            var fullName = string.IsNullOrEmpty(prefix) ? entry.Name : $"{prefix}/{entry.Name}";
            if (entry.TargetType == TreeEntryTargetType.Tree)
            {
                foreach (var child in entry.Target.Peel<Tree>())
                {
                    await ProcessTreeAsync(db, child, prefix: fullName, commit, loadedBuilds);
                }
            }
            else if (entry.TargetType == TreeEntryTargetType.Blob && entry.Name.Equals("build.xml"))
            {
                await ImportBuildManifestAsync(db, entry.Target.Peel<LibGit2Sharp.Blob>(), branch: prefix, commit, loadedBuilds);
            }
            else
            {
                _logger.LogDebug("Skipping {Type} entry {Name}", entry.TargetType, fullName);
            }
        }

        private async Task ImportBuildManifestAsync(ProdConTrackerDbContext db, LibGit2Sharp.Blob blob, string branch, Commit commit, HashSet<string> loadedBuilds)
        {
            var orchBuild = BuildXmlLoader.Load(blob.GetContentText(), branch);

            // Check if the build already exists
            if (!loadedBuilds.Contains(orchBuild.OrchestratedBuildId) && !await db.OrchestratedBuilds.AnyAsync(b => b.OrchestratedBuildId == orchBuild.OrchestratedBuildId))
            {
                _logger.LogInformation("Importing build {BuildId} ...", orchBuild.OrchestratedBuildId);

                db.OrchestratedBuilds.Add(orchBuild);
                foreach (var build in orchBuild.Builds)
                {
                    db.Builds.Add(build);
                }

                _logger.LogInformation("Saving to database...");
                await db.SaveChangesAsync();
            }
        }

        // Special recursive delete because Git makes files non-writable. See https://github.com/libgit2/libgit2sharp/issues/1354
        private static void DeleteDirectory(string directory)
        {
            foreach (string subdirectory in Directory.EnumerateDirectories(directory))
            {
                DeleteDirectory(subdirectory);
            }

            foreach (string fileName in Directory.EnumerateFiles(directory))
            {
                var fileInfo = new FileInfo(fileName)
                {
                    Attributes = FileAttributes.Normal
                };
                fileInfo.Delete();
            }

            Directory.Delete(directory);
        }
    }
}
