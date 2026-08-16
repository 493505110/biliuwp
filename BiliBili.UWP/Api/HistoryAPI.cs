using BiliBili.UWP.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BiliBili.UWP.Api
{
    public class HistoryAPI
    {
        public async Task<List<GetHistoryModel>> GetHistory(int pageNumber, int pageSize = 30)
        {
            try
            {
                string url = string.Format(
                    "https://api.bilibili.com/x/v2/history?pn={0}&ps={1}&jsonp=json",
                    pageNumber,
                    pageSize);
                string results = await WebClientClass.GetResults(new Uri(url));
                GetHistoryModel model = JsonConvert.DeserializeObject<GetHistoryModel>(results);
                if (model?.data == null)
                {
                    return null;
                }

                return JsonConvert.DeserializeObject<List<GetHistoryModel>>(model.data.ToString());
            }
            catch
            {
                return null;
            }
        }
    }
}
