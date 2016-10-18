using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CFD_COMMON.Localization
{
    public enum TransKey
    {
        INVALID_PHONE_NUMBER,
        INVALID_VERIFY_CODE,
        NICKNAME_EXISTS,
        NICKNAME_TOO_LONG,
        ORDER_REJECTED,
        NO_AYONDO_ACCOUNT,
        EXCEPTION,
        PHONE_SIGNUP_FORBIDDEN,
        //USER_NOT_EXIST

        OAUTH_LOGIN_REQUIRED,

        WECHAT_ALREADY_BOUND,
        WECHAT_OPENID_EXISTS,
        PHONE_ALREADY_BOUND,
        PHONE_EXISTS,

        USERNAME_UNAVAILABLE,
        USERNAME_INVALID,

        LIVE_ACC_REJ_RejectedMifid,
        LIVE_ACC_REJ_RejectedByDD,
        LIVE_ACC_REJ_AbortedByExpiry,
        LIVE_ACC_REJ_AbortedByPolicy,

        PRICEDOWN
    }
}
