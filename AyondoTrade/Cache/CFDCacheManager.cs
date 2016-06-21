using QuickFix.Fields;
using QuickFix.FIX44;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AyondoTrade
{
    internal sealed class CFDCacheManager
    {
        private static readonly Lazy<CFDCacheManager> instance = new Lazy<CFDCacheManager>(() => new CFDCacheManager());
        /// <summary>
        /// Key:Account
        /// Value:OpenPositions for an account
        /// </summary>
        private static ConcurrentDictionary<string, List<PositionReport>> openPositionList = new ConcurrentDictionary<string, List<PositionReport>>();
        /// <summary>
        /// Key:Account
        /// Value:ClosedPositions for an account
        /// </summary>
        private static ConcurrentDictionary<string, List<PositionReport>> closedPositionList = new ConcurrentDictionary<string, List<PositionReport>>();

        public static CFDCacheManager Instance { get { return instance.Value; } }

        private CFDCacheManager()
        {
        }
      
        public void UserLogin(string account)
        {
            Task.Factory.StartNew(
                () => {
                    if (!openPositionList.ContainsKey(account))
                    {
                        openPositionList.TryAdd(account, null);
                    }
                });
        }

        public void UserLogout(string account)
        {
            Task.Factory.StartNew(
                () => {
                    if (openPositionList.ContainsKey(account))
                    {
                        List<PositionReport> posList = null;
                        openPositionList.TryRemove(account, out posList);

                        closedPositionList.TryRemove(account, out posList);
                    }
                });
        }

        /// <summary>
        /// set open position list for given account
        /// </summary>
        public void SetOpenPositions(string account, IList<PositionReport> positions)
        {
            Task.Factory.StartNew(
                () => {
                    if (openPositionList.ContainsKey(account))
                    {
                        openPositionList[account] = positions.ToList();
                    }
                    else
                    {
                        openPositionList.TryAdd(account, positions.ToList());
                    }
                });
        }

        /// <summary>
        /// open a position. trigged by order filled
        /// </summary>
        /// <param name="account"></param>
        /// <param name="position"></param>
        public void OpenPosition(string account, PositionReport position)
        {
            Task.Factory.StartNew(
                () => {
                    PositionReport target = openPositionList[account].FirstOrDefault(item =>
                    item.PosMaintRptID.getValue() == position.PosMaintRptID.getValue());
                    if (target != null)
                    {
                        openPositionList[account].Remove(target);
                    }
                    openPositionList[account].Add(position);
                });
        }

        /// <summary>
        /// update a position by its Stop/Take Px
        /// </summary>
        /// <param name="account"></param>
        /// <param name="position"></param>
        public void UpdatePosition(string account, PositionReport position)
        {
            Task.Factory.StartNew(
                () => {
                    if (!openPositionList.ContainsKey(account))
                    {
                        return;
                    }

                    PositionReport posToUpdate = openPositionList[account].FirstOrDefault(item =>
                            item.PosMaintRptID.getValue() == position.PosMaintRptID.getValue());
                    if (posToUpdate != null) //update stop/take px order
                    {
                        if (position.Any(o => o.Key == Tags.StopPx))
                        {
                            posToUpdate.SetField(new DecimalField(Tags.StopPx) { Obj = position.GetDecimal(Tags.StopPx) });
                        }

                        if (position.Any(o => o.Key == Global.FixApp.TAG_TakePx))
                        {
                            posToUpdate.SetField(new DecimalField(Global.FixApp.TAG_TakePx) { Obj = position.GetDecimal(Global.FixApp.TAG_TakePx) });
                        }
                    }
                });
        }

        public void ClosePosition(string account, PositionReport closedPosition)
        {
            Task.Factory.StartNew(
                () => {
                    //if position not exist in open list, remove closed list. force closed list to refresh from Ayondo
                    if (!openPositionList.ContainsKey(account) || openPositionList[account] == null)
                    {
                        List<PositionReport> list = null;
                        closedPositionList.TryRemove(account, out list);
                        return;
                    }

                    //not found in open list - should not happen
                    if (!openPositionList[account].Any(item => item.PosMaintRptID.getValue() == closedPosition.PosMaintRptID.getValue()))
                    {
                        return;
                    }

                    //close list is empty
                    if (!closedPositionList.ContainsKey(account))
                    {
                        return;
                    }

                    PositionReport openPosition = openPositionList[account].FirstOrDefault(item => item.PosMaintRptID.getValue() == closedPosition.PosMaintRptID.getValue());
                    if (openPosition == null)
                        return;
                    //remove from open list
                    openPositionList[account].Remove(openPosition);
                    ////copy some value 
                    //position.SettlPrice = openPosition.SettlPrice;
                    //position.ClearingBusinessDate = openPosition.ClearingBusinessDate;
                    //if (openPosition.Any(o => o.Key == Tags.StopPx))
                    //{
                    //    position.SetField(new DecimalField(Tags.StopPx) { Obj = openPosition.GetDecimal(Tags.StopPx) });
                    //}

                    //if (openPosition.Any(o => o.Key == Global.FixApp.TAG_TakePx))
                    //{
                    //    position.SetField(new DecimalField(Global.FixApp.TAG_TakePx) { Obj = openPosition.GetDecimal(Global.FixApp.TAG_TakePx) });
                    //}
                    //if (openPosition.Any(o => o.Key == Global.FixApp.TAG_Leverage))
                    //{
                    //    position.SetField(new DecimalField(Global.FixApp.TAG_Leverage) { Obj = openPosition.GetDecimal(Global.FixApp.TAG_Leverage) });
                    //}
                    
                    //logic below is that Closed Position List should keep all position history, including open one and closed one.
                    //move open position to closed list
                    closedPositionList[account].Add(openPosition);
                    //add closed position to closed list
                    closedPositionList[account].Add(closedPosition);
                });
        }

        /// <summary>
        /// set closed position list for given account
        /// </summary>
        /// <param name="account"></param>
        /// <param name="positions"></param>
        public void SetClosedPositions(string account, IList<PositionReport> positions)
        {
            Task.Factory.StartNew(
                () => {
                    if (closedPositionList.ContainsKey(account))
                    {
                        closedPositionList[account] = positions.ToList();
                    }
                    else
                    {
                        closedPositionList.TryAdd(account, positions.ToList());
                    }
                });
        }
        
       

        public string PrintStatusHtml(string account)
        {
            StringBuilder sb = new StringBuilder();
            if (string.IsNullOrEmpty(account))
            {
                account = "139121848962";
            }

            if (openPositionList.ContainsKey(account))
            {
                sb.Append("<div>");
                sb.Append(string.Format("<span style='color:green; font-size:24px;'>{0} - Open Position List:</span><hr/>", account));
                sb.Append(GeneratePositionTable(openPositionList[account]));
                sb.Append("</div>");
            }

            if (closedPositionList.ContainsKey(account))
            {
                sb.Append("<div>");
                sb.Append(string.Format("<span style='color:green; font-size:24px;'>{0} - Closed Position List:</span><hr/>", account));
                sb.Append(GeneratePositionTable(closedPositionList[account]));
                sb.Append("</div>");
            }

            return sb.ToString();
        }

        public string PrintStatus(string account)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("------------------------Print Cache Start------------------------");
            var noPositionsGroup = new PositionMaintenanceRequest.NoPositionsGroup();

            if (string.IsNullOrEmpty(account))
            {
                foreach (KeyValuePair<string, List<PositionReport>> pair in openPositionList )
                {
                    sb.AppendLine("Account:" + pair.Key);
                    sb.AppendLine(string.Format("Open Positions ({0}):", pair.Value == null? 0 : pair.Value.Count));

                    if (pair.Value == null)
                        continue;

                    foreach (PositionReport pr in pair.Value)
                    {
                        pr.GetGroup(1, noPositionsGroup);
                        decimal? ShortQty = noPositionsGroup.Any(o => o.Key == Tags.ShortQty) ? noPositionsGroup.ShortQty.Obj : (decimal?)null;
                        decimal? LongQty = noPositionsGroup.Any(o => o.Key == Tags.LongQty) ? noPositionsGroup.LongQty.Obj : (decimal?)null;

                        decimal? StopPx = pr.Any(o => o.Key == Tags.StopPx) ? pr.GetDecimal(Tags.StopPx) : (decimal?)null;
                        decimal? TakePx = pr.Any(o => o.Key == Global.FixApp.TAG_TakePx) ? pr.GetDecimal(Global.FixApp.TAG_TakePx) : (decimal?)null;
                        decimal? Leverage = pr.Any(o => o.Key == Global.FixApp.TAG_Leverage) ? pr.GetDecimal(Global.FixApp.TAG_Leverage) : (decimal?)null;

                        string Text = pr.Text.Obj;
                        sb.AppendLine(string.Format("Account:{0};PosMaintID:{6}; SettlePrice:{1};LongQty:{2};ShortQty{3};StopPx:{4};TakePx{5}",
                            pr.Account, pr.SettlPrice, LongQty, ShortQty, StopPx, TakePx,pr.PosMaintRptID));
                    }
                }
                foreach (KeyValuePair<string, List<PositionReport>> pair in closedPositionList)
                {
                    sb.AppendLine("Account:" + pair.Key);
                    sb.AppendLine(string.Format("Closed Positions ({0}):", pair.Value == null ? 0 : pair.Value.Count));

                    if (pair.Value == null)
                        continue;

                    foreach (PositionReport pr in pair.Value)
                    {
                        pr.GetGroup(1, noPositionsGroup);
                        decimal? ShortQty = noPositionsGroup.Any(o => o.Key == Tags.ShortQty) ? noPositionsGroup.ShortQty.Obj : (decimal?)null;
                        decimal? LongQty = noPositionsGroup.Any(o => o.Key == Tags.LongQty) ? noPositionsGroup.LongQty.Obj : (decimal?)null;

                        decimal? StopPx = pr.Any(o => o.Key == Tags.StopPx) ? pr.GetDecimal(Tags.StopPx) : (decimal?)null;
                        decimal? TakePx = pr.Any(o => o.Key == Global.FixApp.TAG_TakePx) ? pr.GetDecimal(Global.FixApp.TAG_TakePx) : (decimal?)null;
                        decimal? Leverage = pr.Any(o => o.Key == Global.FixApp.TAG_Leverage) ? pr.GetDecimal(Global.FixApp.TAG_Leverage) : (decimal?)null;
                        string Text = pr.Text.Obj;
                        sb.AppendLine(string.Format("Account:{0};SettlePrice:{1};LongQty:{2};ShortQty{3};StopPx:{4};TakePx{5}",
                            pr.Account, pr.SettlPrice, LongQty, ShortQty, StopPx, TakePx));
                    }
                }

                sb.AppendLine("------------------------Print Cache End------------------------");
            }
            else
            {
                sb.AppendLine("------------------------Print Cache Start------------------------");
                if (openPositionList.ContainsKey(account) && openPositionList[account] != null)
                {
                    sb.AppendLine(string.Format("Open Positions ({0}):", openPositionList[account] == null ? 0 : openPositionList[account].Count));
                    foreach (PositionReport pr in openPositionList[account])
                    {
                        pr.GetGroup(1, noPositionsGroup);
                        decimal? ShortQty = noPositionsGroup.Any(o => o.Key == Tags.ShortQty) ? noPositionsGroup.ShortQty.Obj : (decimal?)null;
                        decimal? LongQty = noPositionsGroup.Any(o => o.Key == Tags.LongQty) ? noPositionsGroup.LongQty.Obj : (decimal?)null;

                        decimal? StopPx = pr.Any(o => o.Key == Tags.StopPx) ? pr.GetDecimal(Tags.StopPx) : (decimal?)null;
                        decimal? TakePx = pr.Any(o => o.Key == Global.FixApp.TAG_TakePx) ? pr.GetDecimal(Global.FixApp.TAG_TakePx) : (decimal?)null;
                        decimal? Leverage = pr.Any(o => o.Key == Global.FixApp.TAG_Leverage) ? pr.GetDecimal(Global.FixApp.TAG_Leverage) : (decimal?)null;
                        string Text = pr.Text.Obj;
                        sb.AppendLine(string.Format("Account:{0};SettlePrice:{1};LongQty:{2};ShortQty{3};StopPx:{4};TakePx{5}",
                            pr.Account, pr.SettlPrice, LongQty, ShortQty, StopPx, TakePx));
                    }
                }
                else
                {
                    sb.AppendLine("Open Positions (0)");
                }

                if (closedPositionList.ContainsKey(account) && closedPositionList[account] != null)
                {
                    sb.AppendLine(string.Format("Closed Positions ({0}):", closedPositionList[account] == null ? 0 : closedPositionList[account].Count));
                    foreach (PositionReport pr in openPositionList[account])
                    {
                        pr.GetGroup(1, noPositionsGroup);
                        decimal? ShortQty = noPositionsGroup.Any(o => o.Key == Tags.ShortQty) ? noPositionsGroup.ShortQty.Obj : (decimal?)null;
                        decimal? LongQty = noPositionsGroup.Any(o => o.Key == Tags.LongQty) ? noPositionsGroup.LongQty.Obj : (decimal?)null;

                        decimal? StopPx = pr.Any(o => o.Key == Tags.StopPx) ? pr.GetDecimal(Tags.StopPx) : (decimal?)null;
                        decimal? TakePx = pr.Any(o => o.Key == Global.FixApp.TAG_TakePx) ? pr.GetDecimal(Global.FixApp.TAG_TakePx) : (decimal?)null;
                        decimal? Leverage = pr.Any(o => o.Key == Global.FixApp.TAG_Leverage) ? pr.GetDecimal(Global.FixApp.TAG_Leverage) : (decimal?)null;
                        string Text = pr.Text.Obj;
                        sb.AppendLine(string.Format("Account:{0};SettlePrice:{1};LongQty:{2};ShortQty{3};StopPx:{4};TakePx{5}",
                            pr.Account, pr.SettlPrice, LongQty, ShortQty, StopPx, TakePx));
                    }
                }
                else
                {
                    sb.AppendLine("Closed Positions (0)");
                }

                sb.AppendLine("------------------------Print Cache End------------------------");
            }

            return sb.ToString();
        }

        private string GeneratePositionTable(List<PositionReport> posList)
        {
            StringBuilder sb = new StringBuilder();
            //sb.Append("<table><tr><th>PosMaintRptID</th><th>SettlPrice</th></tr>");
            //foreach (PositionReport pos in posList)
            //{
            //    sb.Append("<tr>");
            //    sb.Append(string.Format("<td>{0}</td>", pos.PosMaintRptID));
            //    sb.Append(string.Format("<td>{0}</td>", pos.SettlPrice));
            //    sb.Append("</tr>");
            //}
            //sb.Append("</table>");
            if (posList == null)
                return string.Empty;

            foreach (PositionReport pos in posList)
            {
                sb.Append("<div>");
                sb.Append(Global.FixApp.GetMessageString(pos).Replace("\r\n", "<br/>")); 
                sb.Append("</div>");
            }

            return sb.ToString();
        }
    }
}
