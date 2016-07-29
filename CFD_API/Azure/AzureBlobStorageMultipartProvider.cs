using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

namespace CFD_API.Azure
{
    public class AzureBlobStorageMultipartProvider : MultipartFormDataStreamProvider
    {
        private readonly CloudBlobContainer _container;

        public AzureBlobStorageMultipartProvider(CloudBlobContainer container)
            : base(Path.GetTempPath())
        {
            _container = container;
            UploadedFiles = new List<AzureFileDetails>();
        }

        public List<AzureFileDetails> UploadedFiles { get; set; }
        // this method is called once this instance
        // has finished parsing the form data
        // we then upload them to Azure
        public override Task ExecutePostProcessingAsync()
        {
            // Upload the files to azure blob storage and remove them from local disk
            foreach (var fileData in FileData)
            {
                var uniqueName = Path.GetFileName(fileData.LocalFileName);
                Trace.WriteLine(fileData.LocalFileName);
                // Retrieve reference to a blob
                ICloudBlob blob = _container.GetBlockBlobReference(uniqueName);
                blob.Properties.ContentType = fileData.Headers.ContentType.MediaType;
                blob.UploadFromStream(new FileStream(fileData.LocalFileName, FileMode.Open));
                //File.Delete(fileData.LocalFileName); // The process cannot access the file 'D:\Windows\TEMP\BodyPart_a37478c4-36c7-44d4-b459-48ee67a7eba0' because it is being used by another process
                UploadedFiles.Add(new AzureFileDetails
                {
                    ContentType = blob.Properties.ContentType,
                    Name = blob.Name,
                    Size = blob.Properties.Length,
                    Location = blob.Uri
                });
            }

            return base.ExecutePostProcessingAsync();
        }
    }
}