using ClassicUO.Game.Managers;
using SciterCore;
using SDL2;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ClassicUO.Game.UI.WebRender
{
    public class WebRender : SciterEventHandler
    {
        [DllImport("user32.dll")]
        static extern IntPtr SetFocus(IntPtr hWnd);
        private uint _gumpId;
        private uint _localGumpID;
        private readonly SciterWindow _appHwnd;
        private readonly IntPtr _GameClientHwnd;
        private readonly IntPtr _GameClientSDL2Hwnd;
        private readonly Host _host;
        private readonly AsyncWorker _worker;
        private readonly uint[] switches = { };
        private readonly SciterValueWorker _unpacker = new SciterValueWorker();
        
        public WebRender(IntPtr hwnd,IntPtr sdlHwnd)
        { 
            _appHwnd = SciterWindow.CreateChildWindow(hwnd);
            _GameClientHwnd = hwnd;
            _GameClientSDL2Hwnd = sdlHwnd;
            _host = new Host();
            _worker = new AsyncWorker();
            _host.SetupWindow(_appHwnd).AttachEventHandler(this);
        }

        public void ShowBody(uint gumpID,uint localGumpID ,string body)
        {
            _gumpId = gumpID;
            _localGumpID = localGumpID;
            if(_appHwnd.TryLoadHtml(body))
            {
                _appHwnd.Show();
            }
            
        }
        
        public void onReceive(uint gumpID, string methodName, string Jobject)
        {
            if (_gumpId != gumpID)
                return;

            SciterValue sv = SciterValue.FromJsonString(Jobject);
            var callback = int.TryParse(methodName, out var number);

            if(callback)
            {
                _worker.requestResultCallback(Convert.ToInt32(methodName), sv);
            }
            else
            {
                _appHwnd.CallFunction(methodName, sv);
            }
        }
        public void sendGump(int buttonId, Tuple<ushort, string>[] entries)
        {
            GameActions.ReplyGump(_localGumpID, 
                _gumpId, buttonId, switches, entries);
        }

        public void GameFocus()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                SetFocus(_GameClientHwnd);
            }
            else
            {
                SDL2.SDL.SDL_RaiseWindow(_GameClientSDL2Hwnd);
            }
        }
        public void SEND(SciterValue[] args)
        {
            if (args.Length < 0)
                return;
           
            var getData = _unpacker.getData(args, out var buttonId, out var callback, 0);

            if(callback != null)
            { 
                Task.Run(() =>
                {
                    sendGump(buttonId, getData);
                    var result = _worker.WaitForResult(buttonId, out var Value);
                    if (result)
                    {
                        callback.Invoke(Value);
                    }
                });
            }
            else
            {
                sendGump(buttonId, getData);
            }
        }


        //protected override bool OnScriptCall(SciterElement se, string name, SciterValue[] args, out SciterValue result)
        //{
        //    result = null;

        //    if (args.Length < 0)
        //        return false;

        //    if(name == "Host_SEND")
        //    {
        //        var buttonId = args[0].Get(0);
        //        sendGump(buttonId, _unpacker.getData(args, 1));
        //        return true;
        //    }

        //    return false;
        //}
    }
         
}
