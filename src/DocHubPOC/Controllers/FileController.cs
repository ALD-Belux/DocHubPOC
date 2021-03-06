﻿using DocHubPOC.Models;
using Microsoft.AspNet.Hosting;
using Microsoft.AspNet.Mvc;
using Microsoft.Extensions.OptionsModel;
using Serilog;
using System.Threading.Tasks;

namespace DocHubPOC.Controllers
{
    [Route("api/[controller]")]
    public class FileController : Controller
    {
        private IHostingEnvironment _environment;
        private IOptions<DHConfig> _config;
        private ILogger _thisLog;

        /// <summary>
        /// Initalize the Web API controller
        /// </summary>
        /// <param name="environment">Web hosting environment</param>
        /// <param name="config">The strong typed configuration class</param>
        public FileController(IHostingEnvironment environment, IOptions<DHConfig> config)
        {
            _environment = environment;
            _config = config;
            _thisLog = Log.ForContext<FileController>();
        }

        /// <summary>
        /// Add our data model from services
        /// </summary>
        [FromServices]
        public IFileRepository FileItems { get; set; }

        /// <summary>
        /// List all container (logical/security partition). Protected with an administration key.
        /// </summary>
        /// <param name="adminKey">The provided admin key</param>
        /// <returns>Send the result over https</returns>
        [HttpGet("list/{adminKey}", Name = "GetContainers")]
        public async Task<IActionResult> GetAllContainer(string adminKey)
        {
            _thisLog.Information("Try to Get all containers");

            //the administration key is stored using user-secret in dev and environment variable in prod.
            if (adminKey == _config.Value.AdminKey)
            {
                _thisLog.Information("Admin Key is good. Executing \"FileItems.GetAllContainer()\"");
                var result = await FileItems.GetAllContainer();
                if (result != null)
                {
                    return Json(result);
                }
                else
                {
                    _thisLog.Information("No result returned");
                    return HttpNotFound();
                }
            }
            else
            {
                _thisLog.Information("Admin Key is not good!");
                return HttpNotFound();
            }
        }

        /// <summary>
        /// List all files in a container (logical/security partition).
        /// </summary>
        /// <param name="adminKey">The administration key</param>
        /// <param name="container">The container to get files from</param>
        /// <returns>A list of files</returns>
        [HttpGet("list/{adminKey}/{container}", Name = "GetFilesList")]
        public async Task<IActionResult> GetFilesList(string adminKey, string container)
        {
            _thisLog.Information("Try to Get all files in {@container}", container);
            container = container.ToLower();

            //the administration key is stored using user-secret in dev and environment variable in prod.
            if (adminKey == _config.Value.AdminKey)
            {
                _thisLog.Information("Admin Key is good. Executing \"FileItems.GetAllFilesInContainer({@container})\"", container);
                var result = await FileItems.GetAllFilesInContainer(container);

                if (result != null)
                {
                    return Json(result);
                }

                return HttpNotFound();
            }
            else
            {
                _thisLog.Information("Admin Key is not good!");
                return HttpNotFound();
            }
        }

        /// <summary>
        /// Get a specified file in a specified container (logical/security partition).
        /// </summary>
        /// <param name="container">The container</param>
        /// <param name="id">The file</param>
        /// <returns>Redirect to the download link</returns>
        [HttpGet("get/{container}/{id}", Name = "GetFile")]
        public async Task<IActionResult> GetFile(string container, string id)
        {
            _thisLog.Information("Try to Get {@id} file in {@container}", id, container);
            container = container.ToLower();
            var item = await FileItems.Find(container, id);
            if (item == null)
            {
                _thisLog.Information("File {@id} not found in {@container}", id, container);
                return HttpNotFound();
            }
            _thisLog.Information("File {@id} found in {@container} - Redirect to {@item}", id, container, item);
            return Redirect(item);
        }

