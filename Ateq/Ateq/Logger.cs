using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ateq
{
    public static class Logger
    {
        static readonly object _locker = new object();

        public static void Log(string logMessage, string filen, string sn, bool res_bool)
        {
            try
            {
                var logFilePath = filen;
               
                //WriteToLog(logMessage, logFilePath);
            }
            catch (Exception ex)
            {
                
            }
        }
        /*
        static void WriteToLog(string logMessage, string logFilePath)
        {
            lock (_locker)
            {

                txtWriter.WriteLine("-- S/N: {0}", sernum);
                if (flagit)
                    txtWriter.WriteLine("-- Result: PASSED\n");
                else
                    txtWriter.WriteLine("-- Result: FAILED\n");

                txtWriter.WriteLine(" \n", logMessage);
                File.AppendAllText(logFilePath,
                        string.Format("-- S/N: {0}\n-- Result: PASSED\n",
                        Environment.NewLine, DateTime.Now.ToLongDateString(),
                        DateTime.Now.ToLongTimeString(), logMessage));
            }
        }*/
    }
}
