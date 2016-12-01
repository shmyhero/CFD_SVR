using Microsoft.Owin.Hosting;
using System;
using System.Net.Http;

namespace CardService
{
    class Program
    {
        static void Main(string[] args)
        {
            StartOptions options = new StartOptions();
            //服务器Url设置
            options.Urls.Add("http://localhost:9000");
            //options.Urls.Add("http://192.168.88.253:9000");
            //options.Urls.Add("http://*:9000");


            WebApp.Start<Startup>(options);

            Console.ReadLine();
        }
    }
}
