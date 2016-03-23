using Microsoft.AspNet.Http;
using System.Collections.Generic;

namespace DocHubPOC.Models
{
    /// <summary>
    /// Represent an upload with a "mutli-part form" like POST request
    /// </summary>
    public class FileUpload
    {
        public string Container { get; set; }
        public ICollection<IFormFile> Files { get; set; }
    }
}