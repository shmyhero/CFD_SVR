using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace CFD_COMMON.Models.Context
{
    public partial class CFDEntities
    {
        public CFDEntities(string connectionString)
            : base(connectionString)
        {
        }

        public static CFDEntities Create(bool log = false, bool UseDatabaseNullSemantics = true)
        {
            string connectionString = GetDbConnectionString("CFDEntities");
            var db = new CFDEntities(connectionString);

            db.Configuration.UseDatabaseNullSemantics = UseDatabaseNullSemantics;

            if (log || Debugger.IsAttached)
                db.Database.Log = s => Trace.WriteLine(s);

            //if (log)
            //    Global.LogLine("created object-context for main DB [" + db.Database.Connection.Database + "]");

            return db;
        }

        public static string GetDbConnectionString(string connectStringName)
        {
            if (RoleEnvironment.IsAvailable)
            {
                string value = null;
                try
                {
                    value = RoleEnvironment.GetConfigurationSettingValue(connectStringName);
                }
                catch (Exception e)
                {
                }

                //if there's no cloud config, return local config
                return value ?? ConfigurationManager.ConnectionStrings[connectStringName].ConnectionString;
            }
            else
            {
                return ConfigurationManager.ConnectionStrings[connectStringName].ConnectionString;
            }
        }
    }
}