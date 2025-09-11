using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
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

        [HttpGet("api/pets/matching")]
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

                var retval = await ctx.Pets.Include(pet => pet.ProfilePicture)
                .Include(pet => pet.Pictures)
                .Where(pet => (
                    (pet.Active == true)
                    && (pet.UserId != user_id)
                    && (pet.UserId != user_id)
                    // filter past matching records (with current Pet info version - modified time)
                    // TODO write extensive tests for this
                    && (ctx.MatchingRecords.Any(mr => (
                        (mr.UserId == user_id)
                        && (mr.PetId == pet.PetId)
                        && (mr.PetVersionTime >= (pet.ModifiedAt ?? pet.CreatedAt))
                    )))
                ))
                .OrderBy(r => Guid.NewGuid())
                .Take(10) // TODO take more to upload to ranking function
                .Select(pet => new
                {
                    id = pet.PetId,
                    owner_id = pet.UserId,
                    profile_image_id = pet.ProfilePictureId,
                    profile_image_url = ((pet.ProfilePicture == null) ? "" : pet.ProfilePicture.Url),
                    images = pet.Pictures.Select(picture => new
                    {
                        id = picture.PetPictureId,
                        url = picture.Url,
                        created_ts = picture.CreatedAt.ToFileTimeUtc(),
                    }),
                }).ToListAsync();

                // TODO upload this to ML model/ranking function for better matches
                return Ok(new
                {
                    pets = retval,
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Console.WriteLine(ex.Message);
                return StatusCode(500, new
                {
                    message = "internal server error",
                });
            }
        }

        [HttpGet("api/pets/{petId}")]
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

                //var user_id_claim = User.FindFirst(CustomClaimTypes.UserId);
                //if (user_id_claim == null)
                //{
                //    return Unauthorized(new
                //    {
                //        message = "User ID not found in token", // TODO replace with error status code
                //    });
                //}

                //int user_id;

                //if (!Int32.TryParse(user_id_claim.Value, out user_id))
                //{
                //    return Unauthorized(new
                //    {
                //        message = "Invalid user ID in token"
                //    });
                //}

                //var user = await ctx.Users
                //    .Where(obj => obj.Id == user_id) // TODO add active check
                //    .FirstOrDefaultAsync();

                //if (user == null)
                //{
                //    return Unauthorized(new
                //    {
                //        message = "User not found"
                //    });
                //}

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
                        message = "bad request | this is not your pet. what are you trying to do",
                    });
                }

                return Ok(new
                {
                    id = pet_obj.PetId,
                    name = pet_obj.Name,
                    description = pet_obj.Description,
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
                Console.WriteLine(ex);
                Console.WriteLine(ex.Message);
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

        [HttpPost("api/pets/new")]
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
                Console.WriteLine(ex);
                Console.WriteLine(ex.Message);
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
                        Console.WriteLine(gcs_ex);
                        Console.WriteLine(gcs_ex.Message);
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

                return StatusCode(500, "TODO");
                //return Ok(new
                //{
                //    user = user
                //});

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Console.WriteLine(ex.Message);
                return StatusCode(500, new
                {
                    message = "internal server error",
                });
            }
        }

        [HttpGet("/api/users/me")]
        [Authorize]
        public async Task<ActionResult> GetCurrentUser()
        {
            // TODO get user id from claim
            foreach (var claim in User.Claims)
            {
                Console.WriteLine($"Claim: {claim.Type} = {claim.Value}");
            }

            // var user_id_claim = User.FindFirst(JwtRegisteredClaimNames.Sub);
            var user_id_claim = User.FindFirst(CustomClaimTypes.UserId);
            if (user_id_claim == null)
            {
                return Unauthorized(new { message = "User ID not found in token" });
            }

            if (Int32.TryParse(user_id_claim.Value, out int user_id))
            {
                var user = await ctx.Users
                    .Include(obj => obj.Pets)
                    .Where(obj => obj.Id == user_id) // TODO add active check
                    .Select(obj => new
                    {
                        id = obj.Id,
                        is_guest = obj.IsGuest,
                        created_at = obj.CreatedAt,
                        pets = obj.Pets.Select(pet => new
                        {
                            id = pet.PetId,
                            name = pet.Name,
                            description = pet.Description,
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
                        message = "User not found"
                    });
                }

                return Ok(new
                {
                    user = user
                });
            }
            else
            {
                return Unauthorized(new
                {
                    message = "Invalid user ID in token"
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
                Console.WriteLine(ex);
                Console.WriteLine(ex.Message);
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
