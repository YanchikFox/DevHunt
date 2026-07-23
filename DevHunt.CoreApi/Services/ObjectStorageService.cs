using Amazon.S3;
using Amazon.S3.Model;
using Amazon;

namespace DevHunt.CoreApi.Services;

/// <summary>
/// Represents a storage object location (bucket + key).
/// </summary>
public readonly record struct StorageObjectKey(string Bucket, string Key)
{
    /// <summary>Returns <c>{Bucket}/{Key}</c>.</summary>
    public override string ToString() => $"{Bucket}/{Key}";
}

/// <summary>
/// Object storage service supporting S3-compatible backends (SeaweedFS, MinIO, AWS S3).
/// Corresponds to devhunt_deployment.puml: Object Storage layer.
/// </summary>
/// <remarks>
/// <para><strong>Supported Storage Backends</strong>:</para>
/// - <b>SeaweedFS</b>: Open-source (Apache 2.0), lightweight, cost-effective for self-hosted deployments
/// - <b>AWS S3</b>: Managed cloud storage (production-ready, global CDN)
/// - <b>MinIO</b>: Alternative to SeaweedFS (AGPL v3 license, more features but stricter licensing)
/// - <b>Azure Blob Storage</b>: Via S3 compatibility layer
///
/// <para><strong>Use Cases</strong>:</para>
/// - User uploads (profile avatars, attachments)
/// - Project files (documentation, assets, media)
/// - Backup storage
/// - Frontend can use signed URLs for direct browser uploads (bypassing API, reduces load)
///
/// <para><strong>Security Features</strong>:</para>
/// - Signed URLs for temporary access (pre-signed GET/PUT)
/// - Credential validation in production (rejects weak/default credentials)
/// - Server-side encryption support (disabled for SeaweedFS compatibility)
///
/// <para><strong>Architecture</strong>:</para>
/// IObjectStorageService interface allows swapping backends without code changes.
/// All S3-compatible storages use AWS SDK for .NET with ForcePathStyle=true.
/// </remarks>
public interface IObjectStorageService
{
    // Type-safe overloads using StorageObjectKey
    /// <summary>Uploads an object stream to the given bucket/key.</summary>
    /// <param name="location">Target bucket and key.</param>
    /// <param name="content">Object payload stream.</param>
    /// <param name="contentType">MIME type stored with the object.</param>
    /// <returns>Public URL or a placeholder URI when storage is disabled.</returns>
    Task<string> UploadAsync(StorageObjectKey location, Stream content, string contentType);
    /// <summary>Downloads an object stream from the given bucket/key.</summary>
    /// <param name="location">Source bucket and key.</param>
    /// <returns>Response stream, or <see langword="null"/> when missing or storage is disabled.</returns>
    Task<Stream?> DownloadAsync(StorageObjectKey location);
    /// <summary>Creates a time-limited GET URL for direct browser access.</summary>
    /// <param name="location">Object bucket and key.</param>
    /// <param name="expiration">Lifetime of the signed URL.</param>
    /// <returns>Pre-signed URL or placeholder URI when storage is disabled.</returns>
    Task<string> GenerateSignedUrlAsync(StorageObjectKey location, TimeSpan expiration);
    /// <summary>Deletes an object from storage.</summary>
    /// <param name="location">Object bucket and key.</param>
    /// <returns><see langword="true"/> when deletion succeeds.</returns>
    Task<bool> DeleteAsync(StorageObjectKey location);
    /// <summary>Checks whether an object exists.</summary>
    /// <param name="location">Object bucket and key.</param>
    /// <returns><see langword="true"/> when metadata can be retrieved.</returns>
    Task<bool> ExistsAsync(StorageObjectKey location);

