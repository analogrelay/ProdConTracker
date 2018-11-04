using Microsoft.EntityFrameworkCore;

namespace TrackerGen.Data
{
    public class ProdConTrackerDbContext : DbContext
    {
        public DbSet<OrchestratedBuild> OrchestratedBuilds { get; set; }
        public DbSet<Build> Builds { get; set; }

        public ProdConTrackerDbContext(DbContextOptions options) : base(options)
        {
        }
    }
}
