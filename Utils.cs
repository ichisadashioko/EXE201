using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;

namespace Shioko
{
    public static class IMAGE_MINE_TYPE
    {
        public const string PNG = "image/png";
        public const string JPEG = "image/jpeg";
        //public const string JPG = "image/jpg";
        public const string GIF = "image/gif";
        public const string BMP = "image/bmp";
        public const string WEBP = "image/webp";
        public const string UNKNOWN = "unknown";

        public static bool IsValidImageMimeType(string mimeType)
        {
            return (mimeType == PNG
                || mimeType == JPEG
                //|| mimeType == JPG
                || mimeType == GIF
                || mimeType == BMP
                || mimeType == WEBP
            );
        }
        public static bool ValidateImageDataUsingImageSharp(byte[] data, out string? mine_type)
        {
            try
            {
                IImageFormat image_format = Image.DetectFormat(data);
                mine_type = image_format.DefaultMimeType;
                if (!IsValidImageMimeType(mine_type))
                {
                    return false;
                }

                // TODO verify if decoding only metadata can still pose security risk? specially crafted "big" metadata
                ImageInfo image_info = Image.Identify(data);
                if ((image_info.Width == 0) || (image_info.Height == 0))
                {
                    return false;
                }

                if ((image_info.Width > 4096) || (image_info.Height > 4096))
                {
                    return false;
                }

                // TODO should we load the whole image and catch exception
                //using var image = await SixLabors.ImageSharp.Image.LoadAsync(stream);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                Log.Error(ex.ToString());
            }
            mine_type = null;
            return false;
        }

        public static bool ValidateImageDataUsingImageSharp(Stream stream, out string? mine_type)
        {
            try
            {
                stream.Seek(0, SeekOrigin.Begin);
                IImageFormat image_format = Image.DetectFormat(stream);
                mine_type = image_format.DefaultMimeType;
                if (!IsValidImageMimeType(mine_type))
                {
                    return false;
                }

                // TODO verify if decoding only metadata can still pose security risk? specially crafted "big" metadata
                stream.Seek(0, SeekOrigin.Begin);
                ImageInfo image_info = Image.Identify(stream);
                if ((image_info.Width == 0) || (image_info.Height == 0))
                {
                    return false;
                }

                if ((image_info.Width > 4096) || (image_info.Height > 4096))
                {
                    return false;
                }

                // TODO should we load the whole image and catch exception
                //using var image = await SixLabors.ImageSharp.Image.LoadAsync(stream);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                Log.Error(ex.ToString());
            }
            mine_type = null;
            return false;
        }

        public static async Task<string> ValidateImageFileData(Stream stream)
        {
            string? media_type = UNKNOWN;
            // check with request header media type
            var buffer = new byte[8];
            await stream.ReadAsync(buffer, 0, buffer.Length);
            // Check for PNG (89 50 4E 47 0D 0A 1A 0A)
            if (buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47 &&
                buffer[4] == 0x0D && buffer[5] == 0x0A && buffer[6] == 0x1A && buffer[7] == 0x0A)
            {
                // It's a PNG
                media_type = PNG;
            }
            // Check for JPEG (FF D8 FF)
            else if (buffer[0] == 0xFF && buffer[1] == 0xD8 && buffer[2] == 0xFF)
            {
                // It's a JPEG
                media_type = JPEG;
            }

            return media_type;
        }
    }
    public class GoogleGeminiApiKeyConfig
    {
        public required string API_KEY { get; set; }
    }
    public class GoogleCloudStorageConfig
    {
        public required string BUCKET_NAME { get; set; }
    }
    public static class Utils
    {
        public const string DOWNLOAD_REMOTE_IMAGE_HTTP_CLIENT_NAME = "IMAGE_DOWNLOADER";
        public const string GOOGLE_GEMINI_HTTP_CLIENT_NAME = "GOOGLE_GEMINI";
        public const int UPLOAD_IMAGE_SIZE_LIMIT = 5242880; // (5 * 1024 * 1024);
        public static async Task<MemoryStream?> DownloadRemoteUrl(string image_url, HttpClient? http_client = null)
        {
            if (http_client == null)
            {
                http_client = new HttpClient();
            }
            using (var response = await http_client.GetAsync(
                image_url,
                HttpCompletionOption.ResponseHeadersRead
                ))
            {
                response.EnsureSuccessStatusCode();
                var limitedStream = new MemoryStream();
                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    var buffer = new byte[81920];
                    int read;
                    int totalRead = 0;
                    while (true)
                    {
                        read = await stream.ReadAsync(buffer, 0, buffer.Length);
                        if (read <= 0)
                        {
                            break;
                        }

                        totalRead += read;
                        if (totalRead > UPLOAD_IMAGE_SIZE_LIMIT)
                        {
                            throw new InvalidOperationException("response too large");
                        }
                        await limitedStream.WriteAsync(buffer, 0, read);
                    }
                    limitedStream.Seek(0, SeekOrigin.Begin);
                    return limitedStream;
                }
            }
        }

