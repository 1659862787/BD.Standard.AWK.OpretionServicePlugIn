using Kingdee.BOS.App.Data;
using Kingdee.BOS.ServiceFacade.KDServiceFx;
using Kingdee.BOS.WebApi.ServicesStub;
using Newtonsoft.Json.Linq;
using System;
using System.Data;
using System.Text;
using System.ComponentModel;
using Kingdee.BOS;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.Core.Metadata;
using Kingdee.BOS.Core.Permission;
using Kingdee.BOS.ServiceHelper;
using Kingdee.BOS.Core.DynamicForm;
using Kingdee.BOS.Core.Metadata.EntityElement;
using Kingdee.BOS.Core.Bill;
using Kingdee.K3.Core.SCM;
using Kingdee.BOS.Core.Metadata.FieldElement;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Linq;
using Kingdee.BOS.WebApi.Client;
using Kingdee.K3.Core.MFG.EntityHelper;
using Kingdee.BOS.Util;

namespace BD.Standard.KY.ServicePlugIn
{
    [Kingdee.BOS.Util.HotUpdate]
    [Description("自定义接口插件")]
    public class GetJson1 : AbstractWebApiBusinessService
    {

        public GetJson1(KDServiceContext context) : base(context)
        {

        }
        public object ExecuteService(string formid, string data)
        //public object ExecuteService(string parameters)  
        {
            //string formid = parameters[0].ToString();
            //string data = parameters[1].ToString();
            /*string formid = string.Empty;
            string data = string.Empty;*/
            var ctx = KDContext.Session.AppContext;
            try
            {

                string key = InsertTable(ctx, formid, data);
                //string key = "";
                string str = "";
                //KYUTILS.WebApiClent("http://legiony9000p/k3cloud/", "668b9640668002", "张", "zzb@123..");
                //其他入库、盘盈盘亏、直接调拨构建
                if (formid.EqualsIgnoreCase("STK_MISCELLANEOUS") || formid.EqualsIgnoreCase("STK_StockCountGain") || formid.EqualsIgnoreCase("STK_StockCountLoss") || formid.EqualsIgnoreCase("STK_TransferDirect"))
                {
                    str = KYUTILS.Save(ctx, formid, data);
                }
                else
                {
                    str = PushBill(ctx, formid, key);
                }
                return JObject.Parse(str);

                //return "";


            }
            catch (Exception ex)
            {
                var sql1 = string.Format("DELETE FROM T_BAS_NETWORKCTRLRECORDS");
                DBUtils.Execute(ctx, sql1);
                JObject result = new JObject()
                {
                    new JProperty("node","0"),
                    new JProperty("data",ex.Message.ToString())
                };
                return result;
            }

        }


