using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.Plugin.System
{
    public class Setting : BaseSystemPlugin
    {
        private PluginInitContext context;
        protected override List<Result> QueryInternal(Query query)
        {
            List<Result> results = new List<Result>();
            if ("setting".Contains(query.RawQuery.ToLower()))
            {
                results.Add(new Result()
                {
                    Title = "Wox Setting Dialog",
                    Score = 100,
                    IcoPath = "Images/app.png",
                    Action = (contenxt) =>
                    {
                        context.OpenSettingDialog();
                        return true;
                    }
                });
            }

            return results;
        }

        protected override void InitInternal(PluginInitContext context)
        {
            this.context = context;
        }
    }
}
