using SkillBridge.Models;
using SkillBridge.Services;
using SkillBridge.Utils;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using System.Security.Claims;
using System.Text;

namespace SkillBridge.Controllers
{
    [ApiController]
    public class AccountController:ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;
        public record GoogleLoginDto(string IdToken);
        public record ForgotPasswordDto(string Email);
        public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IConfiguration configuration,IEmailService emailService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _emailService = emailService;
        }
        public record RegisterDto(string? FirstName, string? LastName,string Email, string Password, bool? RememberMe,bool? isTeacher);
        public record LoginDto(string Email, string Password, bool? RememberMe);
        [HttpPost]
        [Route("/api/auth/register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto register)
        {
            var user = new ApplicationUser
            {
                FirstName = register.FirstName,
                LastName = register.LastName,
                UserName = register.Email,
                Email = register.Email,
                EmailConfirmed = false   // ← explicit
            };

            var result = await _userManager.CreateAsync(user, register.Password);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            await _userManager.AddToRoleAsync(user, register.isTeacher == true ? "Teacher" : "User");

            // Generate token & encode it for URL safety
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var frontendUrl = _configuration["Frontend:Url"];
            var link = $"{frontendUrl}/confirm-email?userId={user.Id}&token={encodedToken}";

            // Send the email
            await _emailService.SendAsync(
                user.Email,
                "Confirm your SkillBridge email",
                EmailTemplates.ConfirmEmail(user.FirstName, link)
            );

            return Ok(new { message = "Registration successful! Please check your email." });
        }
        [HttpGet("/api/auth/confirm-email")]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if(user == null)
                return BadRequest("Invalid user ID");
            var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
            var result = await _userManager.ConfirmEmailAsync(user, decodedToken);

            if (result.Succeeded)
                return Ok("Email confirmed successfully!");
            return BadRequest("Error confirming email");
        }
        [HttpPost]
        [Route("/api/auth/login")]
        public async Task<IActionResult> Login([FromBody] LoginDto login)
        {
            var user = await _userManager.FindByEmailAsync(login.Email);
            if (user!=null && user.EmailConfirmed == false)
                return BadRequest("Please confirm your email before logging in.");
            var result = await _signInManager.PasswordSignInAsync(login.Email, login.Password, login.RememberMe??false, false);
            if (result.Succeeded)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var role = roles.FirstOrDefault();
                // Return same shape as Google login so frontend AuthContext works correctly
                // Without isOnboardingComplete, frontend treats it as undefined (falsy)
                // and OnboardingGuard redirects every email login to /onboarding
                return Ok(new
                {
                    id = user.Id,
                    email = user.Email,
                    name = $"{user.FirstName} {user.LastName}",
                    role,
                    isOnboardingComplete = user.IsOnboardingComplete
                });
            }
            return BadRequest("Invalid Login Attempt.");
        }

        [HttpPost("/api/auth/forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            // 1. Find user by email
            var user= await _userManager.FindByEmailAsync(dto.Email);
            if(user == null || user.EmailConfirmed == false)
                return Ok("If that email exists, we sent a reset link");

            // 3. Generate password reset token
            var passwordResetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            // 4. Encode it (same way as before)
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(passwordResetToken));
            // 5. Build link: {frontendUrl}/reset-password?userId=...&token=...
            var frontendUrl = _configuration["Frontend:Url"];
            var link = $"{frontendUrl}/reset-password?userId={user.Id}&token={encodedToken}";
            // 6. Send email (add template to EmailTemplates.cs too!)
            await _emailService.SendAsync(
                user.Email,
                "Reset your SkillBridge password",
                EmailTemplates.ResetPassword(user.FirstName, link)
            );
            return Ok("If that email exists, we sent a reset link");
            // 7. Return?
        }
        public record ResetPasswordDto(string UserId, string Token, string NewPassword);

        [HttpPost("/api/auth/reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            // 1. Find user by UserId
            var user= await _userManager.FindByIdAsync(dto.UserId);
            // 2. If null → return?
            if(user == null)
                return BadRequest("Invalid user");
            // 3. Decode the token (same as ConfirmEmail!)
            var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(dto.Token));

            // 4. Reset the password
            var result = await _userManager.ResetPasswordAsync(user, decodedToken, dto.NewPassword);
            if (result.Succeeded)
            {
                return Ok("Password reset Successfull.");

            }
            return BadRequest("Something error occured.");
            // 5. Return success or failure
        }
        [Authorize]
        [Route("/api/auth/me")]
        [HttpGet]
        public async Task<IActionResult> GetLoggedInDetails()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return Unauthorized();
            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault();
            return Ok(new
            {
                id = user.Id,
                email = user.Email,
                name = $"{user.FirstName} {user.LastName}",
                role,
                isOnboardingComplete = user.IsOnboardingComplete
            });
        }

        [HttpPost]
        [Route("/api/auth/logout")]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return Ok();

        }
        [HttpPost]
        [Route("api/auth/google")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginDto dto)
        {
            // 1. Validate the Google token
            GoogleJsonWebSignature.Payload payload;
            try
            {
                var settings = new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { _configuration["Google:ClientId"] }
                };
                payload = await GoogleJsonWebSignature.ValidateAsync(dto.IdToken, settings);
            }
            catch (InvalidJwtException)
            {
                return Unauthorized("Invalid Google token");
            }

            // 2. Find existing user by email
            var user = await _userManager.FindByEmailAsync(payload.Email);
            var isNewUser = false;

            if (user == null)
            {
                // 3. New user — create with default role, onboarding incomplete
                isNewUser = true;
                user = new ApplicationUser
                {
                    Email = payload.Email,
                    UserName = payload.Email,
                    FirstName = payload.GivenName ?? "",
                    LastName = payload.FamilyName ?? "",
                    EmailConfirmed = true,             // Google already verified the email
                    IsOnboardingComplete = false       // forces onboarding step
                };

                var result = await _userManager.CreateAsync(user);
                if (!result.Succeeded)
                    return BadRequest(result.Errors);

                await _userManager.AddToRoleAsync(user, "Student");
            }
            else
            {
                // 4. Existing user — link Google login if not already linked
                var logins = await _userManager.GetLoginsAsync(user);
                var hasGoogleLogin = logins.Any(l => l.LoginProvider == "Google");
                if (!hasGoogleLogin)
                {
                    await _userManager.AddLoginAsync(user, new UserLoginInfo(
                        "Google",
                        payload.Subject,   // Google's permanent unique ID for this user
                        "Google"
                    ));
                }
            }

            await _signInManager.SignInAsync(user, isPersistent: false);
            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault();
            return Ok(new
            {
                user.Id,
                user.Email,
                Name = $"{user.FirstName} {user.LastName}",
                role,
                user.IsOnboardingComplete,
                IsNewUser = isNewUser
            });
        }
        public record UpdateRoleDto(string Role);

        [HttpPatch]
        [Authorize]
        [Route("api/auth/role")]
        public async Task<IActionResult> UpdateRole([FromBody] UpdateRoleDto dto)
        {
            if (dto.Role != "Student" && dto.Role != "Teacher")
                return BadRequest("Invalid role");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            // Remove old role, add new role
            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, dto.Role);

            // Update user model
            user.IsOnboardingComplete = true;
            await _userManager.UpdateAsync(user);

            // Re-sign in so the Identity cookie reflects the updated role
            await _signInManager.RefreshSignInAsync(user);

            return Ok(new
            {
                id = user.Id,
                email = user.Email,
                name = $"{user.FirstName} {user.LastName}",
                role = dto.Role,
                isOnboardingComplete = true
            });
        }

    }
}
