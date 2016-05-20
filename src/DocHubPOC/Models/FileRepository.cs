using Microsoft.Net.Http.Headers;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace DocHubPOC.Models
{
    /// <summary>
    /// This class implement the IFileRepository Interface for Azure
    /// </summary>
    public class FileRepository : IFileRepository
    {
        private CloudBlobClient _blobClient;
        private ILogger _thisLog;

        /// <summary>
        /// Initialize the class with logging provider and CloudBlobClient.
        /// It use StorageUtils to obtain the correct Azure Storge information.
        /// </summary>
        public FileRepository()
        {
            _thisLog = Log.ForContext<FileRepository>();
            _blobClient = null;

            try
            {
                CloudStorageAccount storageAccount = StorageUtils.StorageAccount;
                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                _blobClient = blobClient;
            }
            catch (Exception ex)
            {
                Log.Error("Error with FileRepository Constructor: {@ex}", ex);
                throw ex;
            }
        }

        public async Task<FileItem> Add(FileUpload upload)
        {
            _thisLog.Debug("Add - Get Blob container");

            try
            {
                CloudBlobContainer container = _blobClient.GetContainerReference(upload.Container);

                await container.CreateIfNotExistsAsync();

                string lastID = null;

                _thisLog.Debug("Add - Parse uploaded files");
                foreach (var file in upload.Files)
                {
                    if (file.Length > 0)
                    {
                        var fileName = ContentDispositionHeaderValue.Parse(file.ContentDisposition).FileName.Trim('"');
                        _thisLog.Debug("Add - Will upload {@filename}", fileName);
                        CloudBlockBlob blockBlob = container.GetBlockBlobReference(fileName);
                        blockBlob.Properties.ContentType = file.ContentType;
                        await blockBlob.UploadFromStreamAsync(file.OpenReadStream());
                        _thisLog.Information("Add - {@filename} uploaded", fileName);
                        lastID = fileName;
                    }
                }

                return new FileItem { Container = upload.Container, Id = lastID };
            }
            catch (Exception ex)
            {
                _thisLog.Error("Add - Error: {@ex}", ex);
            }

            return null;
        }

        public async Task<string> Find(string container, string id)
        {
            _thisLog.Debug("Find - Container {@container} file {@id}", container, id);

            try
            {
                CloudBlobContainer blolbContainer = _blobClient.GetContainerReference(container);
                CloudBlockBlob blockBlob = blolbContainer.GetBlockBlobReference(id);

                if (await blockBlob.ExistsAsync())
                {
                    _thisLog.Information("Find - Blob {@container}\\{@id} exist. Generate access.", container, id);
                    // Return an uri to the file with a limited time validity (sasConstraints)
                    var sasConstraints = new SharedAccessBlobPolicy();
                    sasConstraints.SharedAccessStartTime = DateTime.UtcNow.AddMinutes(-5);
                    sasConstraints.SharedAccessExpiryTime = DateTime.UtcNow.AddMinutes(10);
                    sasConstraints.Permissions = SharedAccessBlobPermissions.Read;

                    var sasBlobToken = blockBlob.GetSharedAccessSignature(sasConstraints);

                    return blockBlob.Uri + sasBlobToken;
                }

                _thisLog.Debug("Find - Container {@container} with file {@id} doesn't exist");
                return null;
            }
            catch (Exception ex)
            {
                _thisLog.Error("Find - Error: {@ex}", ex);
            }
            return null;
        }

        public async Task<bool> Exist(string container, string id)
        {
            _thisLog.Debug("Exist - Container {@container} file {@id}", container, id);

            try
            {
                CloudBlobContainer blolbContainer = _blobClient.GetContainerReference(container);
                CloudBlockBlob blockBlob = blolbContainer.GetBlockBlobReference(id);
                return await blockBlob.ExistsAsync();
            }
            catch (Exception ex)
            {
                _thisLog.Error("Find - Error: {@ex}", ex);
            }

            return false;
        }

        public async Task<IEnumerable<string>> GetAllContainer()
        {
            _thisLog.Debug("GetAllContainer - Get Storage Account and BlobClient");

            var continuationToken = new BlobContinuationToken();
            List<string> listContainer = new List<string>();

            try
            {
                do
                {
                    _thisLog.Debug("GetAllContainer - Obtain one Azure API limit batch (5000?)");
                    var listContainerSegment = await _blobClient.ListContainersSegmentedAsync(continuationToken);
                    continuationToken = listContainerSegment.ContinuationToken;
                    _thisLog.Debug("GetAllContainer - Generate List");

                    foreach (var item in listContainerSegment.Results)
                    {
                        listContainer.Add(item.Name);
                    }
                } while (continuationToken != null);

                _thisLog.Debug("GetAllContainer - Return List");
                return listContainer;
            }
            catch (Exception ex)
            {
                _thisLog.Error("Something went wrong during \"GetAllContainer()\": {@ex}", ex);
            }

            return null;
        }

        public async Task<IEnumerable<string>> GetAllFilesInContainer(string container)
        {
            _thisLog.Information("GetAllFilesInContainer - Get Blob container {@container}", container);

            var continuationToken = new BlobContinuationToken();
            List<string> listBlobs = new List<string>();

            try
            {
                CloudBlobContainer blolbContainer = _blobClient.GetContainerReference(container);

                do
                {
                    _thisLog.Debug("GetAllFilesInContainer - Obtain one Azure API limit batch (5000?)");
                    var listBlobSegment = await blolbContainer.ListBlobsSegmentedAsync(continuationToken);
                    continuationToken = listBlobSegment.ContinuationToken;

                    _thisLog.Debug("GetAllFilesInContainer - Generate List");
                    foreach (CloudBlockBlob item in listBlobSegment.Results)
                    {
                        listBlobs.Add(item.Name);
                    }
                } while (continuationToken != null);

                _thisLog.Debug("GetAllFilesInContainer - Return List");
                return listBlobs;
            }
            catch (Exception ex)
            {
                _thisLog.Error("GetAllFilesInContainer - Error: {@ex}", ex);
            }

            return null;
        }

        /// <summary>
        /// Used to generate a unique temporary folder.
        /// </summary>
        /// <returns>The path of the folder</returns>
        private string GetTemporaryDirectory()
        {
            _thisLog.Debug("GetZipGetTemporaryDirectory - Generate and create folder");
            string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }

        public async Task<MemoryStream> GetZip(string container, string selectedFiles)
        {
            _thisLog.Debug("GetZip - {selectedFiles}", selectedFiles);
            string uTempPath = string.Empty;
            string tempZipFile = string.Empty;
            string missingFiles = string.Empty;

            CloudBlobContainer blolbContainer = _blobClient.GetContainerReference(container);
            Dictionary<int, string> filesExistID = new Dictionary<int, string>();
            List<Task<bool>> fileExistT = new List<Task<bool>>();
            List<Task> fileDLT = new List<Task>();

            try
            {
                if (!await blolbContainer.ExistsAsync())
                {
                    return null;
                }

                uTempPath = GetTemporaryDirectory() + '\\';

                _thisLog.Debug("GetZip - Let's parse the requested files");

                //Parse the string and create task to ask Azure if file exist
                foreach (var file in selectedFiles.Split(';'))
                {
                    Task<bool> cTask = blolbContainer.GetBlockBlobReference(file).ExistsAsync();
                    fileExistT.Add(cTask);
                    filesExistID.Add(cTask.Id, file);
                }

                _thisLog.Debug("GetZip - Let's find the requested files");
                //Loop while the task of "file exist query to Azure" is not empty
                //When any task complete, check the result (exist or not)
                //if exist create a task to download the file in a temporary folder. Else add to the missing files string.
                while (fileExistT.Count > 0)
                {
                    Task<bool> t = await Task.WhenAny(fileExistT);
                    fileExistT.Remove(t);
                    string file = filesExistID[t.Id];
                    bool exist = await t;
                    if (exist)
                    {
                        fileDLT.Add(blolbContainer.GetBlockBlobReference(file).DownloadToFileAsync(uTempPath + file, FileMode.Create));
                    }
                    else
                    {
                        missingFiles += string.Format("{0} : File does not exist at generation time.{1}", file, Environment.NewLine);
                    }
                }
                //Create a redme file to put in the zip
                String readmeText = "This is a zip file dynamically generated by MyAld at " + System.DateTime.Now.ToString("G");
                System.IO.File.WriteAllText(uTempPath + "Readme.txt", readmeText);

                //If there are missing files add a "MissingFiles.txt" containing the nam of thos files to the zip
                if (!string.IsNullOrEmpty(missingFiles))
                {
                    System.IO.File.WriteAllText(uTempPath + "MissingFiles.txt", missingFiles);
                }

                _thisLog.Debug("GetZip - Wait for all tasks to finish");
                await Task.WhenAll(fileDLT);

                //When all file have downloaded, generate a zip file with a random name in the temporary folder
                tempZipFile = Path.GetTempPath() + Path.GetRandomFileName() + ".zip";
                ZipFile.CreateFromDirectory(uTempPath, tempZipFile);
                _thisLog.Debug("GetZip - Filecreated");
                              

                //Put the zip file in memory and return it
                byte[] filedata = File.ReadAllBytes(tempZipFile);

                MemoryStream msZip = new MemoryStream(filedata);
                return msZip;
            }
            catch (Exception ex)
            {
                _thisLog.Error(ex, "Unable to create Zip file");
                throw;
            }
            finally
            {
                _thisLog.Debug("GetZip - Delete temporary files");

                if (!string.IsNullOrEmpty(uTempPath) && Directory.Exists(uTempPath))
                {
                    Directory.Delete(uTempPath, true);
                }
                if (File.Exists(tempZipFile))
                {
                    File.Delete(tempZipFile);
                }
            }
        }

        public async Task<FileItem> Remove(string container, string id)
        {
            _thisLog.Debug("Remove - Container {@container} file {@id}", container, id);

            try
            {
                CloudBlobContainer blolbContainer = _blobClient.GetContainerReference(container);
                CloudBlockBlob blockBlob = blolbContainer.GetBlockBlobReference(id);

                FileItem result = new FileItem { Container = container, Id = id };

                _thisLog.Information("Remove - Try to delete Container {@container} file {@id}", container, id);
                if (await blockBlob.DeleteIfExistsAsync())
                {
                    return result;
                }
            }
            catch (Exception ex)
            {
                _thisLog.Error("Remove - Error: {@ex}", ex);
            }

            return null;
        }
    }
}