        public class GoogleGeminiApiImageResponse
        {
            public required string base64_data { get; set; }
            public required string mime_type { get; set; }

        }

        public static async Task<GoogleGeminiApiImageResponse?> GoogleGeminiApiGenerateOffspring(
            byte[] image_data_a,
            string image_mime_type_a,
            byte[] image_data_b,
            string image_mime_type_b,
            string gemini_api_key,
            HttpClient? client = null
            )
        {
            try
            {
                string image_base64_a = Convert.ToBase64String(image_data_a);
                string image_base64_b = Convert.ToBase64String(image_data_b);

                // construct payload
                var payload = new
                {
                    contents = new object[]
                    {
                        new
                        {
                            parts = new object[]
                            {
                                new {
                                    text = "Generate a realistic image of a pet that could be the offspring of the two pets shown in the provided images. The new pet should combine physical features and colors from both parents, appearing as a natural mix of the two. Show the offspring in a neutral pose and clear lighting."
                                },
                                new
                                {
                                    inline_data = new
                                    {
                                        mime_type = image_mime_type_a,
                                        data = image_base64_a,
                                    },
                                },
                                new
                                {
                                    inline_data = new
                                    {
                                        mime_type = image_mime_type_a,
                                        data = image_base64_a,
                                    },
                                },
                            },
                        },
                    },
                };

                string request_json = JsonSerializer.Serialize(payload);

                if (client == null)
                {
                    client = new HttpClient();
                }

                client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");

                var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    "v1beta/models/gemini-2.5-flash-image-preview:generateContent"
                );
                request.Headers.Add("x-goog-api-key", gemini_api_key);
                request.Content = new StringContent(request_json, Encoding.UTF8, "application/json");

                var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();

                string response_json = await response.Content.ReadAsStringAsync();
                // TODO store both request and response data for debugging
                // the response JSON contains base64-encoded image(s). extract and save

                using var json_doc = JsonDocument.Parse(response_json);
                var root = json_doc.RootElement;
                JsonElement candidates;
                if (!root.TryGetProperty("candidates", out candidates))
                {
                    Log.Error("Google Gemini API response problem: cannot find 'candidates'");
                    return null;
                }

                if (candidates.ValueKind != JsonValueKind.Array)
                {
                    Log.Error("Google Gemini API response problem: 'candidates' is not an array");
                    return null;
                }

                foreach (var candidate in candidates.EnumerateArray())
                {
                    if (!candidate.TryGetProperty("content", out var content))
                    {
                        continue;
                    }
                    if (!content.TryGetProperty("parts", out var parts))
                    {
                        continue;
                    }
                    if (!(parts.ValueKind == JsonValueKind.Array))
                    {
                        continue;
                    }
                    foreach (var part in parts.EnumerateArray())
                    {
                        if (!part.TryGetProperty("inlineData", out var inline_data))
                        {
                            continue;
                        }
                        string? base64_data = null;
                        string? mime_type = null;

                        if (inline_data.TryGetProperty("data", out var data_json_element))
                        {
                            if (data_json_element.ValueKind == JsonValueKind.String)
                            {
                                base64_data = data_json_element.GetString();
                            }
                        }

                        if (inline_data.TryGetProperty("mimeType", out var mime_type_json_element))
                        {
                            if (mime_type_json_element.ValueKind == JsonValueKind.String)
                            {
                                mime_type = mime_type_json_element.GetString();
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(base64_data) && !string.IsNullOrWhiteSpace(mime_type))
                        {
                            return new GoogleGeminiApiImageResponse
                            {
                                base64_data = base64_data,
                                mime_type = mime_type,
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                Log.Error(ex.ToString());
            }

            return null;
        }

        public static bool IsDevelopment()
        {
            return string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "development", StringComparison.InvariantCultureIgnoreCase);
        }

        public static async Task<MemoryStream?> PullImageData(
            string image_url,
            HttpClient? http_client = null,
            string? webRootPath = null
        )
        {
            try
            {
                if (string.IsNullOrWhiteSpace(image_url))
                {
                    return null;
                }

                if (!IsAllowedImageUrl(image_url))
                {
                    Log.Error($"PullImageData: disallowed url {image_url}");
                    return null;
                }

                if (image_url.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
                {
                    if (!IsDevelopment())
                    {
                        return null;
                    }
                }

                if (IsDevelopment())
                {
                    if (image_url.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrWhiteSpace(webRootPath))
                        {
                            return null;
                        }

                        var uploads_path = Path.Combine(webRootPath, "uploads");
                        if (!Directory.Exists(uploads_path))
                        {
                            return null;
                        }

                        // TODO put "/uploads/" in shared config/static variable
                        var requested_path = image_url.Substring("/uploads/".Length).Replace('/', Path.DirectorySeparatorChar);
                        if (requested_path.Contains(".."))
                        {
                            Log.Error($"PullImageData: directory traversal attempt {image_url}");
                            return null;
                        }

                        var local_path = Path.GetFullPath(Path.Combine(uploads_path, requested_path));

                        if (!local_path.StartsWith(uploads_path, StringComparison.OrdinalIgnoreCase))
                        {
                            Log.Error($"PullImageData: directory traversal attempt {image_url}");
                            return null;
                        }

                        if (!File.Exists(local_path))
                        {
                            Log.Error($"PullImageData: local file not found {local_path}");
                            return null;
                        }

                        var file_info = new FileInfo(local_path);
                        if (file_info.Length > UPLOAD_IMAGE_SIZE_LIMIT)
                        {
                            Log.Error($"PullImageData: local file too large {local_path} ({file_info.Length} bytes)");
                            return null;
                        }

                        var memory_stream = new MemoryStream();
                        using (var file_stream = File.OpenRead(local_path))
                        {
                            await file_stream.CopyToAsync(memory_stream);
                        }
                        memory_stream.Seek(0, SeekOrigin.Begin);
                        return memory_stream;
                    }
                }

                return await DownloadRemoteUrl(image_url, http_client);
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                Log.Error(ex.ToString());
            }

            return null;
        }

        public static bool IsAllowedImageUrl(string input_url)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(input_url))
                {
                    return false;
                }

                bool is_dev = IsDevelopment();

                if (is_dev)
                {
                    if (input_url.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
                    {
                        return true; // TODO validate filepath later to prevent directory traversal attack
                    }
                }

                if (!Uri.TryCreate(input_url, UriKind.Absolute, out Uri? uri_result))
                {
                    Log.Error($"IsAllowedImageUrl: invalid url {input_url}");
                    return false;
                }

                var host = uri_result.Host.ToLowerInvariant();
                var allowed_domains = new List<string>();
                // TODO limit to our bucket domain only
                allowed_domains.Add("storage.googleapis.com");
                if (is_dev)
                {
                    allowed_domains.Add("localhost");
                    allowed_domains.Add("127.0.0.1");
                }

                if (!allowed_domains.Any(domain => host == domain || host.EndsWith($".{domain}")))
                {
                    Log.Error($"IsAllowedImageUrl: disallowed domain {host}");
                    return false;
                }

                if (uri_result.Scheme != Uri.UriSchemeHttps && uri_result.Scheme != Uri.UriSchemeHttp)
                {
                    Log.Error($"IsAllowedImageUrl: disallowed scheme {uri_result.Scheme}");
                    return false;
                }

                // disable http in production
                if (!is_dev && uri_result.Scheme != Uri.UriSchemeHttps)
                {
                    Log.Error($"IsAllowedImageUrl: disallowed scheme {uri_result.Scheme} in production");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                Log.Error(ex.ToString());
                return false;
            }
            return false;
        }

        public static async Task<string> ComputeFileHashAsync(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("file is empty");
            }

            using var sha256 = SHA512.Create();
            var hash_bytes = sha256.ComputeHash(data);

            return BitConverter.ToString(hash_bytes).Replace("-", "").ToUpperInvariant();

        }

        public static async Task<string> ComputeFileHashAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                throw new ArgumentException("file is empty");
            }

