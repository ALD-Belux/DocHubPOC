﻿using System;
using Microsoft.AspNet.Mvc;
using DocHubPOC.Models;
using System.Collections.Generic;
using Microsoft.AspNet.Hosting;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Net.Http.Headers;
using Microsoft.AspNet.Http;
using Microsoft.Extensions.OptionsModel;
using Serilog;

namespace DocHubPOC.Controllers
{
    [Route("api/[controller]")]
    public class FileController : Controller
    {
        private IHostingEnvironment _environment;
        private IOptions<DHConfig> _config;
        private ILogger _thisLog;

        public FileController(IHostingEnvironment environment, IOptions<DHConfig> config)
        {
            _environment = environment;
            _config = config;
            _thisLog = Log.ForContext<FileController>();
        }

        [FromServices]
        public IFileRepository FileItems { get; set; }
                
        [HttpGet("admin/{adminKey}", Name = "GetContainers")]
        public async Task<IActionResult> GetAllContainer(string adminKey)
        {
            _thisLog.Information("Try to Get all containers");

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

        [HttpGet("admin/{adminKey}/{container}", Name = "GetFilesList")]
        public async Task<IActionResult> GetFilesList(string adminKey, string container)
        {
            _thisLog.Information("Try to Get all files in {@container}", container);
            container = container.ToLower();

            if (adminKey == _config.Value.AdminKey)
            {
                _thisLog.Information("Admin Key is good. Executing \"FileItems.GetAllFilesInContainer({@container})\"", container);
                return Json(await FileItems.GetAllFilesInContainer(container));
            }
            else
            {
                _thisLog.Information("Admin Key is not good!");
                return HttpNotFound();
            }
        }

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

        [HttpGet("delete/{container}/{id}", Name = "DeleteFile")]
        public async Task<IActionResult> DeletFile(string container, string id)
        {
            _thisLog.Information("Try to delete {@id} file in {@container}", id, container);
            container = container.ToLower();
            var item = await FileItems.Remove(container, id);
            if (item == null)
            {
                _thisLog.Information("File {@id} not found in {@container}", id, container);
                return HttpNotFound();
            }
            _thisLog.Information("File {@id} found in {@container} and deleted", id, container, item);
            return Json(item);
        }

        [HttpPost("post/{adminKey}", Name = "PostFile")]
        public async Task<IActionResult> Upload(FileUpload item, string adminKey)
        {
            _thisLog.Information("Try to post {@id} file in {@container}", item.Files, item.Container);

            if (adminKey == _config.Value.AdminKey)
            {
                _thisLog.Information("Admin Key is good. Executing \"FileItems.Add({@item})\"", item);
                item.Container = item.Container.ToLower();
                FileItem fItem = await FileItems.Add(item);
                return CreatedAtRoute("GetFile", new { controller = "File", container = fItem.Container, id = fItem.Id }, fItem);
            }
            else
            {
                _thisLog.Information("Admin Key is not good!");
                return HttpNotFound();
            }
        }
    }
}
