// =============================================================================
// Services/DatabaseRouter.cs
// =============================================================================
// This is the KEY class for the Z-axis split.
//
// WHAT IT DOES:
//   - Holds 8 database connections (one per continent)
//   - GetContextFor(continent)  → returns the right database for that continent
//   - GetAllContexts()          → returns all 8 databases (used for GET /articles)
//   - GetContextsFor(continent) → returns correct databases for saving:
//                                  if Global → returns ALL 8
//                                  otherwise → returns just that continent's DB
// =============================================================================

using Microsoft.EntityFrameworkCore;
using ArticleService.Data;
using ArticleService.Models;

namespace ArticleService.Services
{
    public class DatabaseRouter
    {
        // One DbContext per continent, stored in a dictionary
        // Key = continent name, Value = database context
        private readonly Dictionary<string, ArticleDbContext> _contexts;

        public DatabaseRouter(IConfiguration config)
        {
            _contexts = new Dictionary<string, ArticleDbContext>();

            // Create a DbContext for each continent using its connection string
            foreach (var continent in Continents.All)
            {
                var connectionString = config.GetConnectionString(continent);

                if (string.IsNullOrEmpty(connectionString))
                    throw new Exception($"Missing connection string for continent: {continent}");

                var options = new DbContextOptionsBuilder<ArticleDbContext>()
                    .UseNpgsql(connectionString)
                    .Options;

                _contexts[continent] = new ArticleDbContext(options);
            }
        }

        // -----------------------------------------------------------------------
        // Returns the single database for a specific continent
        // e.g. GetContextFor("Europe") → returns the Europe DB
        // -----------------------------------------------------------------------
        public ArticleDbContext GetContextFor(string continent)
        {
            if (!_contexts.ContainsKey(continent))
                throw new ArgumentException($"Unknown continent: {continent}");

            return _contexts[continent];
        }

        // -----------------------------------------------------------------------
        // Returns ALL 8 databases
        // Used by GET /articles to fetch from every database
        // -----------------------------------------------------------------------
        public List<ArticleDbContext> GetAllContexts()
        {
            return _contexts.Values.ToList();
        }

        // -----------------------------------------------------------------------
        // Returns the databases WHERE an article should be SAVED
        // If continent = "Global" → save to ALL 8 databases
        // Otherwise              → save to just that continent's database
        // -----------------------------------------------------------------------
        public List<ArticleDbContext> GetContextsForSaving(string continent)
        {
            if (continent == Continents.Global)
                return GetAllContexts(); // Global = save everywhere

            return new List<ArticleDbContext> { GetContextFor(continent) };
        }

        // -----------------------------------------------------------------------
        // Ensures all 8 databases have their tables created
        // Called once on startup from Program.cs
        // -----------------------------------------------------------------------
        public void MigrateAll()
        {
            foreach (var (continent, context) in _contexts)
            {
                context.Database.Migrate();
            }
        }
    }
}