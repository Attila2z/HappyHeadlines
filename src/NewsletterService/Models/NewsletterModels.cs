namespace NewsletterService.Models
{
    public class NewsletterRequest
    {
        public string Subject { get; set; } = string.Empty;
        public string Body    { get; set; } = string.Empty;
    }

    public class SubscriberDto
    {
        public int      Id           { get; set; }
        public string   Email        { get; set; } = string.Empty;
        public string   Name         { get; set; } = string.Empty;
        public DateTime SubscribedAt { get; set; }
    }

    public class ArticleDto
    {
        public int      Id        { get; set; }
        public string   Title     { get; set; } = string.Empty;
        public string   Author    { get; set; } = string.Empty;
        public string   Continent { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
