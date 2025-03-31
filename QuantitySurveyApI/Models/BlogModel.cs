using System;
using System.Collections.Generic;

namespace QuantitySurveyBlogApi.Models
{
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
        public Blog Blog { get; set; }
    }
}