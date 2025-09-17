using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
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
        private readonly StorageClient google_cloud_storage_client;
        private readonly GoogleCloudStorageConfig gcs_config;

        public UsersController(
            AppDbContext ctx,
            TokenService token_service,
            StorageClient google_cloud_storage_client,
            GoogleCloudStorageConfig gcs_config
        )
        {
            this.ctx = ctx;
            this.token_service = token_service;
            this.google_cloud_storage_client = google_cloud_storage_client;
            this.gcs_config = gcs_config;
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
                        created_ts = picture.CreatedAt.ToFileTimeUtc(),
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
                        created_ts = picture.CreatedAt.ToFileTimeUtc(),
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
                        created_ts = picture.CreatedAt.ToFileTimeUtc(),
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

        public const int UPLOAD_IMAGE_SIZE_LIMIT = 5242880; // (5 * 1024 * 1024);

        [HttpPost("/api/pets/{petId}/images/upload")]
        [Authorize]
        [RequestSizeLimit(UPLOAD_IMAGE_SIZE_LIMIT)]
        [RequestFormLimits(MultipartBodyLengthLimit = UPLOAD_IMAGE_SIZE_LIMIT)]
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
                    if (stream.Length > UPLOAD_IMAGE_SIZE_LIMIT)
                    {
                        return BadRequest(new
                        {
                            message = "file size exceeds limit",
                        });
                    }

                    // TODO extract this code
                    string? media_type = null;
                    // check with request header media type
                    var buffer = new byte[8];
                    await stream.ReadAsync(buffer, 0, buffer.Length);
                    // Check for PNG (89 50 4E 47 0D 0A 1A 0A)
                    if (buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47 &&
                       buffer[4] == 0x0D && buffer[5] == 0x0A && buffer[6] == 0x1A && buffer[7] == 0x0A)
                    {
                        // It's a PNG
                        media_type = "image/png";
                    }
                    // Check for JPEG (FF D8 FF)
                    else if (buffer[0] == 0xFF && buffer[1] == 0xD8 && buffer[2] == 0xFF)
                    {
                        // It's a JPEG
                        media_type = "image/jpeg";
                    }
                    else
                    {
                        return BadRequest(new
                        {
                            message = "Unsupported file type",
                        });
                    }

                    stream.Seek(0, SeekOrigin.Begin);
                    // TODO check file content

                    // TODO check image content with Vision API

                    if (string.IsNullOrWhiteSpace(media_type))
                    {
                        // TODO
                        return BadRequest(new
                        {
                            message = "invalid image file",
                        });
                    }

                    string bucketName = gcs_config.BUCKET_NAME;
                    string objectName = $"users/upload/{Guid.NewGuid()}";

                    try
                    {
                        var gcs_object = google_cloud_storage_client.UploadObject(bucketName, objectName, media_type, stream);
                        if (gcs_object == null)
                        {
                            return StatusCode(500, new
                            {
                                message = "internal server error | failed to upload image",
                            });
                        }
                        var public_image_url = gcs_object.MediaLink;
                        PetPicture picture = new PetPicture()
                        {
                            Url = public_image_url,
                            CreatedAt = DateTime.UtcNow,
                            PetId = petId,
                        };
                        await ctx.PetPictures.AddAsync(picture);
                        await ctx.SaveChangesAsync();

                        return Ok(new
                        {
                            message = "OK",
                            image_info = new
                            {
                                id = picture.PetPictureId,
                                url = picture.Url,
                                created_ts = picture.CreatedAt.ToFileTimeUtc(),// TODO unix timestamp
                            },
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
                // TODO validate file type using magic header
                //var stream = file.OpenReadStream();
                //stream.ReadAsync();

                //return StatusCode(500, "TODO");
                //return Ok(new
                //{
                //    user = user
                //});

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
