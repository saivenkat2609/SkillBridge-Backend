using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace SkillBridge.Models
{
    public class AppDbContext:IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options): base(options)
        {
            
        }
        public DbSet<Skill> Skills { get; set; }
        public DbSet<SkillCategory> SkillCategories { get; set; }
        public DbSet<TeacherProfile> TeacherProfiles { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<TeacherAvailability> TeacherAvailabilities { get; set; }
        public DbSet<AvailabilityException> AvailabilityExceptions { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<Review> Reviews { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Seed();
            modelBuilder.Entity<Booking>()
              .HasOne(b => b.Student)
              .WithMany()
              .HasForeignKey(b => b.StudentId)
              .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Booking>()
              .HasOne(b => b.Teacher)
              .WithMany()
              .HasForeignKey(b => b.TeacherId)
              .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Message>()
              .HasOne(b => b.Sender)
              .WithMany()
              .HasForeignKey(b => b.SenderId)
              .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Message>()
              .HasOne(b => b.Receiver)
              .WithMany()
              .HasForeignKey(b => b.ReceiverId)
              .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Review>()
              .HasOne(b => b.Reviewer)
              .WithMany()
              .HasForeignKey(b => b.ReviewerId)
              .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Review>()
              .HasOne(b => b.Teacher)
              .WithMany()
              .HasForeignKey(b => b.TeacherId)
              .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Review>()
              .HasOne(b => b.Skill)
              .WithMany()
              .HasForeignKey(b => b.SkillId)
              .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