        /// <summary>
        /// 解析数据生成中间表
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="formid"></param>
        /// <param name="data"></param>
        private static string InsertTable(Context ctx, string formid, string data)
        {
            string sql = string.Format("exec KY_parseJson '{0}'", formid);
            DataSet dataSet = DBUtils.ExecuteDataSet(ctx, sql);
            JObject json = JObject.Parse(data.ToLower());
            JObject model = (JObject)json["model"];
            StringBuilder sb = new StringBuilder();
            StringBuilder column = new StringBuilder();
            StringBuilder value = new StringBuilder();

            #region 生成13位时间戳与6位随机数
            string datetimestr = DateTime.Now.ToUniversalTime().Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds.ToString("F0");
            string str = @"abcdefghigklmnopgrstuvwxyZABCDEFGHIGKLMNOPORSTUVWXYZ";
            string randstr = string.Empty;
            Random rand = new Random();
            for (int i = 0; i < 6; i++)
            {
                randstr += str.Substring(rand.Next(52), 1);
            }
            string key = datetimestr + randstr;
            #endregion 生成13位时间戳与6位随机数

            sb.Append(string.Format(@"/*dialect*/  insert into {0} ", "KY_middleTable"));
            value.Append("  values ");
            value.Append("\r\n('" + key + "','" + formid + "','" + data + "',");

            #region 表头数据
            DataRow dataH = dataSet.Tables[0].Rows[0];
            column.Append("(keys,formid,data,");
            foreach (DataColumn columns in dataH.Table.Columns)
            {
                string filed = columns.ColumnName.ToString();
                if (filed.Equals("fruleid"))
                {
                    JArray entrys = (JArray)model[dataH[filed].ToString()];

                    JArray entry = (JArray)entrys[0][dataH[filed] + "_link"];
                    string srcid = entry[0][dataH[filed] + "_link_" + filed].ToString();
                    column.Append("" + filed + ",");
                    value.Append("'" + srcid + "',");
                }
                else if (filed.Equals("Column1"))
                {
                    break;
                }
                else if (model.Property(dataH[filed].ToString()) != null)
                {
                    column.Append("" + filed + ",");
                    value.Append("'" + model[dataH[filed].ToString()].ToString().ToUpper() + "',");
                }
            }
            column.Remove(column.Length - 1, 1);
            if (!string.IsNullOrWhiteSpace(column.ToString())) sb.Append(column.Append(")").ToString());
            value.Remove(value.Length - 1, 1);
            value.Append(" ),");
            sb.Append(value.ToString());
            sb.Remove(sb.Length - 1, 1);
            #endregion 表头数据

            #region 表体数据
            DataRow dataE = dataSet.Tables[1].Rows[0];

            if (!dataE.Table.Columns[0].ColumnName.Equals("Column1"))
            {
                column = new StringBuilder();
                value = new StringBuilder();
                sb.Append(string.Format("\r\n insert into {0}", "KY_middleTables"));
                value.Append("  values ");
                column.Append("(keys,");
                bool flag = true;
                foreach (JObject items in (JArray)model[dataE[dataE.Table.Columns[0].ColumnName.ToString()]])
                {
                    value.Append("\r\n('" + key + "',");
                    foreach (DataColumn columns in dataE.Table.Columns)
                    {
                        string filed = columns.ColumnName.ToString();
                        if (filed.Equals("fsbillid") || filed.Equals("fsid"))
                        {
                            JArray entry = (JArray)items[dataE[filed] + "_link"];
                            string srcid = entry[0][dataE[filed] + "_link_" + filed].ToString();
                            column.Append("" + filed + ",");
                            value.Append("'" + srcid + "',");
                        }
                        else if (filed.Equals("Column1"))
                        {
                            break;
                        }
                        else
                        {
                            if (filed.Contains("_0"))
                            {
                                column.Append("" + filed.Substring(0, filed.Length - 2) + ",");
                                value.Append("'" + items[dataE[filed].ToString()]["fnumber"].ToString() + "',");
                            }
                            else if (filed.Contains("ff1000"))
                            {
                                column.Append("" + filed + ",");
                                JObject cangwei = (JObject)items[dataE[filed].ToString().Substring(0, dataE[filed].ToString().IndexOf("_"))];
                                value.Append("'" + cangwei[dataE[filed].ToString()]["fnumber"].ToString() + "',");
                            }
                            else if (items.Property(dataE[filed].ToString()) != null)
                            {
                                column.Append("" + filed + ",");
                                value.Append("'" + items[dataE[filed].ToString()].ToString() + "',");
                            }

                        }
                    }
                    column.Remove(column.Length - 1, 1);
                    if (!string.IsNullOrWhiteSpace(column.ToString()) && flag)
                    {
                        sb.Append(column.Append(")").ToString());
                        flag = false;
                    }
                    value.Remove(value.Length - 1, 1);
                    value.Append(" ),");

                }
                sb.Append(value.ToString());
                sb.Remove(sb.Length - 1, 1);
            }
            string insrtsql = sb.ToString();
            #endregion 表体数据
            DBUtils.Execute(ctx, insrtsql);
            return key;
        }



