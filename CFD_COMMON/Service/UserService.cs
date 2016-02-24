using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;

namespace CFD_COMMON.Service
{
    public class UserService
    {
        public CFDEntities db { get; set; }

        public UserService(CFDEntities db)
        {
            this.db = db;
        }

        public void CreateUserByPhone(string phone)
        {
            //creating new user if phone doesn't exist in a new transaction
            using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, new TransactionOptions {IsolationLevel = IsolationLevel.Serializable}))
            {
                using (var dbIsolated = CFDEntities.Create())
                {
                    var userIsolated = dbIsolated.Users.FirstOrDefault(o => o.Phone == phone);
                    if (userIsolated == null)
                    {
                        userIsolated = new User
                        {
                            CreatedAt = DateTime.UtcNow,
                            Phone = phone,
                            Token = Guid.NewGuid().ToString("N")
                        };
                        dbIsolated.Users.Add(userIsolated);

                        dbIsolated.SaveChanges();
                        scope.Complete();
                    }
                }
            }
        }
    }
}