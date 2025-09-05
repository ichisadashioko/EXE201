using System.Security.Cryptography;
using System.Text;
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

        public UsersController(AppDbContext ctx, TokenService token_service)
        {
            this.ctx = ctx;
            this.token_service = token_service;
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

            var auth_provider = ctx.AuthProviders.Include(obj => obj.User).Where(obj => (
                obj.ProvideType.Equals(PROVIDER_TYPE.EMAIL)
                && obj.ProviderKey.Equals(input_obj.email)
                && !obj.PasswordHash.IsNullOrEmpty()
            )).FirstOrDefault();
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
