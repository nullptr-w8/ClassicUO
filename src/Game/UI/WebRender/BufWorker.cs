using SciterCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassicUO.Game.UI.WebRender
{
    public class SciterValueWorker
    {
        private readonly List<Tuple<ushort, string>> _entries = new List<Tuple<ushort, string>>();
        
        public Tuple<ushort, string>[] getData(SciterValue[] args,out int ButtonId, out SciterValue callback, int startIndex = 0)
        {
            _entries.Clear();
            callback = null;
            ButtonId = 0;
            for (int i = startIndex; i < args.Length; i++)
            {
                var element = args[i];

                if(i == 0)
                {
                    ButtonId = element.AsInt32();
                    continue;
                }

                if (element.IsObjectFunction)
                {
                    callback = element;
                    continue;
                }

                var val = args[i].ToObject().ToString();

                ushort idx = startIndex == 0 ?
                    Convert.ToUInt16(i) : 
                    Convert.ToUInt16(i - 1);

                _entries.Add(new Tuple<ushort, string>(idx, val));
            }

            return _entries.ToArray();
        }
    }
}
