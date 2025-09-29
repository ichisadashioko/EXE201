using System.IO;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Shioko.Models;
using Shioko.Services;

namespace Shioko.Controllers
{
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext ctx;
        private readonly TokenService token_service;
        private readonly IImageUploadService upload_service;
        private readonly IImageGenService image_gen_service;
        private readonly IHttpClientFactory http_client_factory;
        private readonly IWebHostEnvironment env;

        public UsersController(
            AppDbContext ctx,
            TokenService token_service,
            IImageUploadService upload_service,
            IHttpClientFactory http_client_factory,
            IImageGenService image_gen_service,
            IWebHostEnvironment env
        )
        {
            this.ctx = ctx;
            this.token_service = token_service;
            this.upload_service = upload_service;
            this.http_client_factory = http_client_factory;
            this.env = env;
            this.image_gen_service = image_gen_service;
        }


        public class ai_generate_offspring_dto
        {
            // TODO use saved image url/verify image url
            // TODO
            // public required string image_source_type {get;set;}
            // TODO check max image size 5MB each
            public required string image_a_url { get; set; }
            public required string image_b_url { get; set; }
            //public required int user_image_id_a { get; set; }
            //public required int user_image_id_b { get; set; }
        }

        public class GenerateOffspring_RESPONSE_DTO
        {
            public required string message { get; set; }
            public string? image_url { get; set; }
        }

        public class OffspringImageFilterInput
        {
            public string? hash { get; set; }
        }

