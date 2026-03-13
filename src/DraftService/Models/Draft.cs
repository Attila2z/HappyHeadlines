using System.ComponentModel.DataAnnotations;

namespace DraftService.Models
{
    public class Draft
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [StringLength(255)]
        public string Title { get; set; }

        [Required]
        public string Content { get; set; }

        [Required]
        [StringLength(100)]
        public string AuthorId { get; set; }

        /// <summary>
        /// Status of the draft: Draft, Submitted, Approved, Rejected
        /// </summary>
        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Draft";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public DateTime? SubmittedAt { get; set; }

        /// <summary>
        /// Tags for categorizing the article
        /// </summary>
        [StringLength(500)]
        public string Tags { get; set; }

        /// <summary>
        /// Summary/excerpt of the article
        /// </summary>
        [StringLength(500)]
        public string Summary { get; set; }
    }
}