        /// <summary>
        /// 下推单据生成数据
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="formid"></param>
        private static string PushBill(Context ctx, string formid, string key)
        {
            try
            {
                IDynamicFormView view = null;

                if (formid.Equals("PRD_FeedMtrl"))
                {
                    int update = UpdateEntryid(ctx, key);
                    DynamicObjectCollection feeds = GetData(ctx, key, "PRD_FeedMtrl");

                    if (feeds.Count > 0)
                    {
                        string srcfromid1 = feeds[0]["srcformid"].ToString();
                        string ppbomno = DBUtils.ExecuteScalar<string>(ctx, "select fbillno from T_PRD_PPBOM where fid='" + feeds[0]["fsbillid"].ToString() + "'", "", null);

                        string fid1 = KYUTILS.ERPPushJson(ctx, srcfromid1, feeds[0]["fsbillid"].ToString(), "PRD_PPBOM2PPBOMCHANGE", false);
                        if (!string.IsNullOrWhiteSpace(fid1) && fid1.Length < 8)
                        {
                            FormMetadata meta = (FormMetadata)MetaDataServiceHelper.Load(ctx, "PRD_PPBOMChange");
                            view = KYUTILS.OpenWebView(ctx, meta, "PRD_PPBOMChange", fid1);

                            EntryEntity entryentity = view.BillBusinessInfo.GetEntryEntity("FEntity");
                            DynamicObjectCollection entrys = view.Model.GetEntityDataObject(entryentity) as DynamicObjectCollection;
                            entrys.Clear();
                            int row = 0;
                            foreach (var item in feeds)
                            {
                                if (view.InvokeFormOperation(view.GetFormOperation("NewEntry")))
                                {
                                    view.Model.SetItemValueByNumber("FMaterialID2", item["fmaterial"].ToString(), row);
                                    view.Model.SetValue("FNumerator", "0", row);
                                    view.Model.SetValue("FPPBOMNo", ppbomno, row++);
                                }
                            }
                            if (view.InvokeFormOperation(FormOperationEnum.Save))
                            {
                                if (view.InvokeFormOperation(FormOperationEnum.Submit))
                                {
                                    if (view.InvokeFormOperation(FormOperationEnum.Audit))
                                    {

                                    }
                                }
                            }
                            view.Close();
                        }
                        UpdateEntryid(ctx, key);
                    }
                }


                //DynamicObjectCollection dyc = GetData(ctx, "1720081202545LOXEEH", "");
                DynamicObjectCollection dyc = GetData(ctx, key, "");
                string srcfromid = dyc[0]["srcformid"].ToString();

                StringBuilder sb = new StringBuilder();
                foreach (var dy in dyc)
                {
                    sb.Append(dy["fsid"].ToString() + ",");
                }
                string fid = KYUTILS.ERPPushJson(ctx, srcfromid, sb.Remove(sb.Length - 1, 1).ToString(), dyc[0]["fruleid"].ToString(), true);

                if (!string.IsNullOrWhiteSpace(fid) && fid.Length < 8)
                {
                    FormMetadata meta = (FormMetadata)MetaDataServiceHelper.Load(ctx, formid);
                    view = KYUTILS.OpenWebView(ctx, meta, formid, fid);

                    EntryEntity entryentity = view.BillBusinessInfo.GetEntryEntity(dyc[0]["fentry"].ToString());
                    DynamicObjectCollection entrys = view.Model.GetEntityDataObject(entryentity) as DynamicObjectCollection;

                    List<string> list = new List<string>();
                    foreach (var dy in dyc)
                    {
                        list.Add(dy["fsid"].ToString());
                    }

                    List<string> vs = list.GroupBy(g => string.Format("{0}", g)).Where(w => w.Count() > 0).Select(s => s.Key).ToList();
                    foreach (var v in vs)
                    {
                        int count = 0;
                        foreach (var dy in dyc)
                        {
                            if (v.Equals(dy["fsid"]) && v.Count() > 1) count++;
                        }
                        foreach (var entry in entrys)
                        {
                            int seq = Convert.ToInt32(entry["seq"]) - 1;
                            if (formid.Equals("PRD_FeedMtrl"))
                            {
                                view.Model.SetValue(dyc[0]["fqty"].ToString(), Convert.ToDecimal(dyc[seq]["FRealQty"]), seq);
                                view.Model.SetItemValueByNumber(dyc[0]["FStock"].ToString(), dyc[seq]["FStockId"].ToString(), seq);
                                view.Model.SetItemValueByNumber(dyc[0]["FStockStatus"].ToString(), "KCZT01_SYS", seq);
                                view.Model.SetItemValueByNumber(dyc[0]["flotField"].ToString(), dyc[seq]["flot"].ToString(), seq);
                                string fstockid = GetStockLocId(ctx, dyc[seq]);
                                //仓位赋值
                                RelatedFlexGroupField stockLocField = view.BusinessInfo.GetField(dyc[0]["FStockLoc"].ToString()) as RelatedFlexGroupField;
                                DynamicObject dyRow = view.Model.GetEntityDataObject(stockLocField.Entity)[seq];
                                // 清空仓位
                                stockLocField.DynamicProperty.SetValue(dyRow, null);
                                stockLocField.RefIDDynamicProperty.SetValue(dyRow, 0);
                                var stockLoc = BusinessDataServiceHelper.LoadFromCache(view.Context, new object[] { fstockid }, stockLocField.RefFormDynamicObjectType);
                                stockLocField.DynamicProperty.SetValue(dyRow, stockLoc[0]);
                                stockLocField.RefIDDynamicProperty.SetValue(dyRow, Convert.ToInt32(fstockid));

                                view.InvokeFieldUpdateService(dyc[0]["fqty"].ToString(), seq);
                                view.InvokeFieldUpdateService(dyc[0]["FStockStatus"].ToString(), seq);

                            }

                            //原明细id
                            DynamicObjectCollection link = entry[dyc[0]["fentrylink"].ToString()] as DynamicObjectCollection;
                            string Sid = link[0]["Sid"].ToString();
                            if (v.Equals(Sid))
                            {
                                for (int i = 1; i <= count - 1; i++)
                                {
                                    AssociatedCopyRowUtil.CopyRow((IBillView)view, dyc[0]["fentry"].ToString(), seq, seq + i, true);
                                }
                                break;
                            }
                        }
                    }
                    view.Model.SetValue("Fbillno", dyc[0]["fbillno"].ToString(), 0);
                    view.Model.SetValue("Fdate", dyc[0]["Fdate"].ToString(), 0);
                    string status = view.Model.GetValue("FDocumentStatus").ToString();
                    if (status.Equals("A"))
                    {
                        bool ss = view.InvokeFormOperation(FormOperationEnum.Save);
                    }
                    else
                    {
                        view.InvokeFormOperation(FormOperationEnum.Draft);
                    }

                    List<string> fentry = new List<string>();
                    foreach (var dy in dyc)
                    {
                        string fsid = dy["fsid"].ToString();
                        foreach (var entry in entrys)
                        {
                            string fentryid = entry[0].ToString();
                            if (!fentry.Contains(fentryid))
                            {
                                int seq = Convert.ToInt32(entry["seq"]) - 1;
                                DynamicObjectCollection link = entry[dyc[0]["fentrylink"].ToString()] as DynamicObjectCollection;
                                string Sid = link[0]["Sid"].ToString();
                                if (fsid.Equals(Sid))
                                {

                                    SetValues(ctx, formid, view, dyc, fentry, dy, fentryid, seq);
                                    break;
                                }
                            }
                        }
                    }
                    view.UpdateView();
                    string result = string.Empty;
                    if (!view.InvokeFormOperation(FormOperationEnum.Save))
                    {
                        view.Close();

                        return result;
                    }
                    if (!view.InvokeFormOperation(FormOperationEnum.Submit))
                    {
                        view.Close();
                        result = KYUTILS.Option(ctx, formid, fid, "Submit");
                        //KYUTILS.Option(formid, fid, "Delete");
                        return result;
                    }
                    if (!view.InvokeFormOperation(FormOperationEnum.Audit))
                    {
                        view.Close();
                        result = KYUTILS.Option(ctx, formid, fid, "Audit");
                        //KYUTILS.Option(formid, fid, "Delete");
                        //删除当前单据
                        return result;
                    }
                    else
                    {
                        view.Close();
                        JObject str = new JObject()
                        {
                            new JProperty("Message","审核成功"),
                            new JProperty("fid",fid)
                        };
                        return str.ToString();
                    }
                }

                return fid;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }

        }