    // Convenience overloads with separate bucket/key parameters
    /// <summary>Uploads an object using separate bucket and key parameters.</summary>
    /// <param name="bucket">Target bucket name.</param>
    /// <param name="key">Object key within the bucket.</param>
    /// <param name="content">Object payload stream.</param>
    /// <param name="contentType">MIME type stored with the object.</param>
    /// <returns>Public URL or placeholder URI when storage is disabled.</returns>
    Task<string> UploadAsync(string bucket, string key, Stream content, string contentType);
    /// <summary>Downloads an object using separate bucket and key parameters.</summary>
    /// <param name="bucket">Source bucket name.</param>
    /// <param name="key">Object key within the bucket.</param>
    /// <returns>Response stream, or <see langword="null"/> when missing or storage is disabled.</returns>
    Task<Stream?> DownloadAsync(string bucket, string key);
    /// <summary>Creates a signed URL using separate bucket and key parameters.</summary>
    /// <param name="bucket">Bucket containing the object.</param>
    /// <param name="key">Object key within the bucket.</param>
    /// <param name="expiration">Lifetime of the signed URL.</param>
    /// <returns>Pre-signed URL or placeholder URI when storage is disabled.</returns>
    Task<string> GenerateSignedUrlAsync(string bucket, string key, TimeSpan expiration);
    /// <summary>Deletes an object using separate bucket and key parameters.</summary>
    /// <param name="bucket">Bucket containing the object.</param>
    /// <param name="key">Object key within the bucket.</param>
    /// <returns><see langword="true"/> when deletion succeeds.</returns>
    Task<bool> DeleteAsync(string bucket, string key);
    /// <summary>Checks object existence using separate bucket and key parameters.</summary>
    /// <param name="bucket">Bucket to inspect.</param>
    /// <param name="key">Object key within the bucket.</param>
    /// <returns><see langword="true"/> when metadata can be retrieved.</returns>
    Task<bool> ExistsAsync(string bucket, string key);

    /// <summary>Creates the bucket when it does not already exist.</summary>
    /// <param name="bucket">Bucket name to ensure.</param>
    /// <returns><see langword="true"/> when the bucket exists or was created.</returns>
    Task<bool> EnsureBucketExistsAsync(string bucket);
    /// <summary>Ensures all standard application buckets exist at startup.</summary>
    Task InitializeBucketsAsync();
}

/// <summary>
/// S3-compatible implementation of <see cref="IObjectStorageService"/>.
/// </summary>
public class S3ObjectStorageService : IObjectStorageService
{
    /// <summary>
    /// Holds the object storage connection settings used to decide whether an S3 client can be created.
    /// </summary>
    private record S3ConnectionConfig(
        bool IsEnabled, string? Endpoint, string? AccessKey, string? SecretKey, string? Region)
    {
        /// <summary>Whether endpoint, access key, and secret key are all configured.</summary>
        public bool HasValidCredentials =>
            IsEnabled &&
            !string.IsNullOrEmpty(Endpoint) &&
            !string.IsNullOrEmpty(AccessKey) &&
            !string.IsNullOrEmpty(SecretKey);
    }

    private readonly ILogger<S3ObjectStorageService> _logger;
    private readonly string? _endpoint;
    private readonly string? _publicEndpoint; // Public URL for browser access (may differ from internal endpoint)
    private readonly string? _accessKey;
    private readonly string? _secretKey;
    private readonly string? _region;
    private readonly bool _isEnabled;
    private readonly IAmazonS3? _s3Client;

    /// <summary>
    /// Initializes a new instance of the <see cref="S3ObjectStorageService"/> class.
    /// </summary>
    /// <param name="configuration">Object storage endpoint and credential settings.</param>
    /// <param name="logger">Logger for upload/download failures.</param>
    public S3ObjectStorageService(IConfiguration configuration, ILogger<S3ObjectStorageService> logger)
    {
        _logger = logger;
        _endpoint = configuration["ObjectStorage:Endpoint"];
        _publicEndpoint = configuration["ObjectStorage:PublicEndpoint"] ?? _endpoint;
        _accessKey = configuration["ObjectStorage:AccessKey"];
        _secretKey = configuration["ObjectStorage:SecretKey"];
        _region = configuration["ObjectStorage:Region"] ?? "us-east-1";
        _isEnabled = !string.IsNullOrEmpty(_endpoint) && !string.IsNullOrEmpty(_accessKey);

        ValidateProductionCredentials(configuration, _isEnabled, _accessKey, _secretKey);
        var connectionConfig = new S3ConnectionConfig(_isEnabled, _endpoint, _accessKey, _secretKey, _region);
        _s3Client = CreateS3Client(connectionConfig, _logger);
    }

