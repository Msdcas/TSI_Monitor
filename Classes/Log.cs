using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TSI_Monitor.Classes
{
    static class Log
    {
        public static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public static void Initialize()
        {
            try
            {
                LogManager.LoadConfiguration("nlog.config");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Logger error initialization: " + ex.Message);
                Logger.Error(ex, "Logger error initialization");
            }
        }

    }
}
