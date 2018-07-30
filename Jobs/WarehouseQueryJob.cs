using EasErpQuerySys.EasApplication;
using EasErpQuerySys.EasApplication.Dto;
using EasErpQuerySys.UpdateApplication;
using log4net;
using Newtonsoft.Json;
using Quartz;
using System;
using System.Configuration;
using System.Linq;
using System.Xml.Linq;

namespace EasErpQuerySys.JobScheduling.Jobs
{
    public sealed class WarehouseQueryJob : IJob
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(WarehouseQueryJob));
        private readonly IEasAppService _easAppService = new EasAppService();
        private readonly IUpdateAppService _updateAppService = new UpdateAppService();
        private int _requestCount = 0;
        private int _maximumNumOfRequests = string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["MaximumNumOfRequests"])
            ? 10 : int.Parse(ConfigurationManager.AppSettings["MaximumNumOfRequests"]);
        public void Execute(IJobExecutionContext context)
        {
            _logger.InfoFormat("仓库任务:" + "开始");
            MailJob();
        }

        private void MailJob()
        {
            _requestCount++;
            try
            {
                var input = GetInputFromXml();
                WarehouseQueryOutput queryResult = _easAppService.WarehouseQuery(input);
                _logger.InfoFormat("仓库任务:" + "请求数据成功");

                queryResult.WarehouseList.ForEach(t =>
               {
                   try
                   {
                       UpdatePurchaseOrderInput(t);
                       UpdatePostRequisitionInput(t);
                       var tt = _updateAppService.UpdateWarehouseQuery(JsonConvert.SerializeObject(t));
                       _logger.InfoFormat("仓库任务:" + "上传数据成功，本次任务结束");
                   }
                   catch (Exception e)
                   {
                       throw new EasWebServiceException("update_error", e.Message);
                   }
               });
            }
            catch (EasWebServiceException e)
            {
                var result = e.GetExceptionResult();
                if (result.resultStatus == "fail")
                {
                    _logger.InfoFormat("仓库任务:" + "请求数据失败，" + result.resultMsg);
                    if (_requestCount < _maximumNumOfRequests)
                    {
                        MailJob();
                    }
                    else
                    {
                        _logger.InfoFormat("仓库任务:" + "请求次数达到上限" + _maximumNumOfRequests + "，本次任务结束");
                    }
                }
                else if (result.resultStatus == "update_error")
                {
                    _logger.InfoFormat("仓库任务:" + "上传数据失败，" + result.resultMsg);
                }
                else
                {
                    _logger.InfoFormat("仓库任务:" + "请求数据失败，" + result.resultMsg);
                }
            }
        }

        private WarehouseQueryInput GetInputFromXml()
        {
            string filePath = "Input/WarehouseQueryInput.xml";
            XDocument inputDoc = XDocument.Load(filePath);
            var output = (from x in inputDoc.Elements()
                          select new WarehouseQueryInput
                          {
                              customerNumber = x.Element("customerNumber")?.Value,
                              methodName = x.Element("methodName")?.Value,
                              requestId = x.Element("requestId")?.Value,
                              srcSystem = x.Element("srcSystem")?.Value,
                              status = x.Element("status")?.Value
                          }).FirstOrDefault();
            inputDoc.Elements().First().Element("requestId").Value = Guid.NewGuid().ToString();
            inputDoc.Save(filePath);
            _logger.InfoFormat("仓库任务:" + "获取查询参数成功");
            return output;
        }

        private void UpdatePostRequisitionInput(Warehouse warehouse)
        {
            string postRequisitionQueryInputPath = "Input/PostRequisitionQueryInput.xml";
            XDocument postDocument = XDocument.Load(postRequisitionQueryInputPath);
            var postRow = (from x in postDocument.Element("xml").Element("list").Elements()
                           where x.Element("storageOrgUnit_number")?.Value == warehouse.storageNumber
                           select x).FirstOrDefault();
            if (postRow == null)
            {
                XElement element = new XElement(
                    "row",
                    new XElement("requestId", "1"),
                    new XElement("srcSystem", "safebarcode"),
                    new XElement("methodName", "postRequisitionQuery"),
                    //new XElement("saleOrgUnit_number", "01.10"),
                    new XElement("saleOrgUnit_number", warehouse.storageNumber),
                    new XElement("storageOrgUnit_number", warehouse.storageNumber),
                    new XElement("bizDate_from", ""),
                    new XElement("bizDate_to", ""),

                    new XElement("status", ""),
                    new XElement("material_number_from", ""),
                    new XElement("material_number_to", ""),
                    new XElement("orderCustomer_number_from", ""),
                    new XElement("orderCustomer_number_to", ""),
                    new XElement("bill_number", "")
                    );
                postDocument.Element("xml").Element("list").Add(element);
                postDocument.Save(postRequisitionQueryInputPath);
            }
        }

        private void UpdatePurchaseOrderInput(Warehouse warehouse)
        {
            string purchaseOrderQueryInputPath = "Input/PurchaseOrderQueryInput.xml";
            XDocument purchaseDocument = XDocument.Load(purchaseOrderQueryInputPath);
            var purchaseRow = (from x in purchaseDocument.Element("xml").Element("list").Elements()
                               where x.Element("purchaseOrgUnit_number")?.Value == warehouse.storageNumber
                               select x).FirstOrDefault();
            if (purchaseRow == null)
            {
                XElement element = new XElement(
                    "row",
                    new XElement("requestId", "1"),
                    new XElement("srcSystem", "safebarcode"),
                    new XElement("methodName", "purOrderQuery"),
                    new XElement("purchaseOrgUnit_number", warehouse.storageNumber),
                    new XElement("bizDate_from", ""),
                    new XElement("bizDate_to", ""),

                    new XElement("status", ""),
                    new XElement("material_number_from", ""),
                    new XElement("material_number_to", ""),
                    new XElement("supplier_number_from", ""),
                    new XElement("supplier_number_to", ""),
                    new XElement("bill_number", "")
                    );
                purchaseDocument.Element("xml").Element("list").Add(element);
                purchaseDocument.Save(purchaseOrderQueryInputPath);
            }
        }
    }
}