    /// <summary>Rejects weak or missing credentials when running in Production.</summary>
    private static void ValidateProductionCredentials(
        IConfiguration configuration, bool isEnabled, string? accessKey, string? secretKey)
    {
        var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";
        if (!environment.Equals("Production", StringComparison.OrdinalIgnoreCase) || !isEnabled)
            return;

        if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
        {
            throw new InvalidOperationException(
                "ObjectStorage is enabled in production but AccessKey or SecretKey is missing. " +
                "Both must be configured for production deployment.");
        }

        if (IsWeakCredential(accessKey) || IsWeakCredential(secretKey))
        {
            throw new InvalidOperationException(
                "ObjectStorage credentials MUST be set to strong values (32+ chars) in production. " +
                "Default 'any/any' credentials are NOT allowed. " +
                "Please configure ObjectStorage:AccessKey and ObjectStorage:SecretKey properly.");
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> for default, short, or placeholder-like object storage credentials.
    /// </summary>
    private static bool IsWeakCredential(string credential)
    {
        return credential == "any" ||
               credential.Length < 32 ||
               credential.Contains("default", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Builds an S3 client when valid credentials are configured.</summary>
    private static AmazonS3Client? CreateS3Client(S3ConnectionConfig connectionConfig, ILogger logger)
    {
        if (!connectionConfig.HasValidCredentials)
            return null;

        try
        {
            var s3Config = BuildS3Config(connectionConfig.Endpoint, connectionConfig.Region);
            var client = new AmazonS3Client(connectionConfig.AccessKey, connectionConfig.SecretKey, s3Config);
            logger.LogInformation("S3 client initialized for endpoint: {Endpoint}", connectionConfig.Endpoint);
            return client;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize S3 client");
            return null;
        }
    }

    /// <summary>Configures path-style access and HTTP/HTTPS for S3-compatible endpoints.</summary>
    private static AmazonS3Config BuildS3Config(string? endpoint, string? region)
    {
        var config = new AmazonS3Config { ForcePathStyle = true };

        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            config.ServiceURL = endpoint;
            config.AuthenticationRegion = region;
            config.UseHttp = IsHttpEndpoint(endpoint);
        }
        else
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(region);
        }

        return config;
    }

    /// <summary>Returns whether the configured endpoint uses plain HTTP.</summary>
    private static bool IsHttpEndpoint(string endpoint)
    {
        return Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri)
            ? string.Equals(endpointUri.Scheme, "http", StringComparison.OrdinalIgnoreCase)
            : endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Whether storage is enabled and the S3 client initialized successfully.</summary>
    private bool IsClientAvailable() => _isEnabled && _s3Client != null;

    /// <summary>Detects benign bucket-already-exists responses from S3-compatible providers.</summary>
    private static bool IsBucketAlreadyExistsError(AmazonS3Exception ex) =>
        ex.StatusCode == System.Net.HttpStatusCode.Conflict ||
        ex.ErrorCode == "BucketAlreadyOwnedByYou";

    // String-based overloads delegate to StorageObjectKey implementations
    /// <inheritdoc />
    public Task<string> UploadAsync(string bucket, string key, Stream content, string contentType) =>
        UploadAsync(new StorageObjectKey(bucket, key), content, contentType);

    /// <inheritdoc />
    public Task<Stream?> DownloadAsync(string bucket, string key) =>
        DownloadAsync(new StorageObjectKey(bucket, key));

    /// <inheritdoc />
    public Task<string> GenerateSignedUrlAsync(string bucket, string key, TimeSpan expiration) =>
        GenerateSignedUrlAsync(new StorageObjectKey(bucket, key), expiration);

    // Primary implementations using StorageObjectKey
    /// <inheritdoc />
    public async Task<string> UploadAsync(StorageObjectKey location, Stream content, string contentType)
    {
        if (!IsClientAvailable())
        {
            _logger.LogWarning("ObjectStorage disabled, upload skipped for {Location}", location);
            return $"placeholder://{location}";
        }

        try
        {
            await EnsureBucketExistsAsync(location.Bucket);

            var request = new PutObjectRequest
            {
                BucketName = location.Bucket,
                Key = location.Key,
                InputStream = content,
                ContentType = contentType,
                ServerSideEncryptionMethod = ServerSideEncryptionMethod.None
            };

            var response = await _s3Client!.PutObjectAsync(request);
            var fileUrl = $"{_publicEndpoint}/{location}";
            _logger.LogInformation("File uploaded: {Location}, ETag: {ETag}", location, response.ETag);
            return fileUrl;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error uploading {Location}: {ErrorCode}", location, ex.ErrorCode);
            var errorMessage = $"S3 upload failed: {ex.ErrorCode} - {ex.Message}";
            if (ex.StatusCode == System.Net.HttpStatusCode.Forbidden || ex.ErrorCode == "InvalidAccessKeyId")
                errorMessage += " Check ObjectStorage credentials.";
            throw new InvalidOperationException(errorMessage, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload {Location}", location);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Stream?> DownloadAsync(StorageObjectKey location)
    {
        if (!IsClientAvailable())
            return null;

        try
        {
            var request = new GetObjectRequest { BucketName = location.Bucket, Key = location.Key };
            var response = await _s3Client!.GetObjectAsync(request);
            _logger.LogInformation("File downloaded: {Location}, Size: {Size} bytes", location, response.ContentLength);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("File not found: {Location}", location);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download {Location}", location);
            return null;
        }
    }

    /// <inheritdoc />
    public Task<string> GenerateSignedUrlAsync(StorageObjectKey location, TimeSpan expiration)
    {
        if (!IsClientAvailable())
            return Task.FromResult($"placeholder://{location}");

        try
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = location.Bucket,
                Key = location.Key,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.Add(expiration)
            };

            var signedUrl = _s3Client!.GetPreSignedURL(request);
            _logger.LogInformation("Signed URL generated for {Location}, expires in {Expiration}", location, expiration);
            return Task.FromResult(signedUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate signed URL for {Location}", location);
            throw;
        }
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string bucket, string key) =>
        DeleteAsync(new StorageObjectKey(bucket, key));

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string bucket, string key) =>
        ExistsAsync(new StorageObjectKey(bucket, key));

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(StorageObjectKey location)
    {
        if (!IsClientAvailable())
            return false;

        try
        {
            await _s3Client!.DeleteObjectAsync(new DeleteObjectRequest { BucketName = location.Bucket, Key = location.Key });
            _logger.LogInformation("File deleted: {Location}", location);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete {Location}", location);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(StorageObjectKey location)
    {
        if (!IsClientAvailable())
            return false;

        try
        {
            await _s3Client!.GetObjectMetadataAsync(new GetObjectMetadataRequest { BucketName = location.Bucket, Key = location.Key });
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check existence of {Location}", location);
            return false;
        }
    }

    /// <summary>
    /// <summary>Creates the bucket when missing; tolerates concurrent creation races.</summary>
    /// </summary>
    public async Task<bool> EnsureBucketExistsAsync(string bucket)
    {
        if (!IsClientAvailable())
        {
            _logger.LogWarning("ObjectStorage disabled, cannot ensure bucket {Bucket} exists", bucket);
            return false;
        }

        try
        {
            if (await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, bucket))
            {
                _logger.LogDebug("Bucket {Bucket} already exists", bucket);
                return true;
            }

            return await CreateBucketAsync(bucket);
        }
        catch (AmazonS3Exception ex) when (IsBucketAlreadyExistsError(ex))
        {
            _logger.LogInformation("Bucket {Bucket} already exists (created concurrently)", bucket);
            return true;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure bucket {Bucket} exists. ErrorCode: {ErrorCode}", bucket, ex.ErrorCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error ensuring bucket {Bucket} exists", bucket);
            return false;
        }
    }

    /// <summary>Issues a PutBucket request for the configured region.</summary>
    private async Task<bool> CreateBucketAsync(string bucket)
    {
        _logger.LogInformation("Bucket {Bucket} does not exist, creating...", bucket);
        var request = new PutBucketRequest
        {
            BucketName = bucket,
            BucketRegion = S3Region.FindValue(_region ?? "us-east-1")
        };

        await _s3Client!.PutBucketAsync(request);
        _logger.LogInformation("Bucket {Bucket} created successfully", bucket);
        return true;
    }

    /// <summary>
    /// <summary>Ensures predefined buckets from <see cref="ObjectStorageBuckets"/> exist.</summary>
    /// </summary>
    public async Task InitializeBucketsAsync()
    {
        if (!IsClientAvailable())
        {
            _logger.LogWarning("ObjectStorage disabled, skipping bucket initialization");
            return;
        }

        var buckets = new[]
        {
            ObjectStorageBuckets.Avatars,
            ObjectStorageBuckets.ProjectFiles,
            ObjectStorageBuckets.Uploads,
            ObjectStorageBuckets.Backups
        };

        _logger.LogInformation("Initializing {Count} buckets...", buckets.Length);

        foreach (var bucket in buckets)
        {
            try
            {
                await EnsureBucketExistsAsync(bucket);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize bucket {Bucket}", bucket);
                // Continue with other buckets even if one fails
            }
        }

        _logger.LogInformation("Bucket initialization completed");
    }
}

/// <summary>
/// Helper methods providing standard storage bucket names.
/// </summary>
public static class ObjectStorageBuckets
{
    /// <summary>Bucket for user avatar images.</summary>
    public const string Avatars = "avatars";
    /// <summary>Bucket for project-attached files.</summary>
    public const string ProjectFiles = "project-files";
    /// <summary>Bucket for general user uploads.</summary>
    public const string Uploads = "uploads";
    /// <summary>Bucket for backup archives.</summary>
    public const string Backups = "backups";
}
