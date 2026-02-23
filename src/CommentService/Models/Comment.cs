using System.ComponentModel.DataAnnotations;

namespace CommentService.Models
{
    public class Comment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ArticleId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Author { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        public string Status { get; set; } = CommentStatus.PendingReview;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class CommentRequest
    {
        [Required]
        public int ArticleId { get; set; }

        [Required]
        public string Author { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;
    }

    public static class CommentStatus
    {
        public const string Approved      = "Approved";
        public const string PendingReview = "PendingReview";
    }
}