using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DocHubPOC.Models
{
    public interface IFileRepository
    {
        Task<FileItem> Add(FileUpload upload);
        Task<IEnumerable<string>> GetAllContainer();
        Task<IEnumerable<string>> GetAllFilesInContainer(string container);
        Task<string> Find(string container, string id);
        Task<FileItem> Remove(string container, string id);
    }
}
