using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AlpineHR;

namespace tester
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                ScheduleMover obj = new ScheduleMover();
                var res = obj.MoveSchedules();
                Console.WriteLine(res == 0 ? "OK." : string.Format("There were {0} messages.", res));
            }
            catch (Exception e)
            {
                while (e != null)
                {
                    Console.WriteLine(e.Message);
                    e = e.InnerException;
                }
            }
#if DEBUG
            Console.ReadLine();
#endif
        }
    }
}
