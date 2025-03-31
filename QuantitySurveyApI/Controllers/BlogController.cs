using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace QuantitySurveyBlogApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BlogsController : ControllerBase
    {
        private readonly string _connectionString;

        public BlogsController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        // GET: api/Blogs
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Blog>>> GetBlogs()
        {
            var blogs = new List<Blog>();

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new SqlCommand("sp_GetAllBlogs", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var blog = new Blog
                            {
                                BlogId = reader.GetInt32(reader.GetOrdinal("BlogId")),
                                Title = reader.GetString(reader.GetOrdinal("Title")),
                                Content = reader.GetString(reader.GetOrdinal("Content")),
                                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                                UpdatedAt = reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
                            };

                            blogs.Add(blog);
                        }
                    }
                }
            }

            return blogs;
        }

        // GET: api/Blogs/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Blog>> GetBlog(int id)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new SqlCommand("sp_GetBlogById", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@BlogId", id);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var blog = new Blog
                            {
                                BlogId = reader.GetInt32(reader.GetOrdinal("BlogId")),
                                Title = reader.GetString(reader.GetOrdinal("Title")),
                                Content = reader.GetString(reader.GetOrdinal("Content")),
                                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                                UpdatedAt = reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
                            };

                            // Read associated images
                            blog.Images = new List<BlogImage>();
                            while (await reader.NextResultAsync() && await reader.ReadAsync())
                            {
                                var image = new BlogImage
                                {
                                    ImageId = reader.GetInt32(reader.GetOrdinal("ImageId")),
                                    BlogId = reader.GetInt32(reader.GetOrdinal("BlogId")),
                                    ImageData = (byte[])reader["ImageData"],
                                    ImageMimeType = reader.GetString(reader.GetOrdinal("ImageMimeType")),
                                    Caption = reader.IsDBNull(reader.GetOrdinal("Caption")) ? null : reader.GetString(reader.GetOrdinal("Caption")),
                                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
                                };
                                blog.Images.Add(image);
                            }

                            return blog;
                        }
                    }
                }
            }

            return NotFound();
        }

        // POST: api/Blogs
        [HttpPost]
        public async Task<ActionResult<Blog>> PostBlog([FromForm] BlogCreateDto blogDto)
        {
            var blog = new Blog();

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        using (var command = new SqlCommand("sp_CreateBlog", connection, transaction))
                        {
                            command.CommandType = CommandType.StoredProcedure;

                            command.Parameters.AddWithValue("@Title", blogDto.Title);
                            command.Parameters.AddWithValue("@Content", blogDto.Content);

                            var blogIdParam = new SqlParameter("@BlogId", SqlDbType.Int)
                            {
                                Direction = ParameterDirection.Output
                            };
                            command.Parameters.Add(blogIdParam);

                            await command.ExecuteNonQueryAsync();

                            blog.BlogId = (int)blogIdParam.Value;
                            blog.Title = blogDto.Title;
                            blog.Content = blogDto.Content;
                        }

                        // Handle image uploads
                        if (blogDto.Images != null && blogDto.Images.Any())
                        {
                            for (int i = 0; i < blogDto.Images.Length; i++)
                            {
                                var image = blogDto.Images[i];
                                using var memoryStream = new MemoryStream();
                                await image.CopyToAsync(memoryStream);

                                using (var command = new SqlCommand("sp_AddBlogImage", connection, transaction))
                                {
                                    command.CommandType = CommandType.StoredProcedure;

                                    command.Parameters.AddWithValue("@BlogId", blog.BlogId);
                                    command.Parameters.AddWithValue("@ImageData", memoryStream.ToArray());
                                    command.Parameters.AddWithValue("@ImageMimeType", image.ContentType);
                                    command.Parameters.AddWithValue("@Caption",
                                        blogDto.ImageCaptions?.Length > i ? blogDto.ImageCaptions[i] : DBNull.Value);

                                    await command.ExecuteNonQueryAsync();
                                }
                            }
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }

            return CreatedAtAction("GetBlog", new { id = blog.BlogId }, blog);
        }

        // PUT: api/Blogs/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutBlog(int id, [FromForm] BlogUpdateDto blogDto)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        using (var command = new SqlCommand("sp_UpdateBlog", connection, transaction))
                        {
                            command.CommandType = CommandType.StoredProcedure;

                            command.Parameters.AddWithValue("@BlogId", id);
                            command.Parameters.AddWithValue("@Title", blogDto.Title);
                            command.Parameters.AddWithValue("@Content", blogDto.Content);

                            int rowsAffected = await command.ExecuteNonQueryAsync();

                            if (rowsAffected == 0)
                            {
                                return NotFound();
                            }
                        }

                        // Handle new image uploads
                        if (blogDto.Images != null && blogDto.Images.Any())
                        {
                            for (int i = 0; i < blogDto.Images.Length; i++)
                            {
                                var image = blogDto.Images[i];
                                using var memoryStream = new MemoryStream();
                                await image.CopyToAsync(memoryStream);

                                using (var command = new SqlCommand("sp_AddBlogImage", connection, transaction))
                                {
                                    command.CommandType = CommandType.StoredProcedure;

                                    command.Parameters.AddWithValue("@BlogId", id);
                                    command.Parameters.AddWithValue("@ImageData", memoryStream.ToArray());
                                    command.Parameters.AddWithValue("@ImageMimeType", image.ContentType);
                                    command.Parameters.AddWithValue("@Caption",
                                        blogDto.ImageCaptions?.Length > i ? blogDto.ImageCaptions[i] : DBNull.Value);

                                    await command.ExecuteNonQueryAsync();
                                }
                            }
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }

            return NoContent();
        }

        // DELETE: api/Blogs/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBlog(int id)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new SqlCommand("sp_DeleteBlog", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@BlogId", id);

                    int rowsAffected = await command.ExecuteNonQueryAsync();

                    if (rowsAffected == 0)
                    {
                        return NotFound();
                    }
                }
            }

            return NoContent();
        }

        // GET: api/Blogs/5/Images/1
        [HttpGet("{blogId}/Images/{imageId}")]
        public async Task<IActionResult> GetImage(int blogId, int imageId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new SqlCommand("sp_GetBlogImage", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@BlogId", blogId);
                    command.Parameters.AddWithValue("@ImageId", imageId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var imageData = (byte[])reader["ImageData"];
                            var imageMimeType = reader.GetString(reader.GetOrdinal("ImageMimeType"));

                            return File(imageData, imageMimeType);
                        }
                    }
                }
            }

            return NotFound();
        }
    }

    public class Blog
    {
        public int BlogId { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<BlogImage> Images { get; set; }
    }

    public class BlogImage
    {
        public int ImageId { get; set; }
        public int BlogId { get; set; }
        public byte[] ImageData { get; set; }
        public string ImageMimeType { get; set; }
        public string Caption { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class BlogCreateDto
    {
        public string Title { get; set; }
        public string Content { get; set; }
        public IFormFile[] Images { get; set; }
        public string[] ImageCaptions { get; set; }
    }

    public class BlogUpdateDto
    {
        public string Title { get; set; }
        public string Content { get; set; }
        public IFormFile[] Images { get; set; }
        public string[] ImageCaptions { get; set; }
    }
}