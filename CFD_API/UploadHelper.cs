using CFD_API.Azure;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

namespace CFD_API
{
    public class UploadHelper
    {
        public static List<string> UploadFiles(MultipartFormDataStreamProvider provider, string containerName)
        {
            CloudBlobClient client;
            var container = BlobHelper.GetWebApiContainer(containerName, out client);
            var UploadedFiles = new List<AzureFileDetails>();
            foreach (var fileData in provider.FileData)
            {
                var uniqueName = Path.GetFileName(fileData.LocalFileName);
                ICloudBlob blob = container.GetBlockBlobReference(uniqueName + fileData.Headers.ContentDisposition.FileName.Trim('\"'));
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

            UploadedFiles = UploadedFiles.Where(f => f != null).ToList();

            List<string> imgUriList = UploadedFiles.Select(file => file.Location.AbsoluteUri).ToList();

            return imgUriList;
        }

        public static Dictionary<string, string> GetFormData(MultipartFormDataStreamProvider provider)
        {
            Dictionary<string, string> formDataDic = new Dictionary<string, string>();
            foreach(string key in provider.FormData.AllKeys)
            {
                formDataDic.Add(key, provider.FormData[key]);
            }

            return formDataDic;
        }
    }
}