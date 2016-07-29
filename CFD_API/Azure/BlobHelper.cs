using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CFD_API.Azure
{
    public class BlobHelper
    {
        public static CloudBlobContainer GetWebApiContainer(string containerReference, out CloudBlobClient blobClient)
        {
            // Retrieve storage account from connection-string
            // from Web.Config
            var storageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("StorageConnectionString"));

            // Create the blob client 
            blobClient = storageAccount.CreateCloudBlobClient();

            // Container name must use lower case, @see http://msdn.microsoft.com/en-us/library/windowsazure/dd135715.aspx
            var container = blobClient.GetContainerReference(containerReference);
            container.CreateIfNotExists();

            // Enable public access to blob
            var permissions = container.GetPermissions();
            switch (permissions.PublicAccess)
            {
                case BlobContainerPublicAccessType.Off:
                    permissions.PublicAccess = BlobContainerPublicAccessType.Blob;
                    container.SetPermissions(permissions);
                    break;
            }

            return container;
        }
    }
}