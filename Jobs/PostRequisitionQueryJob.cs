using EasErpQuerySys.EasApplication;
using EasErpQuerySys.EasApplication.Dto;
using EasErpQuerySys.JobScheduling.Dto;
using EasErpQuerySys.UpdateApplication;
using log4net;
using Newtonsoft.Json;
using Quartz;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Xml.Linq;

namespace EasErpQuerySys.JobScheduling.Jobs
{
    public sealed class PostRequisitionQueryJob : IJob
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(PostRequisitionQueryJob));
        private readonly IEasAppService _easAppService = new EasAppService();
        private readonly IUpdateAppService _updateAppService = new UpdateAppService();
        private int _requestCount = 0;
        private int _maximumNumOfRequests = string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["MaximumNumOfRequests"])
            ? 10 : int.Parse(ConfigurationManager.AppSettings["MaximumNumOfRequests"]);
        public void Execute(IJobExecutionContext context)
        {
            _logger.InfoFormat("发货通知单任务:" + "开始");
            MailJob();

            _logger.InfoFormat("发货通知单任务:本次任务结束");
        }

        private void MailJob()
        {
            GetInputList().ForEach(inp =>
            {
                _requestCount = 0;
                RequestAndUpdate(inp);
            });
        }

        private void RequestAndUpdate(PostRequisitionQueryInput postRequisitionQueryInput)
        {
            _requestCount++;
            try
            {
                var input = UpdateXmlParameter(postRequisitionQueryInput);
                PostRequisitionQueryOutput queryResult = _easAppService.PostRequisitionQuery(input);
                _logger.InfoFormat("发货通知单任务:请求数据成功");
                queryResult.PostRequisitionList.ForEach(t =>
                {
                    t.entryList.ForEach(y =>
                    {
                        UpdatePostRequisitionQueryInput updatePostRequisitionQueryInput = new UpdatePostRequisitionQueryInput
                        {
                            bill_id = t.bill_id,
                            bill_number = t.bill_number,
                            bizDate = t.bizDate,
                            bizType_name = t.bizType_name,
                            status = t.status,

                            entry_id = y.entry_id,
                            material_model = y.material_model,
                            material_name = y.material_name,
                            material_number = y.material_number,
                            orderCustomerName = y.orderCustomerName,
                            orderCustomerNnumber = y.orderCustomerNnumber,
                            qty = y.qty,
                            shippedQty = y.shippedQty,

                            storageOrgUnit_number = input.storageOrgUnit_number
                        };

                        try
                        {
                            var tt = _updateAppService.UpdatePostRequisitionQuery(JsonConvert.SerializeObject(updatePostRequisitionQueryInput));
                            _logger.InfoFormat("发货通知单任务:" + "上传数据成功");
                        }
                        catch (Exception e)
                        {
                            throw new EasWebServiceException("update_error", e.Message);
                        }

                    });
                });
                _logger.InfoFormat("发货通知单任务:本轮结束");
            }
            catch (EasWebServiceException e)
            {
                var result = e.GetExceptionResult();
                if (result.resultStatus == "fail")
                {
                    _logger.InfoFormat("发货通知单任务:" + "请求数据失败，" + result.resultMsg);
                    if (_requestCount < _maximumNumOfRequests)
                    {
                        RequestAndUpdate(postRequisitionQueryInput);
                    }
                    else
                    {
                        _logger.InfoFormat("发货通知单任务:" + "请求次数达到上限" + _maximumNumOfRequests + "，本轮结束");
                    }
                }
                else if (result.resultStatus == "update_error")
                {
                    _logger.InfoFormat("发货通知单任务:" + "上传数据失败，" + result.resultMsg);
                }
                else if (result.resultStatus == "parameter_error")
                {
                    _logger.InfoFormat("发货通知单任务:" + result.resultMsg + "，本轮结束");
                }
                else
                {
                    _logger.InfoFormat("发货通知单任务:" + "请求数据失败，" + result.resultMsg);
                }
            }
        }

        private List<PostRequisitionQueryInput> GetInputList()
        {
            try
            {
                string filePath = "Input/PostRequisitionQueryInput.xml";
                XDocument inputDoc = XDocument.Load(filePath);
                var inputList = (from x in inputDoc.Element("xml").Element("list").Elements()
                                 select new PostRequisitionQueryInput
                                 {
                                     bill_number = x.Element("bill_number")?.Value,
                                     bizDate_from = x.Element("bizDate_from")?.Value,
                                     bizDate_to = x.Element("bizDate_to")?.Value,
                                     material_number_from = x.Element("material_number_from")?.Value,
                                     material_number_to = x.Element("material_number_to")?.Value,
                                     methodName = x.Element("methodName")?.Value,
                                     orderCustomer_number_from = x.Element("orderCustomer_number_from")?.Value,
                                     orderCustomer_number_to = x.Element("orderCustomer_number_to")?.Value,
                                     requestId = x.Element("requestId")?.Value,
                                     saleOrgUnit_number = x.Element("saleOrgUnit_number")?.Value,
                                     srcSystem = x.Element("srcSystem")?.Value,
                                     storageOrgUnit_number = x.Element("storageOrgUnit_number")?.Value,
                                     status = x.Element("status")?.Value
                                 }).ToList();
                if (inputList.Count() <= 0)
                    throw new EasWebServiceException("parameter_error", "暂无仓库信息");
                return inputList;
            }
            catch (EasWebServiceException e)
            {
                var result = e.GetExceptionResult();
                _logger.InfoFormat("发货通知单任务:" + "获取查询参数失败，" + result.resultMsg + "，本次任务结束");
                return new List<PostRequisitionQueryInput>();
            }
        }

        private PostRequisitionQueryInput UpdateXmlParameter(PostRequisitionQueryInput input)
        {

            string filePath = "Input/PostRequisitionQueryInput.xml";

            XDocument inputDoc = XDocument.Load(filePath);

            var targetEle = (from x in inputDoc.Element("xml").Element("list").Elements()
                             where x.Element("storageOrgUnit_number")?.Value == input.storageOrgUnit_number
                             select x).FirstOrDefault();
            input.requestId = targetEle.Element("requestId")?.Value;


            DateTime.TryParse(input.bizDate_from, out DateTime dateFrom);
            DateTime.TryParse(input.bizDate_to, out DateTime dateTo);

            if (dateTo != DateTime.MinValue && dateFrom != DateTime.MinValue)
            {
                targetEle.Element("bizDate_from").Value = dateTo.Date.AddDays(-1).ToString("yyyy-MM-dd");
                targetEle.Element("bizDate_to").Value = dateTo.Date.AddDays(1).ToString("yyyy-MM-dd");
            }
            else
            {

                if (dateTo > dateFrom)
                {
                    input.bizDate_from = dateTo.Date.AddDays(-1).ToString("yyyy-MM-dd");
                    targetEle.Element("bizDate_from").Value = dateTo.Date.AddDays(-1).ToString("yyyy-MM-dd");
                    targetEle.Element("bizDate_to").Value = dateTo.Date.AddDays(1).ToString("yyyy-MM-dd");
                }
                else
                {
                    input.bizDate_from = DateTime.Now.Date.AddDays(-1).ToString("yyyy-MM-dd");
                    input.bizDate_to = DateTime.Now.Date.AddDays(1).ToString("yyyy-MM-dd");


                    targetEle.Element("bizDate_from").Value = dateTo.Date.ToString("yyyy-MM-dd");
                    targetEle.Element("bizDate_to").Value = dateTo.Date.AddDays(2).ToString("yyyy-MM-dd");
                }
            }
            targetEle.Element("requestId").Value = Guid.NewGuid().ToString();
            inputDoc.Save(filePath);
            _logger.InfoFormat("发货通知单任务:" + "获取查询参数成功");
            return input;
        }
    }
}
