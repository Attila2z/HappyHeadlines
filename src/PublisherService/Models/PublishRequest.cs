using System.ComponentModel.DataAnnotations;

namespace PublisherService.Models
{
    public class PublishRequest
    {
        [Required]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        [Required]
        public string Author { get; set; } = string.Empty;

        [Required]
        public string Continent { get; set; } = string.Empty;
    }

    public class ArticleQueueMessage
    {
        public string Title     { get; set; } = string.Empty;
        public string Content   { get; set; } = string.Empty;
        public string Author    { get; set; } = string.Empty;
        public string Continent { get; set; } = string.Empty;
    }
}
