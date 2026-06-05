using Amazon.S3;
using Amazon.S3.Model;
using DZI_api.Options;
using Microsoft.Extensions.Options;

namespace DZI_api.Services;

public interface IR2Service
{
    string GeneratePresignedUrl(string key, TimeSpan expires);
}

public class R2Service : IR2Service
{
    private readonly IAmazonS3 _s3Client;
    private readonly CloudflareR2Options _options;

    public R2Service(IOptions<CloudflareR2Options> options)
    {
        _options = options.Value;

        var s3Config = new AmazonS3Config
        {
            ServiceURL = $"https://{_options.AccountId}.r2.cloudflarestorage.com",
            ForcePathStyle = true
        };

        _s3Client = new AmazonS3Client(_options.AccessKeyId, _options.SecretAccessKey, s3Config);
    }

    public string GeneratePresignedUrl(string key, TimeSpan expires)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key = key,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(expires)
        };

        return _s3Client.GetPreSignedURL(request);
    }
}
