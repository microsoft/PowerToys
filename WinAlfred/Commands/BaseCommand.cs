using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WinAlfred.Plugin;

namespace WinAlfred.Commands
{
    public abstract class BaseCommand
    {
        private MainWindow window;

        public void Dispatch(Query query)
        {
            Dispatch(query, true);
        }

        public abstract void Dispatch(Query query, bool updateView);

        //TODO:Ugly, we should subscribe events here, instead of just use usercontrol as the parameter
        protected BaseCommand(MainWindow window)
        {
            this.window = window;
        }

        protected void UpdateResultView(List<Result> results)
        {
            window.OnUpdateResultView(results);
        }
    }
}
