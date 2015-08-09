using EasyHook;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SimpleHook1
{
    public class EntryPoint : EasyHook.IEntryPoint
    {
        public EntryPoint(RemoteHooking.IContext InContext)
        {
        }

        public void Run(RemoteHooking.IContext InContext)
        {
            //while (true)
            {
                System.Threading.Thread.Sleep(30000);
            }
        }
    }
}
