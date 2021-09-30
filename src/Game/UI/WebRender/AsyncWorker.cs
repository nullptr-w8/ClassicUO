using SciterCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClassicUO.Game.UI.WebRender
{
    public class AsyncWorker
    {
        private static event Action<int, SciterValue> requestResultCallBack;
        private TimeSpan w8Timer = TimeSpan.FromSeconds(5);
        public void requestResultCallback(int requestButtonId, SciterValue requestValue)
            => requestResultCallBack?.Invoke(requestButtonId, requestValue);

        public bool WaitForResult(int buttonId, out SciterValue value)
        {
            var waitForResponse = new AutoResetEvent(false);
            var requestResult = false;
            var _requestValue = SciterValue.Create();

            void callback(int requestButtonId, SciterValue requestValue)
            {
                if (buttonId == requestButtonId)
                {
                    waitForResponse.Set();
                    _requestValue = requestValue;
                    requestResult = true;
                }
            }
            requestResultCallBack += callback;
            waitForResponse.WaitOne(w8Timer);
            requestResultCallBack -= callback;
            value = _requestValue;

            return requestResult;
        }
    }
}
