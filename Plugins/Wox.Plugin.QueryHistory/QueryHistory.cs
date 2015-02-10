using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Wox.Plugin.QueryHistory
{
    public class QueryHistory : IPlugin
    {
        private PluginInitContext context;

        public List<Result> Query(Query query)
        {
            var histories = QueryHistoryStorage.Instance.GetHistory();
            string filter = query.Search;
            if (!string.IsNullOrEmpty(filter))
            {
                histories = histories.Where(o => o.Query.Contains(filter)).ToList();
            }
            return histories.Select(history => new Result()
            {
                Title = history.Query,
                SubTitle = history.GetTimeAgo(),
                IcoPath = "Images\\history.png",
                Action = _ =>
                {
                    context.API.ChangeQuery(history.Query);
                    return false;
                }
            }).ToList();
        }

        public void Init(PluginInitContext context)
        {
            this.context = context;
        }

        void API_BeforeWoxQueryEvent(WoxQueryEventArgs e)
        {
            Thread.Sleep(5000);
        }

        private void API_AfterWoxQueryEvent(WoxQueryEventArgs e)
        {
            QueryHistoryStorage.Instance.Add(e.Query.RawQuery);
        }
    }
}