        [HttpPost("/api/ai/users/images")]
        [Authorize]
        public async Task<IActionResult> PullAllImageDataForPetBSelection(
            [FromBody] OffspringImageFilterInput input_obj
            )
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new
                    {
                        message = "bad request", // TODO replace with error status code
                    });
                }

                var user_id_claim = User.FindFirst(CustomClaimTypes.UserId);
                if (user_id_claim == null)
                {
                    return Unauthorized(new
                    {
                        message = "User ID not found in token", // TODO replace with error status code
                    });
                }

                int user_id;

                if (!Int32.TryParse(user_id_claim.Value, out user_id))
                {
                    return Unauthorized(new
                    {
                        message = "Invalid user ID in token"
                    });
                }

                var user = await ctx.Users
                    .Where(obj => obj.Id == user_id) // TODO add active check
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return Unauthorized(new
                    {
                        message = "User not found"
                    });
                }

                var pet_list = await ctx.Pets
                    .Where(obj => (
                    (obj.UserId == user_id)
                    && (obj.Active == true)
                    ))
                    .Include(obj => obj.ProfilePicture)
                    .Include(obj => obj.Pictures)
                    .ToListAsync();

                var user_images = await ctx.UserImages.Where(obj => (obj.UserId == user_id)).ToListAsync();

                var other_image_hash = input_obj.hash;
                if (other_image_hash != null)
                {
                   // TODO filter pet type using google vision api caches
                }

                List<string> pet_image_url_list = new List<string>();
                foreach (var pet in pet_list)
                {
                    foreach (var picture in pet.Pictures)
                    {
                        pet_image_url_list.Add(picture.Url);
                    }
                }

                List<UserImage> other_user_image_list = new List<UserImage>();
                foreach (var user_image in user_images)
                {
                    if (pet_image_url_list.Contains(user_image.StorageUrl))
                    {
                        continue;
                    }

                    other_user_image_list.Add(user_image);
                }

                var retval_obj = new
                {
                    pets = pet_list.Select(obj => new
                    {
                        id = obj.PetId,
                        name = obj.Name,
                        profile_picture_id = obj.ProfilePictureId,
                        images = obj.Pictures.Where(picture => (picture.Active && (!picture.Removed))).Select(picture => new
                        {
                            id = picture.PetPictureId,
                            url = picture.Url,
                            ts = picture.CreatedAt.ToUnixTS(),
                        })
                    }),
                    user_images = other_user_image_list.Select(user_image => new
                    {
                        id = user_image.Id,
                        url = user_image.StorageUrl,
                        ts = user_image.ModifiedAt.ToUnixTS(),
                    })
                };

                return Ok(retval_obj);

                // var user = await ctx.Users
                //     .Include(obj => obj.Pets)
                //     .ThenInclude(pet => pet.ProfilePicture)
                //     .Include(obj => obj.Pets)
                //     .ThenInclude(pet => pet.Pictures)
                //     .Include(obj => obj.UserImages)
                //     .Where(obj => obj.Id == user_id) // TODO add active check
                //     .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Log.Information(ex.ToString());
                Log.Information(ex.Message);
                return StatusCode(500, new
                {
                    message = "internal server error",
                });
            }
        }

        [HttpPost("/api/ai/offspring")]
        [Authorize]
        [ProducesResponseType(typeof(GenerateOffspring_RESPONSE_DTO), 200)]
        public async Task<IActionResult> GenerateOffspring([FromBody] ai_generate_offspring_dto input_obj)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new
                    {
                        message = "bad request", // TODO replace with error status code
                    });
                }

                var user_id_claim = User.FindFirst(CustomClaimTypes.UserId);
                if (user_id_claim == null)
                {
                    return Unauthorized(new
                    {
                        message = "User ID not found in token", // TODO replace with error status code
                    });
                }

                int user_id;

                if (!Int32.TryParse(user_id_claim.Value, out user_id))
                {
                    return Unauthorized(new
                    {
                        message = "Invalid user ID in token"
                    });
                }

                var user = await ctx.Users
                    .Where(obj => obj.Id == user_id) // TODO add active check
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return Unauthorized(new
                    {
                        message = "User not found"
                    });
                }

                // TODO validate if user image url is in database
                string input_image_url_a = input_obj.image_a_url;
                string input_image_url_b = input_obj.image_b_url;

                UserImage? user_image_a = await ctx.UserImages.FirstOrDefaultAsync(obj => (obj.StorageUrl == input_image_url_a));
                if (user_image_a == null)
                {
                    var image_a_obj = await ctx.PetPictures.FirstOrDefaultAsync(obj => (obj.Url == input_image_url_a));
                    if (image_a_obj == null)
                    {
                        return BadRequest(new
                        {
                            message = "invalid input url | input_image_url_a"
                        });
                    }

                    user_image_a = await SyncPetPictureWithUserImages(image_a_obj);
                    if (user_image_a == null)
                    {
                        return StatusCode(500, new
                        {
                            message = "internal server error | failed to SyncPetPictureWithUserImages"
                        });
                    }
                }

                UserImage? user_image_b = await ctx.UserImages.FirstOrDefaultAsync(obj => (obj.StorageUrl == input_image_url_b));
                if (user_image_b == null)
                {
                    var image_b_obj = await ctx.PetPictures.FirstOrDefaultAsync(obj => (obj.Url == input_image_url_b));
                    if (image_b_obj == null)
                    {
                        return BadRequest(new
                        {
                            message = "invalid input url | input_image_url_b"
                        });
                    }

                    user_image_b = await SyncPetPictureWithUserImages(image_b_obj);
                    if (user_image_b == null)
                    {
                        return StatusCode(500, new
                        {
                            message = "internal server error | failed to SyncPetPictureWithUserImages"
                        });
                    }
                }

                //object? image_a_obj = await ctx.PetPictures.FirstOrDefaultAsync(obj => (obj.Url == input_image_url_a));
                //if (image_a_obj == null)
                //{
                //    image_a_obj = await ctx.UserImages.FirstOrDefaultAsync(obj => (obj.StorageUrl == input_image_url_a));
                //}

                //if (image_a_obj == null)
                //{
                //    return BadRequest(new
                //    {
                //        message = "invalid input url | input_image_url_a"
                //    });
                //}

                //object? image_b_obj = await ctx.PetPictures.FirstOrDefaultAsync(obj => (obj.Url == input_image_url_b));
                //if (image_b_obj == null)
                //{
                //    image_b_obj = await ctx.UserImages.FirstOrDefaultAsync(obj => (obj.StorageUrl == input_image_url_b));
                //}

                //if (image_b_obj == null)
                //{
                //    return BadRequest(new
                //    {
                //        message = "invalid input url | input_image_url_b"
                //    });
                //}

                //{
                //    if (image_a_obj is PetPicture pet_picture)
                //    {
                //        user_image_a = await SyncPetPictureWithUserImages(pet_picture);
                //        if (user_image_a == null)
                //        {
                //            return StatusCode(500, new
                //            {
                //                message = "internal server error | failed to SyncPetPictureWithUserImages"
                //            });
                //        }
                //    }
                //}
                //{
                //    if (image_b_obj is PetPicture pet_picture)
                //    {
                //        user_image_b = await SyncPetPictureWithUserImages(pet_picture);
                //        if (user_image_b == null)
                //        {
                //            return StatusCode(500, new
                //            {
                //                message = "internal server error | failed to SyncPetPictureWithUserImages"
                //            });
                //        }
                //    }
                //}

                var image_downloader_http_client = http_client_factory.CreateClient(Utils.DOWNLOAD_REMOTE_IMAGE_HTTP_CLIENT_NAME);
                if (image_downloader_http_client == null)
                {
                    return StatusCode(500, new
                    {
                        // TODO replace with internal error code
                        message = "internal server error | image_downloader_http_client",
                    });
                }

                // TODO google vision cache to extract pet info
                // TODO check image mine type from cache

                // TODO check for hisotry of generated pair
                // TODO check for memory leak with MemoryStream
                var image_data_stream_a = await Utils.PullImageData(
                    user_image_a.StorageUrl,
                    image_downloader_http_client,
                    env.WebRootPath
                    );

                if (image_data_stream_a == null)
                {
                    return StatusCode(500, new
                    {
                        message = "internal server error | failed to load image data",
                    });
                }

                var hash_a = await Utils.ComputeFileHashAsync(image_data_stream_a);
                if (hash_a == null)
                {
                    return StatusCode(500, new
                    {
                        message = "internal server error | failed to compute hash for image data",
                    });
                }

                string? mime_type_a = null, mime_type_b = null;
                var mime_cache_obj = await ctx.ImageDataMimeCaches.FirstOrDefaultAsync(obj => obj.Hash == hash_a);
                if (mime_cache_obj == null)
                {
                    bool is_valid = IMAGE_MINE_TYPE.ValidateImageDataUsingImageSharp(image_data_stream_a, out mime_type_a);
                    if (mime_type_a != null)
                    {
                        await ctx.ImageDataMimeCaches.AddAsync(new ImageDataMimeCache
                        {
                            CreatedAt = DateTime.UtcNow,
                            Hash = hash_a,
                            MimeType = mime_type_a,
                            UserId = user_id,
                        });

                        await ctx.SaveChangesAsync();
                    }

                    if (!is_valid)
                    {
                        return BadRequest(new
                        {
                            message = "invalid image data"
                        });
                    }
                }
                else
                {
                    mime_type_a = mime_cache_obj.MimeType;
                }

                var image_data_stream_b = await Utils.PullImageData(
                    user_image_b.StorageUrl,
                    image_downloader_http_client,
                    env.WebRootPath
                    );

                if (image_data_stream_b == null)
                {
                    return StatusCode(500, new
                    {
                        message = "internal server error | failed to load image data",
                    });
                }

                var hash_b = await Utils.ComputeFileHashAsync(image_data_stream_b);
                if (hash_b == null)
                {
                    return StatusCode(500, new
                    {
                        message = "internal server error | failed to compute hash for image data",
                    });
                }

                mime_cache_obj = await ctx.ImageDataMimeCaches.FirstOrDefaultAsync(obj => obj.Hash == hash_b);
                if (mime_cache_obj == null)
                {
                    bool is_valid = IMAGE_MINE_TYPE.ValidateImageDataUsingImageSharp(image_data_stream_b, out mime_type_b);
                    if (mime_type_b != null)
                    {
                        await ctx.ImageDataMimeCaches.AddAsync(new ImageDataMimeCache
                        {
                            CreatedAt = DateTime.UtcNow,
                            Hash = hash_b,
                            MimeType = mime_type_b,
                            UserId = user_id,
                        });

                        await ctx.SaveChangesAsync();
                    }

                    if (!is_valid)
                    {
                        return BadRequest(new
                        {
                            message = "invalid image data"
                        });
                    }
                }
                else
                {
                    mime_type_b = mime_cache_obj.MimeType;
                }

                if ((mime_type_a == null) || (mime_type_b == null))
                {
                    return BadRequest(new
                    {
                        message = "invalid image data"
                    });
                }
                var result = await image_gen_service.GenImage(
                    image_data_a: image_data_stream_a.ToArray(),
                    image_data_b: image_data_stream_b.ToArray(),
                    mime_type_a: mime_type_a,
                    mime_type_b: mime_type_b
                    );

                if (result == null)
                {
                    return StatusCode(500, new
                    {
                        message = "internal server error | failed to gen image"
                    });
                }

                var result_image_hash = await Utils.ComputeFileHashAsync(result.image_data);
                if (result_image_hash == null)
                {
                    return StatusCode(500, new
                    {
                        message = "internal server error | failed to compute result image hash"
                    });
                }

                bool is_valid_image = false;
                string? result_image_mime_type_in_db = null;
                // TODO should we return the image mime type
                mime_cache_obj = await ctx.ImageDataMimeCaches.FirstOrDefaultAsync(obj => obj.Hash == result_image_hash);
                if (mime_cache_obj == null)
                {
                    is_valid_image = IMAGE_MINE_TYPE.ValidateImageDataUsingImageSharp(result.image_data, out var result_mime_type);
                    if (result_mime_type != null)
                    {
                        result_image_mime_type_in_db = result_mime_type;
                        await ctx.ImageDataMimeCaches.AddAsync(new ImageDataMimeCache
                        {
                            Hash = result_image_hash,
                            MimeType = result_mime_type,
                            CreatedAt = DateTime.UtcNow,
                            UserId = user_id,
                        });

                        await ctx.SaveChangesAsync();
                    }
                }
                else
                {
                    result_image_mime_type_in_db = mime_cache_obj.MimeType;
                    is_valid_image = IMAGE_MINE_TYPE.IsValidImageMimeType(mime_cache_obj.MimeType);
                }

                if (!is_valid_image)
                {
                    return StatusCode(500, new
                    {
                        message = "internal server error | invalid result image"
                    });
                }

                if (result_image_mime_type_in_db == null)
                {
                    return StatusCode(500, new
                    {
                        message = "internal server error | mime type in db"
                    });
                }

                if (!result_image_mime_type_in_db.Equals(result.mime_type))
                {
                    Log.Warning($"MIME type from result is not the same as the one we validate ({result.mime_type} - {result_image_mime_type_in_db})");
                }

                string? result_image_url = await upload_service.UploadImageAsync(
                    new MemoryStream(result.image_data),
                    result_image_mime_type_in_db
                );

                if (result_image_url == null)
                {
                    return StatusCode(500, new
                    {
                        message = "internal server error | failed to upload result image"
                    });
                }

                // TODO content moderation with Google Vision API

                await ctx.UserImages.AddAsync(new UserImage
                {
                    UserId = user_id,
                    IsSafe = false,
                    Hash = result_image_hash,
                    StorageUrl = result_image_url,
                    CreatedAt = DateTime.UtcNow,
                    ModifiedAt = DateTime.UtcNow,
                    Active = true,
                });
                await ctx.SaveChangesAsync();
                //result.image_data;
                //result.mime_type;

                // TODO rate limiting using both user ID and IP address
                return Ok(new
                {
                    message = "OK",
                    image_url = result_image_url,
                });
            }
            catch (Exception ex)
            {
                Log.Information(ex.ToString());
                Log.Information(ex.Message);
                return StatusCode(500, new
                {
                    message = "internal server error",
                });
            }
        }

        private async Task<string?> compute_hash_from_url(string input_url)
        {
            var image_downloader_http_client = http_client_factory.CreateClient(Utils.DOWNLOAD_REMOTE_IMAGE_HTTP_CLIENT_NAME);
            if (image_downloader_http_client == null)
            {
                Log.Error($"failed to create image_downloader_http_client");
                return null;
            }

            var image_data_stream = await Utils.PullImageData(
                input_url,
                image_downloader_http_client,
                env.WebRootPath
            );

            if (image_data_stream == null)
            {
                Log.Error($"failed to PullImageData");
                Log.Error(input_url);
                return null;
            }

            return await Utils.ComputeFileHashAsync(image_data_stream);
        }

        private async Task<UserImage?> SyncPetPictureWithUserImages(PetPicture input_obj)
        {
            // TODO check for removed/disabled flag and consider business logic
            string image_url = input_obj.Url;
            var user_image_obj = await ctx.UserImages.FirstOrDefaultAsync(obj => (obj.StorageUrl == image_url));
            if (user_image_obj == null)
            {
                Log.Debug($"syncing PetPicture and UserImage - {input_obj.PetPictureId}");
                var data_hash = await compute_hash_from_url(image_url);
                if (data_hash == null)
                {
                    Log.Debug($"failed to compute data hash for image_url {input_obj.PetPictureId} - {image_url}");
                    return null;
                }

                int? user_id = null;

                if (input_obj.Pet == null)
                {
                    var pet = await ctx.Pets.FirstOrDefaultAsync(obj => (obj.PetId == input_obj.PetId));
                    if (pet == null)
                    {
                        Log.Debug($"failed to get Pet obj ({input_obj.PetId}) for PetPicture ({input_obj.PetPictureId})");
                        return null; // TODO return debug message
                    }

                    user_id = pet.UserId;
                }
                else
                {
                    user_id = input_obj.Pet.UserId;
                }

                if (user_id == null)
                {
                    Log.Debug($"failed to get owner id for PetPicture {input_obj.PetPictureId}");
                    return null;
                }

                user_image_obj = new UserImage
                {
                    IsSafe = false,
                    StorageUrl = image_url,
                    UserId = user_id.Value, // TODO pull pet object
                    CreatedAt = input_obj.CreatedAt,
                    ModifiedAt = DateTime.UtcNow,
                    Hash = data_hash,
                    Active = true,
                };

                await ctx.UserImages.AddAsync(user_image_obj);
                await ctx.SaveChangesAsync();
                return user_image_obj;
            }

            return user_image_obj;
        }

        //private void CheckPetPictureMimeType(PetPicture input_obj)
        //{
        //    input_obj.Url;

        //}

        private void CheckUserImageMineType()
        {

        }

        public class users_update_display_name_dto
        {
            public required string display_name { get; set; }
        }

        public class GENERIC_RESPONSE_DTO
        {
            public required string message { get; set; }
        }

        [HttpPost("/api/users/name")]
        [Authorize]
        [ProducesResponseType(typeof(GENERIC_RESPONSE_DTO), 200)]
        public async Task<ActionResult> UpdateDisplayName([FromBody] users_update_display_name_dto input_obj)
        {
            var userIdClaim = User.FindFirst(CustomClaimTypes.UserId);
            if (userIdClaim == null || !Int32.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized();
            }

            string? new_display_name = input_obj?.display_name;
            if (string.IsNullOrWhiteSpace(new_display_name) || new_display_name.Length > 100)
            {
                return BadRequest(new { message = "Invalid display name" });
            }

            var user = await ctx.Users.FindAsync(userId);
            if (user == null)
            {
                return Unauthorized(new { message = "Unauthorized" });
            }

            user.DisplayName = new_display_name;
            ctx.Users.Update(user);
            await ctx.SaveChangesAsync();

            return Ok(new { message = "Display name updated successfully" });
        }

        public class InitiateChat_RESPONSE_DTO : GENERIC_RESPONSE_DTO
        {
            public required int chatThreadId { get; set; }
        }

        [HttpPost("/api/chat/initiate/{other_user_id}")]
        [Authorize]
        [ProducesResponseType(typeof(InitiateChat_RESPONSE_DTO), 200)]
        public async Task<ActionResult> InitiateChat([FromRoute] int other_user_id)
        {
            var userIdClaim = User.FindFirst(CustomClaimTypes.UserId);
            if (userIdClaim == null || !Int32.TryParse(userIdClaim.Value, out int meId))
            {
                return Unauthorized();
            }

            // TODO check if other user exists and is active
            var otherUser = await ctx.Users.FindAsync(other_user_id);
            if (otherUser == null)
            {
                return BadRequest(new { message = "The user you are trying to chat with does not exist." });
            }

            // Find existing chat thread
            var thread = await ctx.ChatThreads.FirstOrDefaultAsync(ct =>
                (ct.UserAId == meId && ct.UserBId == other_user_id) ||
                (ct.UserAId == other_user_id && ct.UserBId == meId));

            if (thread == null)
            {
                // Create a new thread if one doesn't exist
                thread = new ChatThread { UserAId = meId, UserBId = other_user_id };
                ctx.ChatThreads.Add(thread);
                await ctx.SaveChangesAsync();
            }

            return Ok(new { chatThreadId = thread.Id });
        }

        // endpoint to get the message history for a chat thread
        [Authorize]
        [HttpGet("/api/chat/{thread_id}/messages")]
        [ProducesResponseType(typeof(ChatMessageDto), 200)]
        public async Task<ActionResult> GetChatMessages([FromRoute] int thread_id)
        {
            var userIdClaim = User.FindFirst(CustomClaimTypes.UserId);
            if (userIdClaim == null || !Int32.TryParse(userIdClaim.Value, out int meId))
            {
                return Unauthorized();
            }

            var thread = await ctx.ChatThreads
                .Include(ct => ct.Messages)
                .ThenInclude(obj => obj.SenderUser)
                .FirstOrDefaultAsync(ct => ct.Id == thread_id);

            if (thread == null || (thread.UserAId != meId && thread.UserBId != meId))
            {
                return NotFound(new { message = "Chat thread not found or access denied." });
            }

            var messages = thread.Messages
                .OrderBy(m => m.CreatedAt)
                .Select(m => new
                {
                    id = m.Id,
                    senderUserId = m.SenderUserId,
                    sender_name = m.SenderUser.DisplayName,
                    content = m.Content,
                    timestamp = ((DateTimeOffset)m.CreatedAt).ToUnixTimeSeconds(),
                    //Timestamp = (m.CreatedAt).ToUnixTimeSeconds(),
                });

            return Ok(new { messages });
        }

        // Chat message for /api/chat/{thread_id}/messages
        public class ChatMessageDto
        {
            public int id { get; set; }
            public int senderUserId { get; set; }
            public string content { get; set; }
            public long timestamp { get; set; }
        }

        public class GetChatMessagesResponseDto
        {
            public required List<ChatMessageDto> messages { get; set; }
        }

        public class matching_rating_dto
        {
            public required int pet_id { get; set; }
            public required int rating { get; set; } // -1: dislike, 0: neutral, 1: like
        }

        [HttpPost("/api/matching-records")]
        [Authorize]
        public async Task<ActionResult> MatchingStoreRating([FromBody] matching_rating_dto input_obj)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new
                    {
                        message = "bad request", // TODO replace with error status code
                    });
                }

                int pet_id = input_obj.pet_id;
                int rating = input_obj.rating;

                var user_id_claim = User.FindFirst(CustomClaimTypes.UserId);
                if (user_id_claim == null)
                {
                    return Unauthorized(new
                    {
                        message = "User ID not found in token", // TODO replace with error status code
                    });
                }

                int user_id;

                if (!Int32.TryParse(user_id_claim.Value, out user_id))
                {
                    return Unauthorized(new
                    {
                        message = "Invalid user ID in token"
                    });
                }

                var user = await ctx.Users
                    .Where(obj => obj.Id == user_id) // TODO add active check
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return Unauthorized(new
                    {
                        message = "User not found"
                    });
                }

                //var pet_obj = ctx.Pets.Where(obj => ((obj.PetId == petId) && (obj.UserId == user_id))).FirstOrDefault();
                var pet_obj = ctx.Pets
                    .Where(obj => (
                        (obj.PetId == pet_id)
                    && (obj.UserId != user_id)
                    && (obj.Active == true)
                    )).FirstOrDefault();

                if (pet_obj == null)
                {
                    return BadRequest(new
                    {
                        message = "bad request",
                    });
                }

                var current_pet_version = pet_obj.ModifiedAt ?? pet_obj.CreatedAt;

                var existing_matching_record = ctx.MatchingRecords.Where(obj => (
                    (obj.UserId == user_id)
                    && (obj.PetId == pet_id)
                    && (obj.PetVersionTime == current_pet_version)
                )).FirstOrDefault();

                if (existing_matching_record == null)
                {
                    MatchingRecord record = new MatchingRecord()
                    {
                        UserId = user_id,
                        PetId = pet_id,
                        PetVersionTime = current_pet_version,
                        SnapshotJsonData = "TODO - for training ML model or past context",
                        CreatedAt = DateTime.UtcNow,
                        Rating = rating,
                    };

                    ctx.MatchingRecords.Add(record);
                    await ctx.SaveChangesAsync();
                    existing_matching_record = record;
                }
                else
                {
                    existing_matching_record.ModifiedAt = DateTime.Now;
                    existing_matching_record.Rating = rating;
                    ctx.MatchingRecords.Update(existing_matching_record);
                    await ctx.SaveChangesAsync();
                }

                return Ok(new
                {
                    message = "OK",
                    id = existing_matching_record.Id,
                    // TODO add more info
                });
            }
            catch (Exception ex)
            {
                Log.Information(ex.ToString());
                Log.Information(ex.Message);
                return StatusCode(500, new
                {
                    message = "internal server error",
                });
            }
        }

        [HttpPost("/api/pets/{pet_id}/set_profile_image/{image_id}")]
        [Authorize]
        public async Task<ActionResult> SetProfileImage([FromRoute] int pet_id, [FromRoute] int image_id)
        {

            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new
                    {
                        message = "bad request", // TODO replace with error status code
                    });
                }

                var user_id_claim = User.FindFirst(CustomClaimTypes.UserId);
                if (user_id_claim == null)
                {
                    return Unauthorized(new
                    {
                        message = "User ID not found in token", // TODO replace with error status code
                    });
                }

                int user_id;

                if (!Int32.TryParse(user_id_claim.Value, out user_id))
                {
                    return Unauthorized(new
                    {
                        message = "Invalid user ID in token"
                    });
                }

                var user = await ctx.Users
                    .Where(obj => obj.Id == user_id) // TODO add active check
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return Unauthorized(new
                    {
                        message = "User not found"
                    });
                }

                //var pet_obj = ctx.Pets.Where(obj => ((obj.PetId == petId) && (obj.UserId == user_id))).FirstOrDefault();
                var pet_obj = ctx.Pets
                    .Include(obj => obj.ProfilePicture)
                    .Include(obj => obj.Pictures)
                    .Where(obj => (
                        (obj.PetId == pet_id)
                    && (obj.UserId == user_id)
                    && (obj.Active == true)
                    )).FirstOrDefault();

                if (pet_obj == null)
                {
                    return BadRequest(new
                    {
                        message = "bad request | this is not your pet. what are you trying to do",
                    });
                }

                var picture = pet_obj.Pictures.Where(obj => obj.PetPictureId == image_id).FirstOrDefault();
                if (picture == null)
                {
                    return BadRequest(new
                    {
                        message = "bad request | invalid image_id",
                    });
                }

                pet_obj.ProfilePictureId = image_id;
                ctx.Pets.Update(pet_obj);
                await ctx.SaveChangesAsync();

                return Ok(new
                {
                    id = pet_obj.PetId,
                    name = pet_obj.Name,
                    description = pet_obj.Description,
                    profile_image_id = pet_obj.ProfilePictureId,
                    profile_image_url = ((pet_obj.ProfilePicture == null) ? null : pet_obj.ProfilePicture.Url),
                    images = pet_obj.Pictures.Select(picture => new
                    {
                        id = picture.PetPictureId,
                        url = picture.Url,
                        created_ts = picture.CreatedAt.ToUnixTS(),
                    }),
                    // TODO
                });
            }
            catch (Exception ex)
            {
                Log.Information(ex.ToString());
                Log.Information(ex.Message);
                return StatusCode(500, new
                {
                    message = "internal server error",
                });
            }
        }

        [HttpGet("/api/pets/matching")]
        [Authorize]
        public async Task<ActionResult> GetMatchingFeed()
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new
                    {
                        message = "bad request", // TODO replace with error status code
                    });
                }

                var user_id_claim = User.FindFirst(CustomClaimTypes.UserId);
                if (user_id_claim == null)
                {
                    return Unauthorized(new
                    {
                        message = "User ID not found in token", // TODO replace with error status code
                    });
                }

                int user_id;

                if (!Int32.TryParse(user_id_claim.Value, out user_id))
                {
                    return Unauthorized(new
                    {
                        message = "Invalid user ID in token"
                    });
                }

                var user = await ctx.Users
                    .Where(obj => obj.Id == user_id) // TODO add active check
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return Unauthorized(new
                    {
                        message = "User not found"
                    });
                }

                var retval = ctx.Pets.Include(pet => pet.ProfilePicture)
                .Include(pet => pet.Pictures)
                .Where(pet => (
                    (pet.Active == true)
                    && (pet.UserId != user_id)
                    && (pet.UserId != user_id)
                    // filter past matching records (with current Pet info version - modified time)
                    // TODO write extensive tests for this
                    && (!ctx.MatchingRecords.Any(mr => (
                        (mr.UserId == user_id)
                        && (mr.PetId == pet.PetId)
                        && (mr.PetVersionTime >= (pet.ModifiedAt ?? pet.CreatedAt))
                    )))
                ))
                //.OrderBy(r => Guid.NewGuid())
                //.Take(10) // TODO take more to upload to ranking function
                .Take(20)
                .AsEnumerable()
                .OrderBy(r => Guid.NewGuid())
                .Select(pet => new
                {
                    id = pet.PetId,
                    name = pet.Name,
                    owner_id = pet.UserId,
                    description = pet.Description,
                    profile_image_id = pet.ProfilePictureId,
                    profile_image_url = ((pet.ProfilePicture == null) ? "" : pet.ProfilePicture.Url),
                    images = pet.Pictures.Select(picture => new
                    {
                        id = picture.PetPictureId,
                        url = picture.Url,
                        created_ts = picture.CreatedAt.ToUnixTS(),
                    }),
                }).ToList();

                // TODO upload this to ML model/ranking function for better matches
                return Ok(new
                {
                    pets = retval,
                });
            }
            catch (Exception ex)
            {
                Log.Information(ex.ToString());
                Log.Information(ex.Message);
                return StatusCode(500, new
                {
                    message = "internal server error",
                });
            }
        }

        [HttpGet("/api/chats")]
        [Authorize]
        public async Task<ActionResult> GetChatThreadList()
        {
            return Ok();
        }

        [HttpGet("/api/pets/{petId}")]
        [Authorize]
        public async Task<ActionResult> GetPetInfo([FromRoute] int petId)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new
                    {
                        message = "bad request", // TODO replace with error status code
                    });
                }

                var user_id_claim = User.FindFirst(CustomClaimTypes.UserId);
                if (user_id_claim == null)
                {
                    return Unauthorized(new
                    {
                        message = "Unauthorized",
                        //message = "User ID not found in token", // TODO replace with error status code
                    });
                }

                int user_id;

                if (!Int32.TryParse(user_id_claim.Value, out user_id))
                {
                    return Unauthorized(new
                    {
                        //message = "Invalid user ID in token",
                        message = "Unauthorized",
                    });
                }

                var user = await ctx.Users
                    .Where(obj => obj.Id == user_id) // TODO add active check
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return Unauthorized(new
                    {
                        //message = "User not found"
                        message = "Unauthorized",
                    });
                }

                //var pet_obj = ctx.Pets.Where(obj => ((obj.PetId == petId) && (obj.UserId == user_id))).FirstOrDefault();
                var pet_obj = ctx.Pets
                    .Include(obj => obj.ProfilePicture)
                    .Include(obj => obj.Pictures)
                    .Where(obj => (
                        (obj.PetId == petId)
                    //&& (obj.UserId == user_id)
                    )).FirstOrDefault();
                if (pet_obj == null)
                {
                    return BadRequest(new
                    {
                        message = "bad request",
                    });
                }

                return Ok(new
                {
                    id = pet_obj.PetId,
                    name = pet_obj.Name,
                    owner_id = pet_obj.UserId,
                    can_edit = (pet_obj.UserId == user_id),
                    description = pet_obj.Description,
                    profile_image_id = pet_obj.ProfilePictureId,
                    profile_image_url = ((pet_obj.ProfilePicture == null) ? null : pet_obj.ProfilePicture.Url),
                    images = pet_obj.Pictures.Select(picture => new
                    {
                        id = picture.PetPictureId,
                        url = picture.Url,
                        created_ts = picture.CreatedAt.ToUnixTS(),
                    }),
                    // TODO
                });
            }
            catch (Exception ex)
            {
                Log.Information(ex.ToString());
                Log.Information(ex.Message);
                return StatusCode(500, new
                {
                    message = "internal server error",
                });
            }
        }

        public class new_pet_dto
        {
            public required string name { get; set; }
        }

        [HttpPost("/api/pets/new")]
        [Authorize]
        public async Task<ActionResult> CreateNewPet([FromBody] new_pet_dto input_obj)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new
                    {
                        message = "bad request", // TODO replace with error status code
                    });
                }

                var user_id_claim = User.FindFirst(CustomClaimTypes.UserId);
                if (user_id_claim == null)
                {
                    return Unauthorized(new
                    {
                        message = "User ID not found in token", // TODO replace with error status code
                    });
                }

                if (Int32.TryParse(user_id_claim.Value, out int user_id))
                {
                    var user = await ctx.Users
                        .Where(obj => obj.Id == user_id) // TODO add active check
                        .FirstOrDefaultAsync();

                    if (user == null)
                    {
                        return Unauthorized(new
                        {
                            message = "User not found"
                        });
                    }

                    if(string.IsNullOrWhiteSpace(input_obj.name) || input_obj.name.Length > 100)
                    {
                        return BadRequest(new
                        {
                            message = "Invalid pet name"
                        });
                    }

                    Pet pet = new Pet()
                    {
                        Name = input_obj.name,
                        UserId = user.Id,
                    };

                    await ctx.Pets.AddAsync(pet);
                    await ctx.SaveChangesAsync();

                    return Ok(new
                    {
                        message = "OK",
                        pet = new
                        {
                            id = pet.PetId,
                        },
                    });

                    //return Ok(new
                    //{
                    //    user = user
                    //});
                }
                else
                {
                    return Unauthorized(new
                    {
                        message = "Invalid user ID in token"
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Information(ex.ToString());
                Log.Information(ex.Message);
                return StatusCode(500, new
                {
                    message = "internal server error",
                });
            }
        }

        public class upload_image_dto
        {
            public required string name { get; set; }
            public required IFormFile file { get; set; }
        }

        [HttpPost("/api/users/images/upload")]
        [Authorize]
        [RequestSizeLimit(Utils.UPLOAD_IMAGE_SIZE_LIMIT)]
        [RequestFormLimits(MultipartBodyLengthLimit = Utils.UPLOAD_IMAGE_SIZE_LIMIT)]
        public async Task<ActionResult> UploadUserImage([FromForm] upload_image_dto input_obj)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new
                    {
                        message = "bad request", // TODO replace with error status code
                    });
                }

                var file = input_obj.file;

                var user_id_claim = User.FindFirst(CustomClaimTypes.UserId);
                if (user_id_claim == null)
                {
                    return Unauthorized(new
                    {
                        message = "User ID not found in token", // TODO replace with error status code
                    });
                }

                int user_id;

                if (!Int32.TryParse(user_id_claim.Value, out user_id))
                {
                    return Unauthorized(new
                    {
                        message = "Invalid user ID in token"
                    });
                }

                var user = await ctx.Users
                    .Where(obj => obj.Id == user_id) // TODO add active check
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return Unauthorized(new
                    {
                        message = "User not found"
                    });
                }

                if (file == null || file.Length == 0)
                {
                    return BadRequest(new
                    {
                        message = "No file uploaded",
                    });
                }

                using (var stream = file.OpenReadStream())
                {
                    if (stream.Length > Utils.UPLOAD_IMAGE_SIZE_LIMIT)
                    {
                        return BadRequest(new
                        {
                            message = "file size exceeds limit",
                        });
                    }

                    if (stream.Length == 0)
                    {
                        return BadRequest(new
                        {
                            message = "empty file"
                        });
                    }

                    string file_hash;
                    try
                    {
                        file_hash = await Utils.ComputeFileHashAsync(stream);
                    }
                    catch (Exception file_hash_ex)
                    {
                        Log.Error(file_hash_ex.Message);
                        Log.Error(file_hash_ex.ToString());
                        return StatusCode(500, new
                        {
                            message = "internal server error",
                        });
                    }

                    var cache_obj = await ctx.ImageDataMimeCaches.FirstOrDefaultAsync(obj => obj.Hash == file_hash);

                    string? media_type = null;
                    if (cache_obj == null)
                    {
                        bool is_valid = IMAGE_MINE_TYPE.ValidateImageDataUsingImageSharp(stream, out media_type);
                        // TODO should we store unknown image file type as unknown?
                        if (media_type != null)
                        {
                            await ctx.ImageDataMimeCaches.AddAsync(new ImageDataMimeCache
                            {
                                Hash = file_hash,
                                MimeType = media_type,
                                UserId = user_id,
                                CreatedAt = DateTime.UtcNow,
                            });

                            await ctx.SaveChangesAsync();
                        }

                        if (!is_valid)
                        {
                            return BadRequest(new
                            {
                                message = "unsupported image file",
                            });
                        }
                    }
                    else
                    {
                        media_type = cache_obj.MimeType;
                    }

                    if (string.IsNullOrWhiteSpace(media_type))
                    {
                        return BadRequest(new
                        {
                            message = "unsupported image file",
                        });
                    }

                    if (!IMAGE_MINE_TYPE.IsValidImageMimeType(media_type))
                    {
                        return BadRequest(new
                        {
                            message = "unsupported image file",
                        });
                    }

                    stream.Seek(0, SeekOrigin.Begin);
                    // TODO check file content

                    // TODO check image content with Vision API

                    try
                    {
                        var public_image_url = await upload_service.UploadImageAsync(stream, media_type);
                        if (public_image_url == null)
                        {
                            return StatusCode(500, new
                            {
                                message = "internal server error | failed to upload image",
                            });
                        }

                        // TODO call google vision API

                        UserImage user_image = new UserImage
                        {
                            UserId = user_id,
                            StorageUrl = public_image_url,
                            Hash = file_hash,
                            IsSafe = false,
                            CreatedAt = DateTime.UtcNow,
                            ModifiedAt = DateTime.UtcNow,
                            Active = true,
                        };

                        await ctx.UserImages.AddAsync(user_image);
                        await ctx.SaveChangesAsync();

                        return Ok(new
                        {
                            message = "OK",
                            image_info = new
                            {
                                id = user_image.Id,
                                url = user_image.StorageUrl,
                                created_ts = user_image.CreatedAt.ToUnixTS(),// TODO unix timestamp
                            }
                        });
                    }
                    catch (Exception gcs_ex)
                    {
                        Log.Information(gcs_ex.ToString());
                        Log.Information(gcs_ex.Message);
                        // TODO add more specific error code and status code
                        return StatusCode(500, new
                        {
                            message = "internal server error | failed to upload image",
                            //message = "internal server error",
                        });
                        // TODO
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Information(ex.ToString());
                Log.Information(ex.Message);
                return StatusCode(500, new
                {
                    message = "internal server error",
                });
            }
        }

        [HttpPost("/api/pets/{petId}/images/upload")]
        [Authorize]
        [RequestSizeLimit(Utils.UPLOAD_IMAGE_SIZE_LIMIT)]
        [RequestFormLimits(MultipartBodyLengthLimit = Utils.UPLOAD_IMAGE_SIZE_LIMIT)]
        public async Task<ActionResult> UploadPetImage([FromForm] upload_image_dto input_obj, [FromRoute] int petId)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new
                    {
                        message = "bad request", // TODO replace with error status code
                    });
                }

                var file = input_obj.file;

                var user_id_claim = User.FindFirst(CustomClaimTypes.UserId);
                if (user_id_claim == null)
                {
                    return Unauthorized(new
                    {
                        message = "User ID not found in token", // TODO replace with error status code
                    });
                }

                int user_id;

                if (!Int32.TryParse(user_id_claim.Value, out user_id))
                {
                    return Unauthorized(new
                    {
                        message = "Invalid user ID in token"
                    });
                }

                var user = await ctx.Users
                    .Where(obj => obj.Id == user_id) // TODO add active check
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return Unauthorized(new
                    {
                        message = "User not found"
                    });
                }

                if (file == null || file.Length == 0)
                {
                    return BadRequest(new
                    {
                        message = "No file uploaded",
                    });
                }

                var pet_obj = ctx.Pets.Where(obj => ((obj.PetId == petId) && (obj.UserId == user_id))).FirstOrDefault();
                if (pet_obj == null)
                {
                    return BadRequest(new
                    {
                        message = "bad request | this is not your pet. what are you trying to do",
                    });
                }

                using (var stream = file.OpenReadStream())
                {
                    if (stream.Length > Utils.UPLOAD_IMAGE_SIZE_LIMIT)
                    {
                        return BadRequest(new
                        {
                            message = "file size exceeds limit",
                        });
                    }

                    if (stream.Length == 0)
                    {
                        return BadRequest(new
                        {
                            message = "empty file"
                        });
                    }

                    string file_hash;
                    try
                    {
                        file_hash = await Utils.ComputeFileHashAsync(stream);
                    }
                    catch (Exception file_hash_ex)
                    {
                        Log.Error(file_hash_ex.Message);
                        Log.Error(file_hash_ex.ToString());
                        return StatusCode(500, new
                        {
                            message = "internal server error",
                        });
                    }

                    var cache_obj = await ctx.ImageDataMimeCaches.FirstOrDefaultAsync(obj => obj.Hash == file_hash);

                    string? media_type = null;
                    if (cache_obj == null)
                    {
                        bool is_valid = IMAGE_MINE_TYPE.ValidateImageDataUsingImageSharp(stream, out media_type);
                        // TODO should we store unknown image file type as unknown?
                        if (media_type != null)
                        {
                            await ctx.ImageDataMimeCaches.AddAsync(new ImageDataMimeCache
                            {
                                Hash = file_hash,
                                MimeType = media_type,
                                UserId = user_id,
                                CreatedAt = DateTime.UtcNow,
                            });

                            await ctx.SaveChangesAsync();
                        }

                        if (!is_valid)
                        {
                            return BadRequest(new
                            {
                                message = "unsupported image file",
                            });
                        }
                    }
                    else
                    {
                        media_type = cache_obj.MimeType;
                    }

                    if (string.IsNullOrWhiteSpace(media_type))
                    {
                        return BadRequest(new
                        {
                            message = "unsupported image file",
                        });
                    }

                    if (!IMAGE_MINE_TYPE.IsValidImageMimeType(media_type))
                    {
                        return BadRequest(new
                        {
                            message = "unsupported image file",
                        });
                    }

                    stream.Seek(0, SeekOrigin.Begin);
                    // TODO check file content

                    // TODO check image content with Vision API

                    try
                    {
                        var public_image_url = await upload_service.UploadImageAsync(stream, media_type);
                        if (public_image_url == null)
                        {
                            return StatusCode(500, new
                            {
                                message = "internal server error | failed to upload image",
                            });
                        }

                        // TODO call google vision API

                        UserImage user_image = new UserImage
                        {
                            UserId = user_id,
                            StorageUrl = public_image_url,
                            Hash = file_hash,
                            IsSafe = false,
                            CreatedAt = DateTime.UtcNow,
                            ModifiedAt = DateTime.UtcNow,
                            Active = true,
                        };

                        await ctx.UserImages.AddAsync(user_image);
                        await ctx.SaveChangesAsync();

                        PetPicture picture = new PetPicture()
                        {
                            Url = public_image_url,
                            CreatedAt = DateTime.UtcNow,
                            PetId = petId,
                        };

                        await ctx.PetPictures.AddAsync(picture);
                        await ctx.SaveChangesAsync();

                        bool updated_as_profile_picture = false;
                        // TODO automatically set the pet profile picture if currently unset
                        if (pet_obj.ProfilePictureId == null)
                        {
                            pet_obj.ProfilePictureId = picture.PetPictureId;

                            ctx.Pets.Update(pet_obj);
                            await ctx.SaveChangesAsync();
                            updated_as_profile_picture = true;
                        }
                        // TODO sync UI to reflect current state more accurately

                        return Ok(new
                        {
                            message = "OK",
                            image_info = new
                            {
                                id = picture.PetPictureId,
                                url = picture.Url,
                                created_ts = picture.CreatedAt.ToUnixTS(),// TODO unix timestamp
                            },
                            updated_as_profile_picture = updated_as_profile_picture,
                        });
                    }
                    catch (Exception gcs_ex)
                    {
                        Log.Information(gcs_ex.ToString());
                        Log.Information(gcs_ex.Message);
                        // TODO add more specific error code and status code
                        return StatusCode(500, new
                        {
                            message = "internal server error | failed to upload image",
                            //message = "internal server error",
                        });
                        // TODO
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Information(ex.ToString());
                Log.Information(ex.Message);
                return StatusCode(500, new
                {
                    message = "internal server error",
                });
            }
        }

        [HttpGet("/api/matches")]
        [Authorize]
        public async Task<ActionResult> GetMatches()
        {
            try
            {
                var user_id_claim = User.FindFirst(CustomClaimTypes.UserId);
                if (user_id_claim == null || !Int32.TryParse(user_id_claim.Value, out int me_id))
                {
                    return Unauthorized(new { message = "Invalid user ID in token" });
                }

                var me = await ctx.Users.FindAsync(me_id);
                if (me == null)
                {
                    return Unauthorized(new { message = "User not found" });
                }

                // Get IDs of all pets owned by the current user
                var my_pet_ids = await ctx.Pets
                    .Where(p => p.UserId == me_id)
                    .Select(p => p.PetId)
                    .ToListAsync();

                // Get all "like" records that are either made by the current user or made on their pets.
                // This single query fetches all data needed to calculate matches.
                var all_relevant_likes = await ctx.MatchingRecords
                    .Include(r => r.Pet).ThenInclude(p => p.ProfilePicture)
                    .Include(r => r.User)
                    .Where(r => r.Rating == 1 && (r.UserId == me_id || my_pet_ids.Contains(r.PetId)))
                    .ToListAsync();

                // Separate the fetched records into likes made by the user and likes they received.
                var my_likes = all_relevant_likes.Where(r => r.UserId == me_id).ToList();
                var likes_on_my_pets = all_relevant_likes.Where(r => my_pet_ids.Contains(r.PetId)).ToList();

                // Group records by the other user involved.
                var other_users_i_liked = my_likes.GroupBy(r => r.Pet.UserId);
                var users_who_liked_me = likes_on_my_pets.GroupBy(r => r.UserId);

                // A match occurs with users who are in both groups (I liked them AND they liked me).
                var matched_user_ids = other_users_i_liked.Select(g => g.Key)
                    .Intersect(users_who_liked_me.Select(g => g.Key));

                var matches_response = new List<object>();

                foreach (var other_user_id in matched_user_ids)
                {
                    var other_user = await ctx.Users.FindAsync(other_user_id);
                    if (other_user == null) continue;

                    var my_likes_on_their_pets_records = my_likes.Where(r => r.Pet.UserId == other_user_id);
                    var their_likes_on_my_pets_records = likes_on_my_pets.Where(r => r.UserId == other_user_id);

                    // The match is considered created at the time of the most recent "like" between the two users.
                    var creation_time = my_likes_on_their_pets_records
                        .Concat(their_likes_on_my_pets_records)
                        .Max(r => r.CreatedAt);

                    matches_response.Add(new
                    {
                        user_a = new { id = me.Id, name = me.DisplayName },
                        user_b = new { id = other_user.Id, name = other_user.DisplayName },
                        user_a_liked_pets = my_likes_on_their_pets_records.Select(r => new
                        {
                            id = r.Pet.PetId,
                            name = r.Pet.Name,
                            owner_id = r.Pet.UserId,
                            description = r.Pet.Description,
                            profile_image_id = r.Pet.ProfilePictureId,
                            profile_image_url = r.Pet.ProfilePicture?.Url,
                        }),
                        user_b_liked_pets = their_likes_on_my_pets_records.Select(r => new
                        {
                            id = r.Pet.PetId,
                            name = r.Pet.Name,
                            owner_id = r.Pet.UserId,
                            description = r.Pet.Description,
                            profile_image_id = r.Pet.ProfilePictureId,
                            profile_image_url = r.Pet.ProfilePicture?.Url,
                        }),
                        creation_time = ((DateTimeOffset)creation_time).ToUnixTimeSeconds(),
                    });
                }

                return Ok(new
                {
                    matches = matches_response
                });
            }
            catch (Exception ex)
            {
                Log.Information(ex.ToString());
                Log.Information(ex.Message);
                return StatusCode(500, new
                {
                    message = "internal server error",
                });
            }
        }
        //public object? FindAllMatches(int user_id)
        //{
        //    var me = ctx.Users.Where(obj => (
        //        (obj.Id == user_id)
        //        && (obj.Active == true)
        //    )).FirstOrDefault();

        //    if (me == null)
        //    {
        //        return null;
        //    }
        //    var my_pet_ids = ctx.Pets.Where(p =>
        //        (p.UserId == user_id)
        //        && (p.Active == true)
        //    )
        //        .Select(p => p.PetId)
        //        .ToList();

        //    var all_relevant_likes = ctx.MatchingRecords
        //        .Include(r => r.Pet).ThenInclude(p => p.ProfilePicture)
        //        .Include(r => r.User)
        //        .Where(r => (
        //            (r.Rating > 0)
        //            && ((r.UserId == user_id) || (my_pet_ids.Contains(r.PetId))
        //        ))).ToList();

        //    var my_likes = all_relevant_likes.Where(r => r.UserId == user_id).ToList();
        //    var likes_on_my_pets = all_relevant_likes.Where(r => my_pet_ids.Contains(r.PetId)).ToList();


        //    var other_users_i_liked = my_likes.GroupBy(r => r.Pet.UserId);
        //    var users_who_liked_me = likes_on_my_pets.GroupBy(r => r.UserId);

        //    var matched_user_ids = other_users_i_liked.Select(g => g.Key)
        //        .Intersect(users_who_liked_me.Select(g => g.Key));

        //    var matches_response = new List<object>();

        //    foreach (var other_user_id in matched_user_ids)
        //    {
        //        var other_user = ctx.Users.Find(other_user_id);
        //        if (other_user == null) { continue; }

        //        var my_likes_on_their_pets_records = my_likes.Where(r => r.Pet.UserId == other_user_id);
        //        var their_likes_on_my_pets_records = likes_on_my_pets.Where(r => r.UserId == other_user_id);

        //        var creation_time = my_likes_on_their_pets_records.Concat(their_likes_on_my_pets_records)
        //            .Max(r => r.CreatedAt);


        //        matches_response.Add(new
        //        {
        //            user_a = new { id = me.Id, name = me.DisplayName },
        //            user_b = new { id = other_user.Id, name = other_user.DisplayName },
        //            user_a_liked_pets = my_likes_on_their_pets_records.Select(r => new
        //            {
        //                id = r.Pet.PetId,
        //                name = r.Pet.Name,
        //                owner_id = r.Pet.UserId,
        //                description = r.Pet.Description,
        //                profile_image_id = r.Pet.ProfilePictureId,
        //                profile_image_url = r.Pet.ProfilePicture?.Url,
        //            }),
        //            user_b_liked_pets = their_likes_on_my_pets_records.Select(r => new
        //            {
        //                id = r.Pet.PetId,
        //                name = r.Pet.Name,
        //                owner_id = r.Pet.UserId,
        //                description = r.Pet.Description,
        //                profile_image_id = r.Pet.ProfilePictureId,
        //                profile_image_url = r.Pet.ProfilePicture?.Url,
        //            }),
        //            creation_time = ((DateTimeOffset)creation_time).ToUnixTimeSeconds(),
        //        });
        //    }

        //    return matches_response;
        //}

        [HttpGet("/api/users/me")]
        [Authorize]
        public async Task<ActionResult> GetCurrentUser()
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new
                    {
                        message = "bad request",
                    });
                }

                // TODO get user id from claim
                foreach (var claim in User.Claims)
                {
                    Log.Information($"Claim: {claim.Type} = {claim.Value}");
                }

                // var user_id_claim = User.FindFirst(JwtRegisteredClaimNames.Sub);
                var user_id_claim = User.FindFirst(CustomClaimTypes.UserId);
                if (user_id_claim == null)
                {
                    return Unauthorized(new
                    {
                        //message = "Invalid user ID in token",
                        message = "unauthorized",
                    });
                    //return Unauthorized(new { message = "User ID not found in token" });
                }
                int user_id;
                if (!Int32.TryParse(user_id_claim.Value, out user_id))
                {
                    return Unauthorized(new
                    {
                        //message = "Invalid user ID in token",
                        message = "unauthorized",
                    });
                }

                var user = await ctx.Users
                    //.Include(obj => obj.Pets)
                    //.ThenInclude(obj => obj.Pictures)
                    .Include(obj => obj.Pets)
                    .ThenInclude(obj => obj.ProfilePicture)
                    .Where(obj => (
                        (obj.Id == user_id)
                        && (obj.Active == true)
                    )) // TODO add active check
                    .Select(obj => new
                    {
                        id = obj.Id,
                        name = obj.DisplayName,
                        is_guest = obj.IsGuest,
                        created_at = obj.CreatedAt,
                        pets = obj.Pets.Select(pet => new
                        {
                            id = pet.PetId,
                            name = pet.Name,
                            description = pet.Description,
                            profile_image_id = pet.ProfilePictureId,
                            profile_image_url = ((pet.ProfilePicture == null) ? null : pet.ProfilePicture.Url),
                            //species = pet.Species,
                            //breed = pet.Breed,
                            //age = pet.Age,
                            //bio = pet.Bio,
                            created_at = pet.CreatedAt,
                        }).ToList(),
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return NotFound(new
                    {
                        message = "User not found",
                    });
                }

                return Ok(new
                {
                    message = "OK",
                    user = user,
                });
            }
            catch (Exception ex)
            {
                Log.Information(ex.ToString());
                Log.Information(ex.Message);
                return StatusCode(500, new
                {
                    message = "internal server error",
                });
            }
        }

        [HttpPost("/api/users/login_with_email")]
        public async Task<ActionResult> LoginWithEmail([FromBody] UserWithEmailAndPasswordDTO input_obj)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    message = "invalid input"
                });
            }

            var auth_provider = ctx.AuthProviders
                .Include(obj => obj.User)
                .Where(obj =>
                    obj.ProvideType == PROVIDER_TYPE.EMAIL &&
                    obj.ProviderKey == input_obj.email &&
                    !string.IsNullOrEmpty(obj.PasswordHash) // Replace IsNullOrEmpty with string.IsNullOrEmpty
                )
                .FirstOrDefault();
            if (auth_provider == null)
            {
                return BadRequest(new
                {
                    message = "This email is not associated with an account!"
                });
            }

            var result = HASHER.VerifyHashedPassword(input_obj.email, auth_provider.PasswordHash, input_obj.password);
            if (result == PasswordVerificationResult.Failed)
            {
                return Unauthorized(new
                {
                    message = "invalid credential"
                });
            }
            else
            {
                if (result == PasswordVerificationResult.SuccessRehashNeeded)
                {
                    // TODO update password hash
                }

                return Ok(new
                {
                    // TODO add JWT
                    access_token = token_service.GenerateToken(auth_provider.User),
                    message = "OK"
                });
            }
        }

        public class UserWithEmailAndPasswordDTO
        {
            public required string email { get; set; }
            public required string password { get; set; }
        }

        [HttpPost("/api/users/create")]
        public async Task<ActionResult> CreateUserWithEmailAndPassword([FromBody] UserWithEmailAndPasswordDTO input_obj)
        {
            // TODO check email and password is valid
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    message = "invalid input"
                });
            }

            await using var transaction = await ctx.Database.BeginTransactionAsync();

            try
            {
                var auth_provider = ctx.AuthProviders.Where(obj => obj.ProvideType.Equals(PROVIDER_TYPE.EMAIL) && obj.ProviderKey.Equals(input_obj.email)).FirstOrDefault();
                if (auth_provider != null)
                {
                    return BadRequest(new
                    {
                        message = "This email is unavailable!"
                    });
                }

                var new_user = new User()
                {
                    IsGuest = false,
                    CreatedAt = DateTime.UtcNow,
                    Pets = [],
                    AuthProviders = [],
                };

                ctx.Users.Add(new_user);
                await ctx.SaveChangesAsync();

                var new_auth_provider = new AuthProviders()
                {
                    UserId = new_user.Id,
                    ProvideType = PROVIDER_TYPE.EMAIL,
                    ProviderKey = input_obj.email,
                    PasswordHash = HASHER.HashPassword(input_obj.email, input_obj.password),
                    CreatedAt = DateTime.UtcNow,
                };

                ctx.AuthProviders.Add(new_auth_provider);
                await ctx.SaveChangesAsync();

                await transaction.CommitAsync();

                return Ok(new
                {
                    access_token = token_service.GenerateToken(new_user),
                    message = "account created successfully",
                });
            }
            catch (Exception ex)
            {
                Log.Information(ex.ToString());
                Log.Information(ex.Message);
                // TODO log exception
                return StatusCode(500, new
                {
                    message = "internal server error"
                });
            }

        }

        private readonly PasswordHasher<string> HASHER = new PasswordHasher<string>();
    }
}
