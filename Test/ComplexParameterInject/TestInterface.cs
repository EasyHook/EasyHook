using System;
using System.Collections.Generic;
using System.Text;

namespace ComplexParameterInject
{
    public class TestInterface : MarshalByRefObject
    {
        public void IsInstalled(Int32 InClientPID)
        {
            Console.WriteLine("ComplexParameterInject has been installed in target {0}.\r\n", InClientPID);
        }

        public void ReportException(Exception InInfo)
        {
            Console.WriteLine("The target process has reported an error:\r\n" + InInfo.ToString());
        }

        public void SendMessage(string message)
        {
            Console.WriteLine("Message from client: " + message);
        }

        public void Ping()
        {
        }
    }
}
