using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CFD_COMMON;
using QuickFix;
using QuickFix.Fields;

namespace CFD_JOBS.Ayondo
{
    class AyondoFixTestApp : IApplication
    {
        public void ToAdmin(Message message, SessionID sessionID)
        {
            if (message.Header.GetString(Tags.MsgType) == MsgType.LOGON)
            {
                message.SetField(new Username("test"));
                message.SetField(new Password("password"));
            }

            CFDGlobal.LogLine("ToAdmin: "+message.ToString());
        }

        public void FromAdmin(Message message, SessionID sessionID)
        {
            CFDGlobal.LogLine("FromAdmin: " + message.ToString());
        }

        public void ToApp(Message message, SessionID sessionId)
        {
            CFDGlobal.LogLine("ToApp: " + message.ToString());
        }

        public void FromApp(Message message, SessionID sessionID)
        {
            CFDGlobal.LogLine("FromApp: " + message.ToString());
        }

        public void OnCreate(SessionID sessionID)
        {
            CFDGlobal.LogLine("OnCreate: " + sessionID.ToString());
        }

        public void OnLogout(SessionID sessionID)
        {
            CFDGlobal.LogLine("OnLogout: " + sessionID.ToString());
        }

        public void OnLogon(SessionID sessionID)
        {
            CFDGlobal.LogLine("OnLogon: " + sessionID.ToString());
        }
    }
}
