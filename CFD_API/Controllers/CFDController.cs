using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using CFD_COMMON.Models.Context;

namespace CFD_API.Controllers
{
    public class CFDController : ApiController
    {
        public CFDEntities db { get; protected set; }

        public CFDController(CFDEntities db)
        {
            this.db = db;
        }

        //public CFDController()
        //{
        //    db=CFDEntities.Create();
        //}
    }
}