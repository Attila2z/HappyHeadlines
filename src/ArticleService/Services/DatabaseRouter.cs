using Microsoft.EntityFrameworkCore;
using ArticleService.Data;
using ArticleService.Models;

namespace ArticleService.Services
{
    public class DatabaseRouter
    {
        private readonly Dictionary<string, DbContextOptions<ArticleDbContext>> _options;

        public DatabaseRouter(IConfiguration config)
        {
            _options = new Dictionary<string, DbContextOptions<ArticleDbContext>>();

            foreach (var continent in Continents.All)
            {
                var connectionString = config.GetConnectionString(continent);

                if (string.IsNullOrEmpty(connectionString))
                    throw new Exception($"Missing connection string for continent: {continent}");

                var options = new DbContextOptionsBuilder<ArticleDbContext>()
                    .UseNpgsql(connectionString)
                    .Options;

                _options[continent] = options;
            }
        }

        public ArticleDbContext CreateContextFor(string continent)
        {
            if (!_options.ContainsKey(continent))
                throw new ArgumentException($"Unknown continent: {continent}");

            return new ArticleDbContext(_options[continent]);
        }

        public IReadOnlyList<string> GetContinents() => _options.Keys.ToList();

        public List<ArticleDbContext> CreateContextsForSaving(string continent)
        {
            if (continent == Continents.Global)
                return _options.Values.Select(opts => new ArticleDbContext(opts)).ToList();

            return new List<ArticleDbContext> { CreateContextFor(continent) };
        }

        public void MigrateAll()
        {
            foreach (var (_, opts) in _options)
            {
                using var context = new ArticleDbContext(opts);
                context.Database.Migrate();
            }
        }
    }
}
