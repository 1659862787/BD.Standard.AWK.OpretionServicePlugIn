
using Newtonsoft.Json.Linq;
using System.Data;

namespace BD.Standard.KY.ServicePlugIn
{
    public class MES
    {

        public static string SaveJson(DataSet datas)
        {
            JArray dataList = new JArray();
            JObject data = null;
            JObject model = null;
            if (datas.Tables[1].Rows.Count == 0) return null;
            //批量传输,表头明细的关联字段
            string id = string.Empty;
            foreach (DataRow dataH in datas.Tables[1].Rows)
            {
                model = new JObject();
                data = new JObject();
                data["IsAutoSubmitAndAudit"] = "true";
                foreach (DataColumn columns in dataH.Table.Columns)
                {
                    string column = columns.ColumnName.ToString();
                    if (column.Equals("id"))
                    {
                        id = dataH["id"].ToString();
                        continue;
                    }
                    if (column.Contains("_0"))
                    {
                        JObject fnumber = new JObject();
                        fnumber.Add(new JProperty("Fnumber", dataH[column].ToString()));
                        model[column.Substring(0, column.Length - 2)] = fnumber;
                    }
                    else
                    {
                        model.Add(new JProperty(column, dataH[column].ToString()));
                    }

                }
                if (datas.Tables.Count == 3)
                {
                    JArray jsonEntry = new JArray();
                    foreach (DataRow dataE in datas.Tables[2].Rows)
                    {
                        if (!id.Equals(dataE["id"].ToString())) continue;
                        JObject jobE = new JObject();
                        foreach (DataColumn columns in dataE.Table.Columns)
                        {
                            string column = columns.ColumnName.ToString();
                            if (!column.Equals("id"))
                            {
                                if (column.Contains("_0"))
                                {
                                    JObject fnumber = new JObject();
                                    fnumber.Add(new JProperty("Fnumber", dataE[column].ToString()));
                                    jobE[column.Substring(0, column.Length - 2)] = fnumber;
                                }
                                else
                                {
                                    jobE.Add(new JProperty(column, dataE[column].ToString()));
                                }
                            }
                        }
                        if (jobE.Count > 0) jsonEntry.Add(jobE);
                        model[datas.Tables[0].Rows[0].ItemArray[1].ToString()] = jsonEntry;

                    }
                }
                data["model"] = model;
                dataList.Add(data);
            }
            return dataList.ToString();

        }


    }
}
