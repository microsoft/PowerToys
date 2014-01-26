using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WinAlfred.Plugin;

namespace WinAlfred.Commands
{
    public abstract class BaseCommand
    {
        public abstract void Dispatch(Query query, bool updateView  = true);

        protected void UpdateResultView(List<Result> results)
        {
            App.Window.OnUpdateResultView(results);
        }
    }
}
