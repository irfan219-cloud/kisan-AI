using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using KisanMitraAI.Core.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;

namespace KisanMitraAI.Infrastructure.Security;

/// <summary>
/// Implementation of encryption service using AWS KMS for key management and AES-256 encryption
/// </summary>
public class EncryptionService : IEncryptionService
{
    private readonly IAmazonKeyManagementService _kmsClient;
    private readonly ILogger<EncryptionService> _logger;
    private readonly string _kmsKeyId;

    public EncryptionService(
        IAmazonKeyManagementService kmsClient,
        IConfiguration configuration,
        ILogger<EncryptionService> logger)
    {
        _kmsClient = kmsClient ?? throw new ArgumentNullException(nameof(kmsClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _kmsKeyId = configuration["AWS:KMS:KeyId"] 
            ?? throw new InvalidOperationException("AWS KMS Key ID not configured");
    }

    public async Task<string> EncryptAsync(string plaintext, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            throw new ArgumentException("Plaintext cannot be null or empty", nameof(plaintext));
        }

        try
        {
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            
            var request = new EncryptRequest
            {
                KeyId = _kmsKeyId,
                Plaintext = new MemoryStream(plaintextBytes)
            };

            var response = await _kmsClient.EncryptAsync(request, cancellationToken);
            
            // Convert encrypted data to Base64 for storage
            var encryptedBytes = response.CiphertextBlob.ToArray();
            var base64Encrypted = Convert.ToBase64String(encryptedBytes);

            _logger.LogDebug("Successfully encrypted data using KMS key {KeyId}", _kmsKeyId);
            
            return base64Encrypted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt data using KMS key {KeyId}", _kmsKeyId);
            throw new InvalidOperationException("Encryption failed", ex);
        }
    }

    public async Task<string> DecryptAsync(string ciphertext, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(ciphertext))
        {
            throw new ArgumentException("Ciphertext cannot be null or empty", nameof(ciphertext));
        }

        try
        {
            // Convert Base64 back to bytes
            var encryptedBytes = Convert.FromBase64String(ciphertext);
            
            var request = new DecryptRequest
            {
                CiphertextBlob = new MemoryStream(encryptedBytes)
            };

            var response = await _kmsClient.DecryptAsync(request, cancellationToken);
            
            // Convert decrypted bytes back to string
            var decryptedBytes = response.Plaintext.ToArray();
            var plaintext = Encoding.UTF8.GetString(decryptedBytes);

            _logger.LogDebug("Successfully decrypted data using KMS");
            
            return plaintext;
        }
        catch (FormatException ex)
        {
            _logger.LogError(ex, "Invalid Base64 format for ciphertext");
            throw new ArgumentException("Invalid ciphertext format", nameof(ciphertext), ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt data using KMS");
            throw new InvalidOperationException("Decryption failed", ex);
        }
    }

    public async Task<Dictionary<string, string>> EncryptBatchAsync(
        Dictionary<string, string> plaintexts, 
        CancellationToken cancellationToken = default)
    {
        if (plaintexts == null || plaintexts.Count == 0)
        {
            throw new ArgumentException("Plaintexts dictionary cannot be null or empty", nameof(plaintexts));
        }

        var result = new Dictionary<string, string>();

        foreach (var kvp in plaintexts)
        {
            if (!string.IsNullOrEmpty(kvp.Value))
            {
                result[kvp.Key] = await EncryptAsync(kvp.Value, cancellationToken);
            }
            else
            {
                result[kvp.Key] = kvp.Value; // Preserve null/empty values
            }
        }

        _logger.LogDebug("Successfully encrypted {Count} fields in batch", plaintexts.Count);
        
        return result;
    }

    public async Task<Dictionary<string, string>> DecryptBatchAsync(
        Dictionary<string, string> ciphertexts, 
        CancellationToken cancellationToken = default)
    {
        if (ciphertexts == null || ciphertexts.Count == 0)
        {
            throw new ArgumentException("Ciphertexts dictionary cannot be null or empty", nameof(ciphertexts));
        }

        var result = new Dictionary<string, string>();

        foreach (var kvp in ciphertexts)
        {
            if (!string.IsNullOrEmpty(kvp.Value))
            {
                result[kvp.Key] = await DecryptAsync(kvp.Value, cancellationToken);
            }
            else
            {
                result[kvp.Key] = kvp.Value; // Preserve null/empty values
            }
        }

        _logger.LogDebug("Successfully decrypted {Count} fields in batch", ciphertexts.Count);
        
        return result;
    }
}
