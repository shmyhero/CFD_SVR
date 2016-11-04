using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CFD_JOBS_WinApp.Interface
{
    /// <summary>
    /// 所有Job必须要继承的接口
    /// </summary>
    public interface ICFDJob
    {
        void Run();

        void Stop();
    }
}