        /// <summary>
        /// Return true if the file in a specific container exist
        /// </summary>
        /// <param name="container">The container</param>
        /// <param name="id">The file</param>
        /// <returns>OK/200 if exist 404 if not</returns>
        [HttpGet("exist/{container}/{id}", Name = "ExistFile")]
        public async Task<IActionResult> ExistFile(string container, string id)
        {
            _thisLog.Information("Try to verify if {@id} file in {@container} exist", id, container);
            container = container.ToLower();
            var exist = await FileItems.Exist(container, id);
            if (!exist)
            {
                _thisLog.Information("File {@id} not found in {@container}", id, container);
                return HttpNotFound();
            }
            _thisLog.Information("File {@id} found in {@container} - Return true", id, container);
            return Ok();
        }

        /// <summary>
        /// Return a zip containing the requested files in a specific container
        /// </summary>
        /// <param name="container">The container name</param>
        /// <param name="selectedFiles">A string containing ";" separated file names</param>
        /// <returns>A zip file</returns>
        [HttpGet("get/zip", Name = "GetZip")]
        public async Task<IActionResult> GetZip(string container, string selectedFiles)
        {
            _thisLog.Information("Request Zip File");

            try
            {
                var memStream = await FileItems.GetZip(container, selectedFiles);

                _thisLog.Information("Return the Zip File");

                if (memStream.Equals(null))
                {
                    _thisLog.Information("got a null answer");
                    return HttpNotFound();
                }
                _thisLog.Information("Return the Zip File");
                return new FileStreamResult(memStream, "application/zip") { FileDownloadName = string.Format("MyALD-{0:yyyyMMddHHmmssfff}.zip", System.DateTime.Now) };
            }
            catch (System.Exception)
            {
                return HttpNotFound();
            }
        }

        /// <summary>
        /// Delete a file in a container(logical/security partition). Protected with an administration key.
        /// </summary>
        /// <param name="adminKey">The administration key</param>
        /// <param name="container">The container</param>
        /// <param name="id">The file</param>
        /// <returns>The deleted Item metadata</returns>
        [HttpGet("delete/{adminKey}/{container}/{id}", Name = "DeleteFile")]
        public async Task<IActionResult> DeletFile(string adminKey, string container, string id)
        {
            _thisLog.Information("Try to delete {@id} file in {@container}", id, container);
            container = container.ToLower();

            //the administration key is stored using user-secret in dev and environment variable in prod.
            if (adminKey == _config.Value.AdminKey)
            {
                var item = await FileItems.Remove(container, id);
                if (item == null)
                {
                    _thisLog.Information("File {@id} not found in {@container}", id, container);
                    return HttpNotFound();
                }
                _thisLog.Information("File {@id} found in {@container} and deleted", id, container, item);
                return Json(item);
            }
            else
            {
                return HttpNotFound();
            }
        }

        /// <summary>
        /// Upload a file using a multi-part form like upload.
        /// </summary>
        /// <param name="item">An object containing a container and one or more files</param>
        /// <param name="adminKey">The administration key</param>
        /// <returns>The last file processed</returns>
        [HttpPost("post/{adminKey}", Name = "PostFile")]
        public async Task<IActionResult> Upload(FileUpload item, string adminKey)
        {
            _thisLog.Information("Try to post {@id} file in {@container}", item.Files, item.Container);

            //the administration key is stored using user-secret in dev and environment variable in prod.
            if (adminKey == _config.Value.AdminKey)
            {
                _thisLog.Information("Admin Key is good. Executing \"FileItems.Add({@item})\"", item);
                item.Container = item.Container.ToLower();
                FileItem fItem = await FileItems.Add(item);

                if (fItem != null)
                {
                    _thisLog.Information("File uploaded");
                    return CreatedAtRoute("GetFile", new { controller = "File", container = fItem.Container, id = fItem.Id }, fItem);
                }

                _thisLog.Information("Unable to upload file");
                return HttpBadRequest();
            }
            else
            {
                _thisLog.Information("Admin Key is not good!");
                return HttpNotFound();
            }
        }
    }
}