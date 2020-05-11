using System;
using System.Linq;
using System.Threading.Tasks;

using PowerToys_Settings_Sandbox.Core.Models;
using PowerToys_Settings_Sandbox.Core.Services;
using PowerToys_Settings_Sandbox.Helpers;

namespace PowerToys_Settings_Sandbox.ViewModels
{
    public class ContentGridDetailViewModel : Observable
    {
        private SampleOrder _item;

        public SampleOrder Item
        {
            get { return _item; }
            set { Set(ref _item, value); }
        }

        public ContentGridDetailViewModel()
        {
        }

        public async Task InitializeAsync(long orderID)
        {
            var data = await SampleDataService.GetContentGridDataAsync();
            Item = data.First(i => i.OrderID == orderID);
        }
    }
}