        /// <summary>
        /// 单据赋值
        /// </summary>
        /// <param name="ctx">当前上下文</param>
        /// <param name="view">当前单据视图</param>
        /// <param name="dyc">中间表集合，默认获取第一行的部分数据,规定列明</param>
        /// <param name="fentry">明细分录id集合</param>
        /// <param name="dy">中间表集合子项，用于数据赋值</param>
        /// <param name="fentryid">明细分录id</param>
        /// <param name="seq">明细索引</param>
        private static void SetValues(Context ctx, String formid, IDynamicFormView view, DynamicObjectCollection dyc, List<string> fentry, DynamicObject dy, string fentryid, int seq)
        {
            //生产汇报单
            if (formid.Equals("PRD_MORPT"))
            {
                view.Model.SetValue("FQuaQty", Convert.ToDecimal(dy["FQuaQty"]), seq);
                view.Model.SetValue("FFailQty", Convert.ToDecimal(dy["FFailQty"]), seq);
                view.Model.SetValue("FReworkQty", Convert.ToDecimal(dy["FReworkQty"]), seq);
                view.Model.SetValue("FScrapQty", Convert.ToDecimal(dy["FScrapQty"]), seq);
                view.Model.SetValue("FReMadeQty", Convert.ToDecimal(dy["FReMadeQty"]), seq);

                if (Convert.ToDecimal(dy["FFinishQty"]) > 0)
                {
                    view.Model.SetValue("FFinishQty", Convert.ToDecimal(dy["FFinishQty"]), seq);
                    view.InvokeFieldUpdateService("FFinishQty", seq);
                }
                view.Model.SetValue("FHrWorkTime", Convert.ToDecimal(dy["FHrWorkTime"]), seq);
                view.InvokeFieldUpdateService("FHrWorkTime", seq);

                view.Model.SetItemValueByNumber(dyc[0]["FStock"].ToString(), dy["FStockId"].ToString(), seq);
                view.Model.SetItemValueByNumber(dyc[0]["FStockStatus"].ToString(), "KCZT01_SYS", seq);
                view.Model.SetItemValueByNumber(dyc[0]["flotField"].ToString(), dy["flot"].ToString(), seq);
                string fstockid = GetStockLocId(ctx, dy);
                //仓位赋值
                RelatedFlexGroupField stockLocField = view.BusinessInfo.GetField(dyc[0]["FStockLoc"].ToString()) as RelatedFlexGroupField;
                DynamicObject dyRow = view.Model.GetEntityDataObject(stockLocField.Entity)[seq];
                // 清空仓位
                stockLocField.DynamicProperty.SetValue(dyRow, null);
                stockLocField.RefIDDynamicProperty.SetValue(dyRow, 0);
                var stockLoc = BusinessDataServiceHelper.LoadFromCache(view.Context, new object[] { fstockid }, stockLocField.RefFormDynamicObjectType);
                stockLocField.DynamicProperty.SetValue(dyRow, stockLoc[0]);
                stockLocField.RefIDDynamicProperty.SetValue(dyRow, Convert.ToInt32(fstockid));


                view.InvokeFieldUpdateService(dyc[0]["FStockStatus"].ToString(), seq);
                fentry.Add(fentryid);
            }
            else
            {
                view.Model.SetValue(dyc[0]["fqty"].ToString(), Convert.ToDecimal(dy["FRealQty"]), seq);
                view.Model.SetItemValueByNumber(dyc[0]["FStock"].ToString(), dy["FStockId"].ToString(), seq);
                view.Model.SetItemValueByNumber(dyc[0]["FStockStatus"].ToString(), "KCZT01_SYS", seq);
                view.Model.SetItemValueByNumber(dyc[0]["flotField"].ToString(), dy["flot"].ToString(), seq);
                string fstockid = GetStockLocId(ctx, dy);
                //仓位赋值
                RelatedFlexGroupField stockLocField = view.BusinessInfo.GetField(dyc[0]["FStockLoc"].ToString()) as RelatedFlexGroupField;
                DynamicObject dyRow = view.Model.GetEntityDataObject(stockLocField.Entity)[seq];
                // 清空仓位
                stockLocField.DynamicProperty.SetValue(dyRow, null);
                stockLocField.RefIDDynamicProperty.SetValue(dyRow, 0);
                var stockLoc = BusinessDataServiceHelper.LoadFromCache(view.Context, new object[] { fstockid }, stockLocField.RefFormDynamicObjectType);
                stockLocField.DynamicProperty.SetValue(dyRow, stockLoc[0]);
                stockLocField.RefIDDynamicProperty.SetValue(dyRow, Convert.ToInt32(fstockid));

                view.InvokeFieldUpdateService(dyc[0]["fqty"].ToString(), seq);
                view.InvokeFieldUpdateService(dyc[0]["FStockStatus"].ToString(), seq);
                fentry.Add(fentryid);
            }
        }



