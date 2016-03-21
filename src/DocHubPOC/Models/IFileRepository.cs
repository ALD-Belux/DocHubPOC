using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DocHubPOC.Models
{
    /// <summary>
    /// This interface must be implemented by the data provider to access the files.
    /// </summary>
    public interface IFileRepository
    {
        /// <summary>
        /// Use to upload one or more file from a multi-part form.
        /// </summary>
        /// <param name="upload">The FileUpload object from the HTTP Post</param>
        /// <returns>The metadata of the last file processed in a FileItem object</returns>
        Task<FileItem> Add(FileUpload upload);

        /// <summary>
        /// Used for administration. Return all the container (logical/security partition)
        /// </summary>
        /// <returns>An IEnumerable of the container</returns>
        Task<IEnumerable<string>> GetAllContainer();

        /// <summary>
        /// Used for administration. Return all the files in a container (logical/security partition).
        /// </summary>
        /// <param name="container">The container to list files from</param>
        /// <returns>An IEnumerable of the files in the given container</returns>
        Task<IEnumerable<string>> GetAllFilesInContainer(string container);

        /// <summary>
        /// Find and return a link to a file in a specific container (logical/security partition).
        /// </summary>
        /// <param name="container">The container</param>
        /// <param name="id">The file</param>
        /// <returns>A link to the file or null</returns>
        Task<string> Find(string container, string id);

        /// <summary>
        /// Usedd for administration. Delete a file in a given container (logical/security partition).
        /// </summary>
        /// <param name="container">The container to delete the file from</param>
        /// <param name="id">The file</param>
        /// <returns>A fileItem representing the deleted file.</returns>
        Task<FileItem> Remove(string container, string id);
    }
}
