using EasErpQuerySys.EasApplication;
using EasErpQuerySys.EasApplication.Dto;
using EasErpQuerySys.UpdateApplication;
using EasErpQuerySys.UpdateApplication.Dto;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Topshelf;

namespace EasErpQuerySys.JobScheduling
{
    class Program
    {
        static void Main(string[] args)
        {
            Run();
        }
        static void Run()
        {
            log4net.Config.XmlConfigurator.ConfigureAndWatch(new FileInfo(AppDomain.CurrentDomain.BaseDirectory + "log4net.config"));
            HostFactory.Run(x =>
            {
                x.UseLog4Net();
                x.Service<ServiceRunner>();
                x.SetDescription("Eas Erp 定时任务");
                x.SetDisplayName("EasErpQuerySys.JobScheduling");
                x.SetServiceName("EasErpQuerySys.JobScheduling");
                x.EnablePauseAndContinue();
            });
        }
    }
}