        /// <summary>
        /// 获取中间表数据
        /// </summary>
        /// <param name="ctx">上下文</param>
        /// <param name="key">当前数据主键</param>
        /// <returns>返回当前单据对应中间表数据</returns>
        private static DynamicObjectCollection GetData(Context ctx, string key, string formid)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(@"/*dialect*/ select a.formid,fruleid,srcformid,fentry,fentrylink,fqty,c.flot flotField,FStock,FStockLoc,FStockStatus,");
            sb.Append("b.fsbillid,fsid,fbillno,fdate,fmaterial,FRealQty,b.FLot flot,FStockId,FF100004,FF100005,FF100006,FF100007,");
            sb.Append("FFinishQty,FQuaQty,FFailQty,FReworkQty,FScrapQty,FReMadeQty,FHrWorkTime ");
            sb.Append(" from  KY_middleTable a ");
            sb.Append(" left join  KY_middleTables b on a.keys=b.keys ");
            sb.Append(" left join KY_rules c on a.formid=c.formid ");
            sb.Append("  where a.keys='" + key + "' ");
            if (!string.IsNullOrWhiteSpace(formid))
            {
                sb.Append(" and isnull(fsid,'')=''");
            }
            string sql = sb.ToString();
            DynamicObjectCollection dyc = DBUtils.ExecuteDynamicObject(ctx, sql) as DynamicObjectCollection;
            return dyc;
        }

