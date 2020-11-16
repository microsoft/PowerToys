// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Plugin.Service.Helper;
using Wox.Plugin;

namespace Microsoft.Plugin.Service
{
    public class Main : IPlugin, IContextMenu
    {
        private PluginInitContext _context;

        public void Init(PluginInitContext context)
        {
            _context = context;
        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            if (!(selectedResult?.ContextData is ServiceResult))
            {
                return new List<ContextMenuResult>();
            }

            var contextMenuResult = new List<ContextMenuResult>();
            var serviceResult = selectedResult.ContextData as ServiceResult;

            if (serviceResult.IsRunning)
            {
                contextMenuResult.Add(new ContextMenuResult
                {
                    PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                    Title = "Stop",
                    Glyph = "\xE71A",
                    FontFamily = "Segoe MDL2 Assets",
                    Action = _ =>
                    {
                        Task.Run(() => ServiceHelper.Stop(serviceResult, _context.API));
                        return true;
                    },
                });

                contextMenuResult.Add(new ContextMenuResult
                {
                    PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                    Title = "Restart",
                    Glyph = "\xE72C",
                    FontFamily = "Segoe MDL2 Assets",
                    Action = _ =>
                    {
                        Task.Run(() => ServiceHelper.Restart(serviceResult, _context.API));
                        return true;
                    },
                });
            }
            else
            {
                contextMenuResult.Add(new ContextMenuResult
                {
                    PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                    Title = "Start",
                    Glyph = "\xEDB5",
                    FontFamily = "Segoe MDL2 Assets",
                    Action = _ =>
                    {
                        Task.Run(() => ServiceHelper.Start(serviceResult, _context.API));
                        return true;
                    },
                });
            }

            return contextMenuResult;
        }

        public List<Result> Query(Query query)
        {
            var search = query?.Search ?? string.Empty;
            return ServiceHelper.Search(search).ToList();
        }
    }
}
