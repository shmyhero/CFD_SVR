using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace CFD_COMMON.Azure
{
    public class Blob
    {
        public static void UploadFromBytes(string containerName, string blobName, byte[] bytes)
        {
            var storageConStr = CFDGlobal.GetConfigurationSetting("StorageConnectionString");
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageConStr);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            // Retrieve a reference to a container.
            CloudBlobContainer container = blobClient.GetContainerReference(containerName);

            // Create the container if it doesn't already exist.
            container.CreateIfNotExists();
            container.SetPermissions(new BlobContainerPermissions {PublicAccess = BlobContainerPublicAccessType.Blob});

            // Retrieve reference to a blob named "myblob".
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(blobName);

            //// Save blob contents to a file.
            //using (var fileStream = System.IO.File.OpenWrite(@"path\myfile"))
            //{
            //    blockBlob.DownloadToStream(fileStream);
            //}

            //// read blob contents as a string.
            //string text;
            //using (var memoryStream = new MemoryStream())
            //{
            //    blockBlob2.DownloadToStream(memoryStream);
            //    text = System.Text.Encoding.UTF8.GetString(memoryStream.ToArray());
            //}

            //// Create or overwrite the "myblob" blob with contents from a local file.
            //using (var fileStream = System.IO.File.OpenRead(@"path\myfile"))
            //{
            //    blockBlob.UploadFromStream(fileStream);
            //}

            //// Delete the blob.
            //blockBlob.Delete();

            blockBlob.UploadFromByteArray(bytes, 0, bytes.Length);
        }
    }
}