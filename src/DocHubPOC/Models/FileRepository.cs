using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Net.Http.Headers;
using Serilog;


namespace DocHubPOC.Models
{
    /// <summary>
    /// This class implement the IFileRepository Interface for Azure
    /// </summary>
    public class FileRepository : IFileRepository
    {
        private ILogger _thisLog;
        private CloudBlobClient _blobClient;

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
            catch (Exception ex )
            {
                _thisLog.Error("Something went wrong during \"GetAllContainer()\": {@ex}", ex);
            }

            return null;
        }

        public async Task <IEnumerable<string>> GetAllFilesInContainer(string container)
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

        public async Task<FileItem> Remove(string container, string id)
        {
            _thisLog.Debug("Remove - Container {@container} file {@id}", container, id);

            try
            {
                CloudBlobContainer blolbContainer = _blobClient.GetContainerReference(container);
                CloudBlockBlob blockBlob = blolbContainer.GetBlockBlobReference(id);

                FileItem result = new FileItem { Container = container, Id = id };

                _thisLog.Information("Remove - Try to delete Container {@container} file {@id}", container, id);
                if(await blockBlob.DeleteIfExistsAsync())
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
