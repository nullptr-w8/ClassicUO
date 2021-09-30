using SciterCore;
using SciterCore.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassicUO.Game.UI
{
    public class Host : SciterArchiveHost
    {
        private string homePath = AppDomain.CurrentDomain.BaseDirectory;
        protected override LoadResult OnLoadData(object sender, LoadDataArgs args)
        {
            if (args.Uri.IsFile)
            {
                var path = Path.Combine(homePath, args.Uri.OriginalString.Replace("file://", ""));
                if(File.Exists(path))
                {
                    var data = File.ReadAllBytes(path);
                    Sciter.SciterApi.SciterDataReady(Window.Handle, path, data, (uint)data.Length);
                } 
            }

            Archive?.GetItem(args.Uri, res =>
            {
                if (res.IsSuccessful)
                {
                    Sciter.SciterApi.SciterDataReady(Window.Handle, res.Path, res.Data, (uint)res.Size);
                }
            });

            // call base to ensure LibConsole is loaded
            return base.OnLoadData(sender, args);
        }
    }
}
