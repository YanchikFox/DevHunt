// Example usage of EncryptionService for storing sensitive data
// For example, integration tokens with GitHub, GitLab, etc.

namespace DevHunt.CoreApi.Security;

/*
 * Usage example:
 *
 * public class IntegrationController : ControllerBase
 * {
 *     private readonly IEncryptionService _encryption;
 *
 *     public IntegrationController(IEncryptionService encryption)
 *     {
 *         _encryption = encryption;
 *     }
 *
 *     [HttpPost("github/token")]
 *     public async Task<IActionResult> StoreGitHubToken([FromBody] string token)
 *     {
 *         // Encrypt token before saving to DB
 *         var encryptedToken = _encryption.Encrypt(token);
 *
 *         // Save encryptedToken to database
 *         // Instead of storing plaintext token
 *
 *         return Ok();
 *     }
 *
 *     [HttpGet("github/token")]
 *     public async Task<IActionResult> GetGitHubToken()
 *     {
 *         // Get encryptedToken from DB
 *         var encryptedToken = "...";
 *
 *         // Decrypt for use
 *         var decryptedToken = _encryption.Decrypt(encryptedToken);
 *
 *         return Ok();
 *     }
 * }
 *
 * IMPORTANT:
 * - Never log decrypted tokens
 * - Store encryption keys in environment variables/KeyVault
 * - Use different keys for different environments
 */

