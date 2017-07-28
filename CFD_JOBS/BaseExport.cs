using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.IO;
using System.Text;

namespace CFD_JOBS
{
    abstract class BaseExport
    {
        public List<ExportItem> RemittanceExportItems;
        public abstract void ExportRemittance(string fileName);

        public List<RewardExportItem> DepositRewardExportItems;
        public abstract void ExportDepositReward(string fileName);

        public List<PartnerExportItem> PartnerExportItems;
        public abstract void ExportPartner(string fileName);
    }

    class CSVExport : BaseExport
    {
        public override void ExportRemittance(string fileName)
        {
            if (RemittanceExportItems == null)
            {
                throw new Exception("列为空");
            }

            StringBuilder sb = new StringBuilder();
            //加标题
            sb.Append("Beneficiary Name,Beneficiary Account No.,Bank Name,Bank Branch,Province,City,ID Card No.,Currency,Transaction Amount Received\n");
            RemittanceExportItems.ForEach(item => {
                sb.Append(item.BeneficiaryName);
                sb.Append(",");
                sb.Append(item.BeneficiaryAccountNo);
                sb.Append(",");
                sb.Append(item.BankName);
                sb.Append(",");
                sb.Append(item.BankBranch);
                sb.Append(",");
                sb.Append(item.Province);
                sb.Append(",");
                sb.Append(item.City);
                sb.Append(",");
                sb.Append(item.IdCardNo);
                sb.Append(",");
                sb.Append(item.Currency);
                sb.Append(",");
                sb.Append(item.Amount);
                sb.Append("\n");
            });

            File.WriteAllText(fileName, sb.ToString());
        }

        public override void ExportDepositReward(string fileName)
        {
            throw new NotImplementedException();
        }

        public override void ExportPartner(string fileName)
        {
        }
    }

    class ExcelExport : BaseExport
    {
        public override void ExportRemittance(string fileName)
        {
            if (RemittanceExportItems == null)
            {
                throw new Exception("列为空");
            }
            //把模板copy一份
            var templateBytes = File.ReadAllBytes("Template/WeCollect_Remittance_Template.xls");
            File.WriteAllBytes(fileName, templateBytes);

            String sConnectionString = string.Format("Provider=Microsoft.Jet.OLEDB.4.0;Data Source={0};Extended Properties=Excel 8.0;", fileName);
            using (OleDbConnection oleConn = new OleDbConnection(sConnectionString))
            {
                oleConn.Open();
                using (OleDbCommand ole_cmd = oleConn.CreateCommand())
                {
                    RemittanceExportItems.ForEach(item =>
                    {
                        ole_cmd.CommandText = string.Format("insert into [Sheet1$] values('{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}','{8}','{9}')", item.BeneficiaryName, item.UserName, item.BeneficiaryAccountNo, item.BankName, item.BankBranch, item.Province, item.City, item.IdCardNo, item.Currency, item.Amount);
                        ole_cmd.ExecuteNonQuery();
                    });
                }
            }
        }

        public override void ExportDepositReward(string fileName)
        {
            if (DepositRewardExportItems == null)
            {
                throw new Exception("列为空");
            }
            //把模板copy一份
            var templateBytes = File.ReadAllBytes("Template/Deposit_Reward_Template.xls");
            File.WriteAllBytes(fileName, templateBytes);

            String sConnectionString = string.Format("Provider=Microsoft.Jet.OLEDB.4.0;Data Source={0};Extended Properties=Excel 8.0;", fileName);
            using (OleDbConnection oleConn = new OleDbConnection(sConnectionString))
            {
                oleConn.Open();
                using (OleDbCommand ole_cmd = oleConn.CreateCommand())
                {
                    DepositRewardExportItems.ForEach(item =>
                    {
                        ole_cmd.CommandText = string.Format("insert into [Sheet1$] values('{0}','{1}','{2}','{3}','{4}')", item.AccountName, item.RealName, item.DepositAmount, item.RewardAmount, item.DepositAt.ToString("yyyy-MM-dd HH:mm:ss"));
                        ole_cmd.ExecuteNonQuery();
                    });
                }
            }
        }

        public override void ExportPartner(string fileName)
        {
            if (PartnerExportItems == null)
            {
                throw new Exception("列为空");
            }

            if (PartnerExportItems == null || PartnerExportItems.Count == 0)
                return;

            //把模板copy一份
            var templateBytes = File.ReadAllBytes("Template/Partner_Template.xls");
            File.WriteAllBytes(fileName, templateBytes);

            String sConnectionString = string.Format("Provider=Microsoft.Jet.OLEDB.4.0;Data Source={0};Extended Properties=Excel 8.0;", fileName);
            using (OleDbConnection oleConn = new OleDbConnection(sConnectionString))
            {
                oleConn.Open();
                using (OleDbCommand ole_cmd = oleConn.CreateCommand())
                {
                    PartnerExportItems.ForEach(item =>
                    {
                        ole_cmd.CommandText = string.Format("insert into [Sheet1$] values('{0}','{1}','{2}','{3}')", item.PartnerCode, item.Name, item.Email, item.Phone);
                        ole_cmd.ExecuteNonQuery();
                    });
                }
            }
        }
    }

    class ExportItem
    {
        //持卡人
        public string BeneficiaryName;
        public string UserName;
        public string BeneficiaryAccountNo;
        public string BankName;
        public string BankBranch;
        public string Province;
        public string City;
        public string IdCardNo;
        public string Currency { get { return "USD"; } }
        public decimal Amount;
    }

    class RewardExportItem
    {
        /// <summary>
        /// 实盘账户名
        /// </summary>
        public string AccountName;
        /// <summary>
        /// 真实姓名
        /// </summary>
        public string RealName;
        /// <summary>
        /// 入金金额
        /// </summary>
        public decimal DepositAmount;
        /// <summary>
        /// 奖励金额
        /// </summary>
        public decimal RewardAmount;
        /// <summary>
        /// 入金时间
        /// </summary>
        public DateTime DepositAt;
    }

    class PartnerExportItem
    {
        public string PartnerCode;
        public string Name;
        public string Email;
        public string Phone;
    }
}
