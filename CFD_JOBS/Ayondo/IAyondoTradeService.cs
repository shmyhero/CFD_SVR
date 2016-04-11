using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace CFD_JOBS.Ayondo
{
    [ServiceContract]
    public interface IAyondoTradeService
    {
        [OperationContract]
        string Test(string text);
    }
}
