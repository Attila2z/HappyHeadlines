using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProfanityService.Data;
using ProfanityService.Models;

namespace ProfanityService.Controllers
{
    [ApiController]
    public class ProfanityController : ControllerBase
    {
        private readonly ProfanityDbContext _context;

        public ProfanityController(ProfanityDbContext context)
        {
            _context = context;
        }

        // -----------------------------------------------------------------------
        // FILTER — POST /profanity/filter
        // Called by CommentService to filter a comment.
        // Replaces all bad words with *** and returns the cleaned text.
        // -----------------------------------------------------------------------
        [HttpPost("/profanity/filter")]
        public async Task<IActionResult> Filter([FromBody] FilterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
                return BadRequest("Field 'text' is required.");

            // Load all banned words from the database
            var bannedWords = await _context.ProfanityWords
                .Select(w => w.Word.ToLower())
                .ToListAsync();

            var filteredText = request.Text;
            var hadProfanity = false;

            // Replace each bad word with *** (case insensitive)
            foreach (var word in bannedWords)
            {
                if (filteredText.ToLower().Contains(word))
                {
                    hadProfanity = true;
                    // Replace with stars of same length e.g. "damn" → "****"
                    var stars = new string('*', word.Length);
                    filteredText = System.Text.RegularExpressions.Regex.Replace(
                        filteredText,
                        System.Text.RegularExpressions.Regex.Escape(word),
                        stars,
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase
                    );
                }
            }

            return Ok(new FilterResponse
            {
                OriginalText = request.Text,
                FilteredText = filteredText,
                HadProfanity = hadProfanity
            });
        }

        // -----------------------------------------------------------------------
        // GET ALL WORDS — GET /profanity/words
        // -----------------------------------------------------------------------
        [HttpGet("/profanity/words")]
        public async Task<IActionResult> GetWords()
        {
            var words = await _context.ProfanityWords.ToListAsync();
            return Ok(words);
        }

        // -----------------------------------------------------------------------
        // ADD WORD — POST /profanity/words
        // Add a new banned word to the database
        // -----------------------------------------------------------------------
        [HttpPost("/profanity/words")]
        public async Task<IActionResult> AddWord([FromBody] ProfanityWord word)
        {
            if (string.IsNullOrWhiteSpace(word.Word))
                return BadRequest("Field 'word' is required.");

            // Store in lowercase for consistent matching
            word.Word = word.Word.ToLower().Trim();

            _context.ProfanityWords.Add(word);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetWords), word);
        }

        // -----------------------------------------------------------------------
        // DELETE WORD — DELETE /profanity/words/{id}
        // -----------------------------------------------------------------------
        [HttpDelete("/profanity/words/{id}")]
        public async Task<IActionResult> DeleteWord(int id)
        {
            var word = await _context.ProfanityWords.FindAsync(id);
            if (word == null)
                return NotFound($"Word with id={id} not found.");

            _context.ProfanityWords.Remove(word);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}