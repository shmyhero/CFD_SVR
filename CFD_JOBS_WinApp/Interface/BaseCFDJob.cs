using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CFD_JOBS_WinApp.Interface
{
    public abstract class BaseCFDJob : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public string JobName { get; set; }

        private DateTime createdAt;
        public DateTime CreatedAt {
            get {
                return createdAt;
            }
            set {
                createdAt = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CreatedAt"));
            }
        }

        private string exceptionMessage;
        public string ExceptionMessage { get {
                return exceptionMessage;
            }
            set {
                exceptionMessage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ExceptionMessage"));
            }
        }

        protected List<string> logInfoItems = new List<string>();

        private string logInfo;
        public string LogInfo {
            get { return logInfo; }
            set { logInfo = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("LogInfo")); }
        }

        private bool isRunning = false;
        public bool IsRunning {
            get { return isRunning; }
            set { isRunning = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsRunning")); }
        }
    }
}
