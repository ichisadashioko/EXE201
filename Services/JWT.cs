using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Shioko.Models;

namespace Shioko.Services
{
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
