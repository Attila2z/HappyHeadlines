using Microsoft.EntityFrameworkCore;
using ArticleService.Data;
using ArticleService.Models;

namespace ArticleService.Services
{
    public class DatabaseRouter
    {
        private readonly Dictionary<string, ArticleDbContext> _contexts;

        public DatabaseRouter(IConfiguration config)
        {
            _contexts= new Dictionary<string, ArticleDbContext>();

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

        public ArticleDbContext GetContextFor(string continent)
        {
            if (!_contexts.ContainsKey(continent))
                throw new ArgumentException($"Unknown continent: {continent}");

            return _contexts[continent];
        }

        public List<ArticleDbContext> GetAllContexts()
        {
            return _contexts.Values.ToList();
        }

        public List<ArticleDbContext> GetContextsForSaving(string continent)
        {
            if (continent == Continents.Global)
                return GetAllContexts();

            return new List<ArticleDbContext> { GetContextFor(continent) };
        }

        public void MigrateAll()
        {
            foreach (var (continent, context) in _contexts)
            {
                context.Database.Migrate();
            }
        }
    }
}
