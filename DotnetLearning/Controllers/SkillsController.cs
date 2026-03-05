using DotnetLearning.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;

namespace DotnetLearning.Controllers
{
    [ApiController]
    public class SkillsController : ControllerBase
    {
        
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _environment;
        public record CreateSkillDTO { public string Title { get; set; } public string Description { get; set; } public int PricePerHour { get; set; } public int CategoryId { get; set; } public IFormFile? Image { get; set; } };
        public record GetSkillsDTO(int totalCount, List<Skill> Skills);
        public SkillsController(AppDbContext context,UserManager<ApplicationUser> userManager, IWebHostEnvironment environment)
        {
            _context = context;
            _userManager = userManager;
            _environment = environment;
        }
        [HttpGet]
        [Route("api/categories")]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _context.SkillCategories.ToListAsync();
            return Ok(categories);
        }

        [HttpGet]
        [Route("api/skills")]
        public async Task<IActionResult> GetSkills([FromQuery] SkillQueryParams skillFilters)
        {
            var query = _context.Skills.Include(s => s.Category).AsQueryable();
            if (!string.IsNullOrEmpty(skillFilters.SearchTerm))
                query = query.Where(s => s.Title.Contains(skillFilters.SearchTerm));
            if(skillFilters.CategoryId.HasValue)
                query = query.Where(s => s.CategoryId == skillFilters.CategoryId);
            if(skillFilters.MinPrice>0)
                query = query.Where(s => s.PricePerHour >= skillFilters.MinPrice);
            if(skillFilters.MaxPrice!=int.MaxValue)
                query = query.Where(s => s.PricePerHour <= skillFilters.MaxPrice);
            query = skillFilters.SortBy switch
            {
                "price_asc" => query.OrderBy(s => s.PricePerHour),
                "price_desc" => query.OrderByDescending(s => s.PricePerHour),
                "rating" => query.OrderByDescending(s => s.Rating),
                _ => query.OrderBy(s => s.CreatedAt)
            };
            var totalCount = await query.CountAsync();
            var pageSkills=query.Skip(skillFilters.PageSize* (skillFilters.Page - 1)).Take(skillFilters.PageSize);
            var skills = await pageSkills.ToListAsync();

            return Ok(new GetSkillsDTO(
                totalCount,
                skills
            ));
        }
        [HttpGet]
        [Route("api/skills/{id}")]
        public async Task<IActionResult> GetSkillById(int id)
        {
            var skill = await _context.Skills.Include(s => s.Category).FirstOrDefaultAsync(s => s.SkillId == id);
            if (skill == null)
            {
                return NotFound();
            }
            return Ok(skill);
        }

        [HttpPost]
        [Route("api/skills")]
        [Authorize(Roles = "Teacher")]
        public async Task<IActionResult> CreateSkill([FromForm] CreateSkillDTO skillDto)
        {
            
            var teacherId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(teacherId);
            var teacherName = $"{user.FirstName} {user.LastName}";
            var skill = new Skill
            {
                Title = skillDto.Title,
                Description = skillDto.Description,
                PricePerHour = skillDto.PricePerHour,
                CategoryId = skillDto.CategoryId,
                TeacherId = teacherId,
                TeacherName = teacherName,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            };
            if (skillDto.Image != null)
            {
                var file = skillDto.Image;
                var extension = Path.GetExtension(file.FileName).ToLower();
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
                var fileSize = file.Length;
                if (!allowedExtensions.Contains(extension) || fileSize > 5 * 1024 * 1024)
                {
                    return BadRequest("Only jpg and png files are allowed");
                }
                var guid = Guid.NewGuid();
                var imageUrlInLocal = Path.Combine(_environment.WebRootPath, "images", "skills",$"{guid}{extension}");
                Directory.CreateDirectory(Path.GetDirectoryName(imageUrlInLocal));
                using var stream = new FileStream(imageUrlInLocal, FileMode.Create);
                await file.CopyToAsync(stream);
                skill.ImageUrl = $"/images/skills/{guid}{extension}";
            }
            _context.Skills.Add(skill);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetSkillById), new { id = skill.SkillId }, skill);
        }
    }
}
