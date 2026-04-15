using Amazon.Polly;
using Amazon.Polly.Model;
using Amazon.S3;
using Amazon.S3.Model;
using KisanMitraAI.Core.VoiceIntelligence;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace KisanMitraAI.Infrastructure.AI;

/// <summary>
/// Implementation of voice synthesizer using Amazon Polly
/// </summary>
public class VoiceSynthesizer : IVoiceSynthesizer
{
    private readonly IAmazonPolly _pollyClient;
    private readonly IAmazonS3 _s3Client;
    private readonly IMemoryCache _cache;
    private readonly ILogger<VoiceSynthesizer> _logger;
    private readonly string _bucketName;
    private readonly TimeSpan _synthesisTimeout = TimeSpan.FromSeconds(60); // Increased for long text synthesis (up to 3000 chars)
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(24);

    private readonly Dictionary<string, string> _languageToVoiceMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "hi-IN", "Kajal" },      // Hindi (Neural)
        { "ta-IN", "Kajal" },      // Tamil (using Hindi voice as placeholder)
        { "te-IN", "Kajal" },      // Telugu (using Hindi voice as placeholder)
        { "bn-IN", "Kajal" },      // Bengali (using Hindi voice as placeholder)
        { "mr-IN", "Kajal" }       // Marathi (using Hindi voice as placeholder)
    };

    public VoiceSynthesizer(
        IAmazonPolly pollyClient,
        IAmazonS3 s3Client,
        IMemoryCache cache,
        ILogger<VoiceSynthesizer> logger,
        string bucketName)
    {
        _pollyClient = pollyClient ?? throw new ArgumentNullException(nameof(pollyClient));
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _bucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
    }

    public async Task<(Stream AudioStream, string S3Url)> SynthesizeAsync(
        string text,
        string voiceId,
        string languageCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text cannot be null or empty", nameof(text));
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("Language code cannot be null or empty", nameof(languageCode));

        // Use provided voiceId or map from language code
        var actualVoiceId = string.IsNullOrWhiteSpace(voiceId)
            ? GetVoiceIdForLanguage(languageCode)
            : voiceId;

        _logger.LogInformation("Synthesizing speech for text length {Length} with voice {VoiceId}",
            text.Length, actualVoiceId);

        // Check cache for previously synthesized audio
        var cacheKey = GenerateCacheKey(text, actualVoiceId);
        if (_cache.TryGetValue<string>(cacheKey, out var cachedS3Key))
        {
            _logger.LogInformation("Retrieved synthesized audio from cache: {S3Key}", cachedS3Key);
            var cachedStream = await DownloadFromS3(cachedS3Key!, cancellationToken);
            var cachedS3Url = GeneratePresignedUrl(cachedS3Key!, TimeSpan.FromHours(24));
            return (cachedStream, cachedS3Url);
        }

        try
        {
            using var timeoutCts = new CancellationTokenSource(_synthesisTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var synthesizeRequest = new SynthesizeSpeechRequest
            {
                Text = text,
                VoiceId = actualVoiceId,
                OutputFormat = OutputFormat.Mp3,
                Engine = Engine.Neural,
                LanguageCode = languageCode
            };

            SynthesizeSpeechResponse response;
            try
            {
                response = await _pollyClient.SynthesizeSpeechAsync(synthesizeRequest, linkedCts.Token);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Polly synthesis timed out after {Timeout} seconds for text: {Text}", 
                    _synthesisTimeout.TotalSeconds, text.Substring(0, Math.Min(50, text.Length)));
                
                // Return a fallback - just the text response without audio
                throw new InvalidOperationException(
                    $"Voice synthesis timed out. The text response is available but audio could not be generated.", 
                    ex);
            }

            // Read the audio stream into a MemoryStream so we can get the length
            var audioMemoryStream = new MemoryStream();
            await response.AudioStream.CopyToAsync(audioMemoryStream, linkedCts.Token);
            audioMemoryStream.Position = 0;

            // Upload to S3 for caching
            var s3Key = $"audio/synthesized/{Guid.NewGuid()}.mp3";
            await _s3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = s3Key,
                InputStream = audioMemoryStream,
                ContentType = "audio/mpeg"
            }, linkedCts.Token);

            // Cache the S3 key
            _cache.Set(cacheKey, s3Key, _cacheExpiration);

            // Generate presigned URL (valid for 24 hours)
            var s3Url = GeneratePresignedUrl(s3Key, TimeSpan.FromHours(24));

            _logger.LogInformation("Synthesized audio uploaded to S3: {S3Key}, Presigned URL generated", s3Key);

            // Return the audio stream and presigned S3 URL
            var audioStream = await DownloadFromS3(s3Key, cancellationToken);
            return (audioStream, s3Url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error synthesizing speech for text: {Text}", text);
            throw;
        }
    }

    private string GetVoiceIdForLanguage(string languageCode)
    {
        if (_languageToVoiceMap.TryGetValue(languageCode, out var voiceId))
            return voiceId;

        _logger.LogWarning("Unknown language code {LanguageCode}, defaulting to Kajal (Hindi)", languageCode);
        return "Kajal";
    }

    private string GenerateCacheKey(string text, string voiceId)
    {
        var input = $"{text}:{voiceId}";
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return $"voice:{Convert.ToHexString(hashBytes)}";
    }

    private async Task<Stream> DownloadFromS3(string s3Key, CancellationToken cancellationToken)
    {
        var response = await _s3Client.GetObjectAsync(_bucketName, s3Key, cancellationToken);
        var memoryStream = new MemoryStream();
        await response.ResponseStream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;
        return memoryStream;
    }

    private string GeneratePresignedUrl(string s3Key, TimeSpan expiration)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = s3Key,
            Expires = DateTime.UtcNow.Add(expiration)
        };

        return _s3Client.GetPreSignedURL(request);
    }
}
