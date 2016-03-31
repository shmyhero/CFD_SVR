using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CFD_API.DTO
{
    public class ResultDTO
    {
        public bool success { get; set; }
        public string message { get; set; }
    }

    //public class Result
    //{
    //    /// <summary>
    //    /// 请求失败返回的消息
    //    /// </summary>
    //    public string message { get; set; }

    //    /// <summary>
    //    /// 是否请求成功
    //    /// </summary>
    //    public bool success { get; set; }

    //    /// <summary>
    //    /// 请求成功返回的数据
    //    /// </summary>
    //    public object data { get; set; }
    //}

    //public class Result<T>
    //{
    //    /// <summary>
    //    /// 是否请求成功
    //    /// </summary>
    //    public bool success { get; set; }

    //    /// <summary>
    //    /// 请求成功返回的数据
    //    /// </summary>
    //    public T data { get; set; }

    //    public List<T> datas { get; set; }

    //    /// <summary>
    //    /// 请求失败返回的消息
    //    /// </summary>
    //    public string message { get; set; }
    //}
}