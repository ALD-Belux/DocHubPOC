using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Http;

namespace DocHubPOC.Models
{
    public class FileUpload
    {
        public string Container { get; set; }
        public ICollection<IFormFile> Files { get; set; }
    }
}
