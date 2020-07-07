using System;

namespace Wox.Plugin
{
    public class ToolTipData
    {
        public string Title { get; private set; }

        public string Text { get; private set; }

        public ToolTipData(string title, string text)
        {          
            if(string.IsNullOrEmpty(title))
            {
                throw new ArgumentException("title cannot be null or empty", "title");
            }
            Title = title;
            Text = text;
        }
    }
}
