using CFD_API.Azure;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

namespace CFD_API
{
    public class UploadHelper
    {
        public static async Task<T> UploadFiles<T>(HttpRequestMessage req, string containerName,
            Func<List<string>, T> actionOnFormData)
        {
            if (!req.Content.IsMimeMultipartContent()) actionOnFormData(null);
            CloudBlobClient client;
            var azureProvider =
                new AzureBlobStorageMultipartProvider(
                    BlobHelper.GetWebApiContainer(containerName, out client));

            await req.Content.ReadAsMultipartAsync(azureProvider);
            var files = azureProvider.UploadedFiles;

            files = files.Where(f => f != null).ToList();

            List<string> imgUriList = files.Select(file => file.Location.AbsoluteUri).ToList();

            return actionOnFormData(imgUriList);
        }

        public static async Task<T> GetFormData<T>(HttpRequestMessage req, Func<Dictionary<string, string>, T> actionOnFormData)
        {
            if (!req.Content.IsMimeMultipartContent()) actionOnFormData(null);
            NameValueCollection formData = await req.Content.ReadAsFormDataAsync();
            Dictionary<string, string> formDataDic = new Dictionary<string, string>();
            foreach(string key in formData.AllKeys)
            {
                formDataDic.Add(key, formData[key]);
            }

            return actionOnFormData(formDataDic);
        }
    }
}