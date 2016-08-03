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
            Func<Tuple<string, Dictionary<string, string>>, T> actionOnFormData)
        {
            if (!req.Content.IsMimeMultipartContent()) actionOnFormData(null);
            // multipart form, i.e there is a profile picture to receive
            CloudBlobClient client;
            var azureProvider =
                new AzureBlobStorageMultipartProvider(
                    BlobHelper.GetWebApiContainer("banner-img", out client));

            // Read the form data and upload to azure
            await req.Content.ReadAsMultipartAsync(azureProvider);
            // Get the first file uploaded (we expect only one)
            var files = azureProvider.UploadedFiles;

            files = files.Where(f => f != null).ToList();

            Dictionary<string, string> formData = new Dictionary<string, string>();
            foreach (var key in azureProvider.FormData.AllKeys)
            {//接收FormData  
                formData.Add(key, azureProvider.FormData[key]);
            }

            string imgageUrl = files.FirstOrDefault() == null ? string.Empty : files.FirstOrDefault().Location.AbsoluteUri;
            return actionOnFormData(new Tuple<string, Dictionary<string, string>>(imgageUrl, formData));
        }
    }
}