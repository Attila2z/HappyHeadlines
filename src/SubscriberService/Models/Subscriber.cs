using System.ComponentModel.DataAnnotations;

namespace SubscriberService.Models
{
    public class Subscriber
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public DateTime SubscribedAt { get; set; } = DateTime.UtcNow;
    }

    public class SubscribeRequest
    {
        [Required]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Name { get; set; } = string.Empty;
    }

    public class FeatureFlags
    {
        public bool SubscriberServiceEnabled { get; set; } = true;
    }
}
