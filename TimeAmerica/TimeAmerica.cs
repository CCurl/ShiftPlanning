using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeAmericaLib.TimeAmericaSoap;

namespace TimeAmericaLib
{
    public class TimeAmerica
    {
        private Guid Key { get; set; }
        private string EndPoint { get; set; }
        public string LastStatus { get; private set; }
        SchedulesSoapClient soapClient;

        public TimeAmerica(string key, string endPoint)
        {
            this.Key = Guid.Parse(key);
            this.EndPoint = endPoint;
        }

        private void CheckSoapClient()
        {
            if (soapClient == null)
            {
                soapClient = new SchedulesSoapClient(EndPoint);
            }

            if (soapClient == null)
            {
                throw new Exception("Unable to create TimeAmerica soap client");
            }
        }

        public bool ClearDates(DateTime fromDate, DateTime thruDate)
        {
            try
            {
                CheckSoapClient();
                this.LastStatus = string.Empty;
                if (!soapClient.ClearAll(this.Key, fromDate, thruDate))
                {
                    throw new Exception("TimeAmericaSoap.ClearDates() returned false.");
                }
                return true;
            }
            catch (Exception e)
            {
                this.LastStatus = e.Message;
            }
            return false;
        }

        public bool SendSchedules(ScheduleItem[] taSchedules)
        {
            try
            {
                CheckSoapClient();
                this.LastStatus = string.Empty;
                if (!soapClient.AddSchedules(this.Key, taSchedules))
                {
                    throw new Exception("TimeAmericaSoap.AddSchedules() returned false.");
                }
                return true;
            }
            catch (Exception e)
            {
                this.LastStatus = e.Message;
            }
            return false;
        }
    }
}
