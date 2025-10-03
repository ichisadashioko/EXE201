using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Security.Claims;
using System.Text;
using Google.Cloud.Storage.V1;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Shioko.Models;

namespace Shioko.Services
{
    public static class RateLimitedFeatures
    {
        public const string IMAGE_GEN = "image_gen";
        public const string IMAGE_UPLOAD = "image_upload";
    }

    public class RateLimitingService
    {
        private readonly AppDbContext ctx;
        public RateLimitingService(AppDbContext context)
        {
            ctx = context;
        }

        // Define temporary limits here. TODO this could come from appsettings.json or database data
        private static readonly Dictionary<string, (int FreeLimit, int PremiumLimit, TimeSpan Period)> FeatureLimits = new(){
            { RateLimitedFeatures.IMAGE_GEN, (FreeLimit: 1, PremiumLimit: 20, Period: TimeSpan.FromMinutes(1)) },
            { RateLimitedFeatures.IMAGE_UPLOAD, (FreeLimit: 10, PremiumLimit: 50, Period: TimeSpan.FromMinutes(1)) },
        };

        public async Task<(bool IsAllowed, string Message)> IsActionAllowedAsync(int user_id, string feature_name)
        {
            var user = await ctx.Users.FindAsync(user_id);
            if (user == null)
            {
                return (false, "User not found");
            }

            if (!FeatureLimits.TryGetValue(feature_name, out var limit_config))
            {
                // TODO block unknown feature_name?
                return (true, $"feature is not rate limited ({feature_name})"); // TODO debug log
            }

            var (free_limit, premium_limit, period) = limit_config;
            var limit = user.IsPremium ? premium_limit : free_limit;

            var since_time = DateTime.UtcNow - period;
            var action_count = await ctx.ApiUsageLogs
                .Where(log => (
                    (log.UserId == user_id)
                    && (log.FeatureName == feature_name)
                    && (log.CreatedAt >= since_time)
                )).CountAsync();

            if (action_count >= limit)
            {
                // TODO make serialized data for more information
                return (false, $"Rate limit exceeded for feature {feature_name}. Limit: {limit} per {period.TotalMinutes} minutes.");
            }

            return (true, "Action is allowed.");
        }

        public async Task LogUsageAsync(int user_id, string feature_name)
        {
            var log_entry = new ApiUsageLog
            {
                UserId = user_id,
                FeatureName = feature_name,
                CreatedAt = DateTime.UtcNow,
            };

            ctx.ApiUsageLogs.Add(log_entry);
            await ctx.SaveChangesAsync();
        }
    }

    public class ImageGenResult
    {
        public required byte[] image_data { get; set; }
        public required string mime_type { get; set; }
    }
    public interface IImageGenService
    {
        Task<ImageGenResult?> GenImage(
            byte[] image_data_a,
            string mime_type_a,
            byte[] image_data_b,
            string mime_type_b
        );
    }

    public class GoogleGeminiApiImageGenService : IImageGenService
    {
        private readonly string api_key;
        private readonly IHttpClientFactory http_client_factory;

        public GoogleGeminiApiImageGenService(
            IHttpClientFactory http_client_factory,
            GoogleGeminiApiKeyConfig api_key_config
            )
        {
            this.http_client_factory = http_client_factory;
            this.api_key = api_key_config.API_KEY;
        }
        public async Task<ImageGenResult?> GenImage(byte[] image_data_a, string mime_type_a, byte[] image_data_b, string mime_type_b)
        {
            var result = await Utils.GoogleGeminiApiGenerateOffspring(
                image_data_a,
                mime_type_a,
                image_data_b,
                mime_type_b,
                api_key,
                client: http_client_factory.CreateClient(name: Utils.GOOGLE_GEMINI_HTTP_CLIENT_NAME)
            );

            if (result == null)
            {
                return null;
            }

            return new ImageGenResult
            {
                // TODO check for decoding exception
                image_data = Convert.FromBase64String(result.base64_data),
                mime_type = result.mime_type,
            };
        }
    }

    public class DummyImageGenService : IImageGenService
    {
        private readonly string BASE_PATH;
        public DummyImageGenService(string base_path)
        {
            BASE_PATH = base_path;
        }

        public async Task<ImageGenResult?> GenImage(byte[] image_data_a, string mime_type_a, byte[] image_data_b, string mime_type_b)
        {
            var filepath = Path.Combine(BASE_PATH, $"icon-512.png");
            if (!File.Exists(filepath))
            {
                Log.Error($"DummyImageGenService: {filepath} does not exist");
                return null;
            }

            return new ImageGenResult
            {
                image_data = await File.ReadAllBytesAsync(filepath),
                mime_type = IMAGE_MINE_TYPE.PNG,
            };
        }
    }
    public interface IImageUploadService
    {
        Task<string?> UploadImageAsync(Stream stream, string media_type);
    }

    public class LocalImageUploadService : IImageUploadService
    {
        private readonly string BASE_PATH;
        public LocalImageUploadService(string base_path)
        {
            BASE_PATH = base_path;
        }

        public async Task<string?> UploadImageAsync(Stream stream, string media_type)
        {
            var directory = Path.Combine(BASE_PATH, "uploads");
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var filepath = Path.Combine(directory, $"{Guid.NewGuid().ToString()}");
            using (var fileStream = new FileStream(filepath, FileMode.Create, FileAccess.Write))
            {
                await stream.CopyToAsync(fileStream);
            }

            return $"/uploads/{Path.GetFileName(filepath)}";
        }
    }
    public class GoogleCloudImageUploadService : IImageUploadService
    {
        private readonly StorageClient google_cloud_storage_client;
        private readonly GoogleCloudStorageConfig gcs_config;

        public GoogleCloudImageUploadService(
            StorageClient google_cloud_storage_client,
            GoogleCloudStorageConfig gcs_config
        )
        {
            this.google_cloud_storage_client = google_cloud_storage_client;
            this.gcs_config = gcs_config;
        }
        public async Task<string?> UploadImageAsync(Stream stream, string media_type)
        {
            string bucketName = gcs_config.BUCKET_NAME;
            string objectName = $"users/uploads/{Guid.NewGuid()}";

            try
            {
                var gcs_object = google_cloud_storage_client.UploadObject(bucketName, objectName, media_type, stream);
                if (gcs_object == null)
                {
                    return null;
                }
                var public_image_url = gcs_object.MediaLink;
                return public_image_url;

                // TODO use this and check for gcloud emulator compability
                // return $"https://storage.googleapis.com/{_bucketName}/{fileName}";
            }
            catch (Exception gcs_ex)
            {
                Log.Information(gcs_ex.ToString());
                Log.Information(gcs_ex.Message);
            }

            return null;
        }
    }
    public class JwtSettings
    {
        public string Key { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public int ExpiryInMinutes { get; set; }
    }

    public static class CustomClaimTypes
    {
        public const string UserId = "user_id";
    }

    public class TokenService
    {
        public readonly JwtSettings _jwtSettings;

        public TokenService(IOptions<JwtSettings> jwtSettings)
        {
            _jwtSettings = jwtSettings.Value;
        }

        public string GenerateToken(User user)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user), "User cannot be null");
            }

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                //new Claim(CustomClaimTypes.UserId, user.Id),
                new Claim(CustomClaimTypes.UserId, user.Id.ToString()),
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);


            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                // TODO use configured timeout
                expires: DateTime.UtcNow.AddHours(24 * 90), // 90 days
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

}
