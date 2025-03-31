using Microsoft.EntityFrameworkCore;
using QuantitySurveyBlogApi.Models;

namespace QuantitySurveyBlogApi.Data
{
    public class BlogDbContext : DbContext
    {
        public BlogDbContext(DbContextOptions<BlogDbContext> options) : base(options) { }

        public DbSet<Blog> Blogs { get; set; }
        public DbSet<BlogImage> BlogImages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Blog>()
                .HasMany(b => b.Images)
                .WithOne(i => i.Blog)
                .HasForeignKey(i => i.BlogId);
        }
    }
}