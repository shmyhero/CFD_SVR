using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CFD_API.Azure
{
    public class AzureFileDetails
    {
        public string Name { get; set; }
        public long Size { get; set; }
        public string ContentType { get; set; }
        public Uri Location { get; set; }

        public override string ToString()
        {
            return String.Format("({0} {1})", Name, Location);
        }
    }
}