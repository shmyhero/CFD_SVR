using CFD_COMMON;
using QuickFix.Fields;
using QuickFix.FIX44;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Caching;
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

        private static ObjectCache cfdCache = MemoryCache.Default;
        //所有Cache都在两小时后过期
        private static int absoluteExpiration = 7200;
        
        private const string openPositionCachePrefix = "OpenPosition_";
        private const string closedPositionCachePrefix = "ClosedPosition_";
        private const string balancePrefix = "Balance_";

        private static bool isEnabled = true;

        public static CFDCacheManager Instance { get { return instance.Value; } }

        private CFDCacheManager()
        {
        }

        //public void UserLogin(string account)
        //{
        //    Task.Factory.StartNew(
        //        () => {
        //            CFDGlobal.LogLine(string.Format("Cache - UserLogin ({0})", account));

        //            //if (!openPositionList.ContainsKey(account))
        //            //{
        //            //    CFDGlobal.LogLine(string.Format("Cache - Account ({0}) added into open position list", account));

        //            //    openPositionList.TryAdd(account, null);
        //            //}
        //            //if (!cfdCache.Contains(openPositionCachePrefix + account))
        //            //{
        //            //    CFDGlobal.LogLine(string.Format("Cache - Account ({0}) added into open position list", account));
        //            //    cfdCache.Set(openPositionCachePrefix + account, new List<PositionReport>(), new CacheItemPolicy() { AbsoluteExpiration = DateTime.Now.AddSeconds(absoluteExpiration) });
        //            //    CFDGlobal.LogLine(string.Format("Cache - Account ({0}) added finished", account));
        //            //}
        //        });
        //}

        public void UserLogout(string account)
        {
            Task.Factory.StartNew(
                () => {
                    CFDGlobal.LogLine(string.Format("Cache - UserLogout ({0})", account));

                    //if (openPositionList.ContainsKey(account))
                    //{
                    //    CFDGlobal.LogLine(string.Format("Cache - Account ({0}) removed from open/closed position list", account));

                    //    List<PositionReport> posList = null;
                    //    openPositionList.TryRemove(account, out posList);

                    //    closedPositionList.TryRemove(account, out posList);

                    //    decimal? balance = 0;
                    //    balanceList.TryRemove(account, out balance);
                    //}
                    if (cfdCache.Contains(openPositionCachePrefix + account))
                    {
                        CFDGlobal.LogLine(string.Format("Cache - Account ({0}) removed from open/closed position list", account));

                        cfdCache.Remove(openPositionCachePrefix + account);

                        cfdCache.Remove(closedPositionCachePrefix + account);

                        cfdCache.Remove(balancePrefix + account);
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
                    
                    //if (openPositionList.ContainsKey(account))
                    //{
                    //    openPositionList[account] = positions.ToList();
                    //}
                    //else
                    //{
                    //    openPositionList.TryAdd(account, positions.ToList());
                    //}
                    try
                    {
                        CFDGlobal.LogLine(string.Format("Cache - Account ({0}) Set Open PositionReport, Pos Count:{1}", account, positions == null ? 0 : positions.Count));
                        cfdCache.Set(openPositionCachePrefix + account, positions, new CacheItemPolicy() { AbsoluteExpiration = DateTime.Now.AddSeconds(absoluteExpiration) });
                        CFDGlobal.LogLine(string.Format("Cache - Account ({0}) Set Open PositionReport, finished", account, positions == null ? 0 : positions.Count));
                    }
                    catch(Exception ex)
                    {
                        CFDGlobal.LogLine(string.Format("Cache - Account ({0}) Set Open PositionReport, exception: {1}", account, ex.Message));
                    }


                });
        }

        public IList<PositionReport> GetOpenPosition(string account)
        {
         
            if (!isEnabled)
            {
                CFDGlobal.LogLine(string.Format("Cache - Account ({0}) Get Open Position, but cache disabled", account));
                return null;
            }

            
            //if (openPositionList.ContainsKey(account))
            //{
            //    return openPositionList[account];
            //}
            if (cfdCache.Contains(openPositionCachePrefix + account))
            {
                var positions = cfdCache[openPositionCachePrefix + account] as IList<PositionReport>;
                if (positions == null)
                {
                    CFDGlobal.LogLine(string.Format("Cache - Account ({0}) Get Open PositionReport, Pos Count:0", account));

                    return null;
                }
                else
                {
                    return positions;
                }
            }
            else
            {
                CFDGlobal.LogLine(string.Format("Cache - Account ({0}) Get Open PositionReport, Not exist in cache", account));
            }

            return null;
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
                    CFDGlobal.LogLine(string.Format("Cache - Account ({0}) received a new Position", account));

                    //PositionReport target = openPositionList[account].FirstOrDefault(item =>
                    //item.PosMaintRptID.getValue() == position.PosMaintRptID.getValue());
                    //if (target != null)
                    //{
                    //    openPositionList[account].Remove(target);
                    //}
                    //openPositionList[account].Add(position);
                    if (!cfdCache.Contains(openPositionCachePrefix + account))
                        return;

                    if (cfdCache[openPositionCachePrefix + account] == null)
                        return;

                    var positionList = cfdCache[openPositionCachePrefix + account] as IList<PositionReport>;
                    PositionReport target = positionList.FirstOrDefault(item =>
                    item.PosMaintRptID.getValue() == position.PosMaintRptID.getValue());
                    if (target != null)
                    {
                        positionList.Remove(target);
                    }
                    positionList.Add(position);
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
                    CFDGlobal.LogLine(string.Format("Cache - Account ({0}) received an updated Position", account));

                    //if (!openPositionList.ContainsKey(account))
                    //{
                    //    return;
                    //}

                    //PositionReport posToUpdate = openPositionList[account].FirstOrDefault(item =>
                    //        item.PosMaintRptID.getValue() == position.PosMaintRptID.getValue());
                    //if (posToUpdate != null) //update stop/take px order. at current stage, replace whole position
                    //{
                    //    int index = openPositionList[account].IndexOf(posToUpdate);
                    //    openPositionList[account].RemoveAt(index);
                    //    openPositionList[account].Insert(index, position);
                    //    //if (position.Any(o => o.Key == Tags.StopPx))
                    //    //{
                    //    //    posToUpdate.SetField(new DecimalField(Tags.StopPx) { Obj = position.GetDecimal(Tags.StopPx) });
                    //    //}

                    //    //if (position.Any(o => o.Key == Global.FixApp.TAG_TakePx))
                    //    //{
                    //    //    posToUpdate.SetField(new DecimalField(Global.FixApp.TAG_TakePx) { Obj = position.GetDecimal(Global.FixApp.TAG_TakePx) });
                    //    //}
                    //}

                    if (!cfdCache.Contains(openPositionCachePrefix + account))
                    {
                        return;
                    }

                    if (cfdCache[openPositionCachePrefix + account] == null)
                    {
                        return;
                    }

                    var positionList = cfdCache[openPositionCachePrefix + account] as IList<PositionReport>;

                    PositionReport posToUpdate = positionList.FirstOrDefault(item =>
                            item.PosMaintRptID.getValue() == position.PosMaintRptID.getValue());
                    if (posToUpdate != null) //update stop/take px order. at current stage, replace whole position
                    {
                        int index = positionList.IndexOf(posToUpdate);
                        positionList.RemoveAt(index);
                        positionList.Insert(index, position);
                    }
                });
        }

        public void ClosePosition(string account, PositionReport closedPosition)
        {
            Task.Factory.StartNew(
                () => {
                    CFDGlobal.LogLine(string.Format("Cache - Account ({0}) closed a Position", account));

                    ////if position not exist in open list, remove closed list. force closed list to refresh from Ayondo
                    //if (!openPositionList.ContainsKey(account) || openPositionList[account] == null)
                    //{
                    //    List<PositionReport> list = null;
                    //    closedPositionList.TryRemove(account, out list);
                    //    return;
                    //}

                    ////not found in open list - should not happen
                    //if (!openPositionList[account].Any(item => item.PosMaintRptID.getValue() == closedPosition.PosMaintRptID.getValue()))
                    //{
                    //    return;
                    //}

                    //PositionReport openPosition = openPositionList[account].FirstOrDefault(item => item.PosMaintRptID.getValue() == closedPosition.PosMaintRptID.getValue());
                    //if (openPosition == null)
                    //{
                    //    return;
                    //}

                    ////remove from open list
                    //openPositionList[account].Remove(openPosition);

                    ////close list is not empty
                    //if (closedPositionList.ContainsKey(account))
                    //{
                    //    closedPositionList[account].Add(openPosition);
                    //    //add closed position to closed list
                    //    closedPositionList[account].Add(closedPosition);
                    //}

                    //if position not exist in open list, remove closed list. force closed list to refresh from Ayondo
                    if (!cfdCache.Contains(openPositionCachePrefix + account) || cfdCache[openPositionCachePrefix + account] == null)
                    {
                        cfdCache.Remove(closedPositionCachePrefix + account);
                        return;
                    }

                    //not found in open list - should not happen
                    List<PositionReport> openList = cfdCache[openPositionCachePrefix + account] as List<PositionReport>;
                    if (!openList.Any(item => item.PosMaintRptID.getValue() == closedPosition.PosMaintRptID.getValue()))
                    {
                        return;
                    }

                    PositionReport openPosition = openList.FirstOrDefault(item => item.PosMaintRptID.getValue() == closedPosition.PosMaintRptID.getValue());
                    if (openPosition == null)
                    {
                        return;
                    }

                    //remove from open list
                    openList.Remove(openPosition);

                    //close list is not empty
                    if (cfdCache.Contains(closedPositionCachePrefix + account))
                    {
                        List<PositionReport> closedList = cfdCache[closedPositionCachePrefix + account] as List<PositionReport>;
                        closedList.Add(openPosition);
                        //add closed position to closed list
                        closedList.Add(closedPosition);
                    }
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
                    CFDGlobal.LogLine(string.Format("Cache - Account ({0}) query Closed Position List", account));

                    //if (closedPositionList.ContainsKey(account))
                    //{
                    //    closedPositionList[account] = positions.ToList();
                    //}
                    //else
                    //{
                    //    closedPositionList.TryAdd(account, positions.ToList());
                    //}

                    cfdCache.Set(closedPositionCachePrefix + account, positions, new CacheItemPolicy() { AbsoluteExpiration = DateTime.Now.AddSeconds(absoluteExpiration) });
                });
        }

        public IList<PositionReport> GetClosedPosition(string account)
        {
            if (!isEnabled)
                return null;

            //if (closedPositionList.ContainsKey(account))
            //{
            //    return closedPositionList[account];
            //}

            if (cfdCache.Contains(closedPositionCachePrefix + account))
            {
                return cfdCache[closedPositionCachePrefix + account] as IList<PositionReport>;
            }

            return null;
        }

        public void SetBalance(string account, decimal? balance)
        {
            Task.Factory.StartNew(
                () => {
                    CFDGlobal.LogLine(string.Format("Cache - Account ({0}) set balance: {1}", account, balance));

                    //if (balanceList.ContainsKey(account))
                    //{
                    //    balanceList[account] = balance;
                    //}
                    //else
                    //{
                    //    balanceList.TryAdd(account, balance);
                    //}

                    cfdCache.Set(balancePrefix + account, balance, new CacheItemPolicy() { AbsoluteExpiration = DateTime.Now.AddSeconds(absoluteExpiration) });
                });
        }

        public bool TryGetBalance(string account, out decimal balance)
        {
            balance = default(decimal);

            if (!isEnabled)
                return false;

            //if (balanceList.ContainsKey(account))
            //{
            //    balance = balanceList[account].HasValue? balanceList[account].Value : default(decimal);
            //    return true;
            //}
            //else
            //{
            //    return false;
            //}
            if (cfdCache.Contains(balancePrefix + account))
            {
                balance = (decimal)cfdCache[balancePrefix + account];
                return true;
            }
            else
            {
                return false;
            }
        }

        public void SwitchCache(bool mode)
        {
            isEnabled = mode;
        }

        /// <summary>
        /// Clear cache by account
        /// </summary>
        /// <param name="account"></param>
        public void ClearCache(string account)
        {
            //List<PositionReport> list = null;
            //if(openPositionList.ContainsKey(account))
            //{
            //    openPositionList.TryRemove(account, out list);
            //}

            //if(closedPositionList.ContainsKey(account))
            //{
            //    closedPositionList.TryRemove(account, out list);
            //}

            //decimal? balance = 0;
            //if(balanceList.ContainsKey(account))
            //{
            //    balanceList.TryRemove(account, out balance);
            //}
            if (cfdCache.Contains(openPositionCachePrefix + account))
            {
                cfdCache.Remove(openPositionCachePrefix + account);
            }

            if (cfdCache.Contains(closedPositionCachePrefix + account))
            {
                cfdCache.Remove(closedPositionCachePrefix + account);
            }

            if (cfdCache.Contains(balancePrefix + account))
            {
                cfdCache.Remove(balancePrefix + account);
            }
        }

        /// <summary>
        /// Clear whole cache
        /// </summary>
        public void ClearCache()
        {
            var allKeys = cfdCache.Select(c => c.Key).ToList();
            allKeys.ForEach(key =>
            {
                cfdCache.Remove(key);
            });
        }

        public string PrintStatusHtml(string account, string userName)
        {
            StringBuilder sb = new StringBuilder();
            if (string.IsNullOrEmpty(account))
            {
                account = "139121848962";
            }

            if (cfdCache.Contains(balancePrefix + account))
            {
                CFDGlobal.LogLine(string.Format("Cache - Account ({0}) Print Balance", account));

                sb.Append("<pre>");
                sb.Append(string.Format("<span style='color:green; font-size:24px;'>{0} - Balance:{1}</span><hr/>", userName, (decimal)cfdCache[balancePrefix + account]));
                sb.Append("</pre>");
            }

            if (cfdCache.Contains(openPositionCachePrefix + account))
            {
                CFDGlobal.LogLine(string.Format("Cache - Account ({0}) Print Open Position", account));

                sb.Append("<pre>");
                sb.Append(string.Format("<span style='color:green; font-size:24px;'>{0} - Open Position List:</span><hr/>", userName));
                sb.Append(GeneratePositionTable(cfdCache[openPositionCachePrefix + account] as List<PositionReport>));
                sb.Append("</pre>");
            }

            if (cfdCache.Contains(closedPositionCachePrefix + account))
            {
                CFDGlobal.LogLine(string.Format("Cache - Account ({0}) Print Closed Position", account));

                sb.Append("<pre>");
                sb.Append(string.Format("<span style='color:green; font-size:24px;'>{0} - Closed Position List:</span><hr/>", userName));
                sb.Append(GeneratePositionTable(cfdCache[closedPositionCachePrefix + account] as List<PositionReport>));
                sb.Append("</pre>");
            }

            return sb.ToString();
        }

        private string GeneratePositionTable(List<PositionReport> posList)
        {
            StringBuilder sb = new StringBuilder();
            if (posList == null)
                return string.Empty;

            foreach (PositionReport pos in posList)
            {
                sb.Append("<pre>");
                sb.Append(Global.FixApp.GetMessageString(pos).Replace("\r\n", "<br/>"));
                sb.Append("</pre>");
            }

            return sb.ToString();
        }
    }
}
