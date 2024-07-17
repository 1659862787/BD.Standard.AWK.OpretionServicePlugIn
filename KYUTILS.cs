using Kingdee.BOS;
using Kingdee.BOS.App.Data;
using Kingdee.BOS.Core.Bill;
using Kingdee.BOS.Core.DynamicForm;
using Kingdee.BOS.Core.Metadata;
using Kingdee.BOS.Core.Metadata.FormElement;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.WebApi.Client;
using Kingdee.BOS.WebApi.FormService;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace BD.Standard.KY.ServicePlugIn
{
    public class KYUTILS
    {
        static K3CloudApiClient client = null;
        static int result = 0;
        public static void WebApiClent(string url, string accountid, string username, string password)
        {
            client = new K3CloudApiClient(url);
            var loginResult = client.ValidateLogin(accountid, username, password, 2052);
            result = JObject.Parse(loginResult)["LoginResultType"].Value<int>();
        }


        /// <summary>
        /// 生成下推json
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public static string ERPPushJson(Context ctx,string formid, string EntryIds, string RuleId,bool flag)
        {
            JObject json = null;
            if (flag)
            {
                json = new JObject()
                {
                    new JProperty("EntryIds",EntryIds),
                    new JProperty("RuleId",RuleId),
                    new JProperty("IsEnableDefaultRule","false"),
                    new JProperty("IsDraftWhenSaveFail","true"),
                };
            }
            else
            {
                //生产补料变更新增
                json = new JObject()
                {
                    new JProperty("Ids",EntryIds),
                    new JProperty("RuleId",RuleId),
                    new JProperty("IsEnableDefaultRule","false"),
                    new JProperty("IsDraftWhenSaveFail","true"),
                };
            }
            string MessageReturned = JsonConvert.SerializeObject(WebApiServiceCall.Push(ctx, formid, JsonConvert.SerializeObject(json)));

            if (JObject.Parse(MessageReturned)["Result"]["ResponseStatus"]["IsSuccess"].ToString().Equals("True"))
            {
                string fid = ((Newtonsoft.Json.Linq.JContainer)JObject.Parse(JObject.Parse(MessageReturned)["Result"]["ResponseStatus"]["SuccessEntitys"][0].ToString()).First).First.ToString();
                return fid;
            }
            else
            {
                return MessageReturned;
            }
        }

        /// <summary>
        /// 操作
        /// </summary>
        /// <param name="formid"></param>
        /// <param name="fid"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        public static string Option(Context ctx, string formid, string fid,string option)
        {
            //其他json
            JObject json = new JObject()
            {
                new JProperty("Ids",fid),
            };
            //保存json
            JObject id = new JObject()
            {
                new JProperty("fid",fid),
            };
            JObject jsons = new JObject()
            {
                new JProperty("model",id),
            };
            switch (option) 
                {
                    case "Draft":
                        return JsonConvert.SerializeObject(WebApiServiceCall.Draft(ctx,formid, JsonConvert.SerializeObject(jsons)));
                    case "Save":
                        return JsonConvert.SerializeObject(WebApiServiceCall.Save(ctx,formid, JsonConvert.SerializeObject(jsons)));
                    case "Submit":
                        return JsonConvert.SerializeObject(WebApiServiceCall.Submit(ctx,formid, JsonConvert.SerializeObject(json)));
                    case "Audit":
                        return JsonConvert.SerializeObject(WebApiServiceCall.Audit(ctx,formid, JsonConvert.SerializeObject(json)));
                    case "Delete":
                    JsonConvert.SerializeObject(WebApiServiceCall.UnAudit(ctx,formid, JsonConvert.SerializeObject(json)));
                        return client.Delete(formid, JsonConvert.SerializeObject(json));
                }
            return ""; 

        }

        /// <summary>
        /// 保存单据
        /// </summary>
        /// <param name="formid"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string Save(Context ctx, string formid, string data)
        {
            

                String MessageReturned = JsonConvert.SerializeObject(WebApiServiceCall.Push(ctx,formid, JsonConvert.SerializeObject(data)));
                if (JObject.Parse(MessageReturned)["Result"]["ResponseStatus"]["IsSuccess"].ToString().Equals("True"))
                {
                    return MessageReturned;
                }
                else
                {
                    return MessageReturned;
                }

        }

        /// <summary>
        /// 打开表单
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="meta"></param>
        /// <param name="formId"></param>
        /// <param name="PkId"></param>
        /// <returns></returns>
        public static IDynamicFormView OpenWebView(Context ctx, FormMetadata meta, string formId, object PkId = null)
        {
            BusinessInfo info = meta.BusinessInfo;
            Form form = info.GetForm();
            BillOpenParameter param = new BillOpenParameter(formId, null);
            param.SetCustomParameter("formID", form.Id);
            //根据主键是否为空 置为新增或修改状态
            param.InitStatus = OperationStatus.EDIT;
            param.Status = OperationStatus.EDIT;
            param.PageId = Guid.NewGuid().ToString();
            param.SetCustomParameter("Status", OperationStatus.EDIT);
            param.SetCustomParameter("InitStatus", OperationStatus.EDIT);
            param.SetCustomParameter("PlugIns", form.CreateFormPlugIns());  //插件实例模型
            //修改主业务组织无须用户确认
            param.SetCustomParameter("ShowConformDialogWhenChangeOrg", false);

            param.Context = ctx;
            param.FormMetaData = meta;
            param.LayoutId = param.FormMetaData.GetLayoutInfo().Id;
            param.PkValue = !IsPrimaryValueEmpty(PkId) ? PkId : null;//单据主键内码FID
            IResourceServiceProvider provider = form.GetFormServiceProvider();
            //普通的动态表单模式DynamicFormView
            //IDynamicFormView billview  = provider.GetService(typeof(IDynamicFormView)) as IDynamicFormView;
            //这里模拟为引入模式的WebView，否则遇到交互的时候会有问题，移动端目前无法直接交互
            Type type = Type.GetType("Kingdee.BOS.Web.Import.ImportBillView,Kingdee.BOS.Web");
            IDynamicFormView billview = (IDynamicFormView)Activator.CreateInstance(type);
            (billview as IBillViewService).Initialize(param, provider);//初始化 
            (billview as IBillViewService).LoadData();
            //加载单据数据 
            //如果是普通DynamicFormView时，LoadData的时候会加网控，要清除。
            //引入模式View不需要               
            // (billview  as IBillView).CommitNetworkCtrl();
            return billview;
        }

        private static bool IsPrimaryValueEmpty(object pk)
        {
            return pk == null || pk.ToString() == "0" || string.IsNullOrWhiteSpace(pk.ToString());
        }

    }
}
