

using System.ComponentModel.DataAnnotations;

namespace ArticleService.Models
{
    public class Article
    {
       
        [Key]
        public int Id { get; set; }

        
        [Required]
        [MaxLength(200)]
        public string Title { get; set; }

        [Required]
        public string Content { get; set; }

        [Required]
        [MaxLength(100)]
        public string Author { get; set; }

        [Required]
        [MaxLength(50)]
        public string Continent { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // DTO — used for Create and Update requests (no Id from the client)
    public class ArticleRequest
    {
        [Required]
        public string Title { get; set; }

        [Required]
        public string Content { get; set; }

        [Required]
        public string Author { get; set; }

        [Required]

        public string Continent { get; set; }

    }

    public static class Continents
    {
        public const string Africa = "Africa";
        public const string Antarctica   = "Antarctica";
        public const string Asia         = "Asia";
        public const string Australia    = "Australia";
        public const string Europe       = "Europe";
        public const string NorthAmerica = "NorthAmerica";
        public const string SouthAmerica = "SouthAmerica";
        public const string Global       = "Global";
    

    public static readonly List<string> All = new()
    {
        Africa, Antarctica, Asia, Australia,
        Europe, NorthAmerica, SouthAmerica, Global
        };
    }
}