        /// <summary>
        /// 获取仓位值集
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="dy"></param>
        /// <returns>返回对应仓位值集</returns>
        private static string GetStockLocId(Context ctx, DynamicObject dy)
        {
            StringBuilder stockLocId = new StringBuilder();
            stockLocId.AppendLine(@"/*dialect*/ SELECT fid FROM T_BAS_FLEXVALUESDETAIL ");
            stockLocId.AppendLine(" where 1=1");
            stockLocId.AppendLine(string.Format(" and FF100001=(select fentryid from T_BAS_FLEXVALUESENTRY  where fnumber='{0}') ", dy["FF100004"]).ToString());
            stockLocId.AppendLine(string.Format(" and FF100002=(select fentryid from T_BAS_FLEXVALUESENTRY  where fnumber='{0}') ", dy["FF100005"].ToString()));
            stockLocId.AppendLine(string.Format(" and FF100003=(select fentryid from T_BAS_FLEXVALUESENTRY  where fnumber='{0}') ", dy["FF100006"].ToString()));
            stockLocId.AppendLine(string.Format(" and FF100004=(select fentryid from T_BAS_FLEXVALUESENTRY  where fnumber='{0}') ", dy["FF100007"].ToString()));
            string sql1 = stockLocId.ToString();
            string x1 = DBUtils.ExecuteScalar<string>(ctx, sql1, "", null);
            return x1;
        }


        private static int UpdateEntryid(Context ctx, string key)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(@"/*dialect*/ update a set fsid=p.FENTRYID  ");
            sb.AppendLine(" from  KY_middleTables a");
            sb.AppendLine(" left join T_BD_MATERIAL m on a.FMaterial=m.FNUMBER ");
            sb.AppendLine(" left join T_PRD_PPBOMentry p on fid=fsbillid and p.FMATERIALID=m.FMATERIALID");
            sb.AppendLine(" where keys='" + key + "' and isnull(a.fsid,'')=''");
            string sql1 = sb.ToString();
            int i = DBUtils.Execute(ctx, sql1);
            return i;
        }
    }
}