            using var sha256 = SHA512.Create();
            using var stream = file.OpenReadStream();
            var hash_bytes = await sha256.ComputeHashAsync(stream);

            return BitConverter.ToString(hash_bytes).Replace("-", "").ToUpperInvariant();
        }
        public static async Task<string> ComputeFileHashAsync(Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            using var sha256 = SHA512.Create();
            var hash_bytes = await sha256.ComputeHashAsync(stream);

            return BitConverter.ToString(hash_bytes).Replace("-", "").ToUpperInvariant();
        }

        //public static const int UPLOAD_IMAGE_SIZE_LIMIT = 5242880; // (5 * 1024 * 1024);
        public static bool ShouldServeIndexHtmlContent(PathString request_path)
        {
            if (request_path == "/")
            {
                return true;
            }

            if (request_path.StartsWithSegments("/chathub"))
            {
                return false;
            }

            if (IsDevelopment())
            {
                if (request_path.StartsWithSegments("/uploads"))
                {
                    return false;
                }
            }
            if (request_path.StartsWithSegments("/api"))
            {
                return false;
            }
            if (request_path.StartsWithSegments("/index.html"))
            {
                return false;
            }

            if (Path.HasExtension(request_path))
            {
                return false;
            }

            if (request_path.StartsWithSegments("/swagger"))
            {
                return false;
            }
            if (request_path.StartsWithSegments("/assets"))
            {
                return false;
            }
            if (request_path.StartsWithSegments("/favicon.ico"))
            {
                return false;
            }

            return true;
        }
    }
}
