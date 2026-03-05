using DotnetLearning.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DotnetLearning.Controllers
{
    [ApiController]
    public class AccountController:ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }
        public record RegisterDto(string? FirstName, string? LastName,string Email, string Password, bool? RememberMe,bool? isTeacher);
        public record LoginDto(string Email, string Password, bool? RememberMe);
        [HttpPost]
        [Route("/api/auth/register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto register)
        {
            var user = new ApplicationUser { FirstName = register.FirstName, LastName = register.LastName, UserName = register.Email, Email = register.Email };
            var result = await _userManager.CreateAsync(user, register.Password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, register.isTeacher == true ? "Teacher" : "User");
                await _signInManager.SignInAsync(user, isPersistent: false);
                var roles = await _userManager.GetRolesAsync(user);
                var role = roles.FirstOrDefault();
                return Ok(new { id = user.Id, email = user.Email, role = role });
            }
            return BadRequest(result.Errors);
        }
        [HttpPost]
        [Route("/api/auth/login")]
        public async Task<IActionResult> Login([FromBody] LoginDto login)
        {
            var result = await _signInManager.PasswordSignInAsync(login.Email, login.Password, login.RememberMe??false, false);
            if (result.Succeeded)
            { 
                var user = await _userManager.FindByEmailAsync(login.Email);
                var roles = await _userManager.GetRolesAsync(user);
                var role = roles.FirstOrDefault();
                return Ok(new { id = user.Id, email = user.Email, role = role });
            }
            return BadRequest("Invalid Login Attempt.");
        }
        [Authorize]
        [Route("/api/auth/me")]
        [HttpGet]
        public IActionResult GetLoggedInDetails()
        {
            return Ok(new
            {
                id = User.FindFirstValue(ClaimTypes.NameIdentifier),
                email = User.FindFirstValue(ClaimTypes.Email),
                role = User.FindFirstValue(ClaimTypes.Role)
            });
        }

        [HttpPost]
        [Route("/api/auth/logout")]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return Ok();

        }

    }
}
