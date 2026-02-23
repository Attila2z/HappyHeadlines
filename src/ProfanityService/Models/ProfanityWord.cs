using System.ComponentModel.DataAnnotations;

namespace ProfanityService.Models
{
    // Stored in the database — each row is one banned word
    public class ProfanityWord
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Word { get; set; }
    }

    // What CommentService sends to us
    public class FilterRequest
    {
        [Required]
        public string Text { get; set; }
    }

    // What we send back
    public class FilterResponse
    {
        public string OriginalText { get; set; }
        public string FilteredText { get; set; }
        public bool HadProfanity  { get; set; }
    }
}