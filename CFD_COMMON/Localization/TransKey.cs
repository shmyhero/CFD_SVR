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
        PHONE_SIGNUP_FORBIDDEN,

        WECHAT_ALREADY_BOUND,
        WECHAT_OPENID_EXISTS,
        PHONE_ALREADY_BOUND,
        PHONE_EXISTS,

        PHONE_NOT_BOUND,

        ORDER_REJECTED,
        NO_AYONDO_ACCOUNT,
        EXCEPTION,

        OAUTH_LOGIN_REQUIRED,

        //USER_NOT_EXIST

        USERNAME_UNAVAILABLE,
        USERNAME_INVALID,

        LIVE_ACC_EXISTS,
        OCR_NO_TRANSACTION_ID,

        LIVE_ACC_REJ_RejectedMifid,
        LIVE_ACC_REJ_RejectedByDD,
        LIVE_ACC_REJ_RejectedDuplicate,
        LIVE_ACC_REJ_AbortedByExpiry,
        LIVE_ACC_REJ_AbortedByPolicy,

        NO_APPROVED_BANK_CARD,

        PRICEDOWN,

        Live_Acc_Not_Exist
    }
}
