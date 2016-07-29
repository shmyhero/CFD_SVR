using CFD_API.Azure;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

namespace CFD_API
{
    public class UploadHelper
    {
        public static async Task<T> UploadImage<T>(HttpRequestMessage req,
            Func<AzureFileDetails, T> actionOnFormData)
        {
            if (!req.Content.IsMimeMultipartContent()) return actionOnFormData(null);
            // multipart form, i.e there is a profile picture to receive
            CloudBlobClient client;
            var provider =
                new AzureBlobStorageMultipartProvider(
                    BlobHelper.GetWebApiContainer("banner-img", out client));

            // Read the form data and upload to azure
            await req.Content.ReadAsMultipartAsync(provider);
            // Get the first file uploaded (we expect only one)
            var files = provider.UploadedFiles;

            files = files.Where(f => f != null).ToList();

            return actionOnFormData(files.FirstOrDefault());
        }
    }
}