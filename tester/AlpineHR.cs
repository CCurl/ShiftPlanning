using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ShiftPlannerLib;
using TimeAmericaLib;
using TimeAmericaLib.TimeAmericaSoap;

namespace AlpineHR
{
    public class ScheduleMover
    {
        public ScheduleMover()
        {
            this.settings = new Settings("settings.ini");
            taSchedules = new List<ScheduleItem>();
            this.Errors = new List<string>();
            this.InfoMessages = new List<string>();
            CollectError("No errors detected.");
        }

        #region [Member variables]
        private Settings settings;
        private List<ScheduleItem> taSchedules;
        private bool detailedInfo;
        private int payType;

        TimeAmerica timeAmerica;
        string taKey;
        string taEndpoint;
        #endregion

        /// <summary>
        /// 
        /// </summary>
        public int MoveSchedules()
        {
            try
            {
                DateTime now = DateTime.Now;
                ErrorFile = string.Format("errors-{0,2:D4}-{1,2:D2}-{2,2:D2}.txt", now.Year, now.Month, now.Day);
                InfoFileName = string.Format("info-{0,2:D4}-{1,2:D2}-{2,2:D2}.txt", now.Year, now.Month, now.Day);

                string ptSetting = settings.GetValue("TimeAmerica", "PayType");
                if (!int.TryParse(ptSetting, out payType))
                {
                    CollectMessage(string.Format("[TimeAmerica]-PayType setting '{0}' is invalid or missing, defaulting to '0'.", ptSetting), false);
                    payType = 0;
                }

                string dumpDetails = settings.GetValue("General", "DetailedInfo");
                detailedInfo = (dumpDetails == "true");

                CollectMessage(string.Format("run start: {0}", now.ToString()), false);
                
                if (settings.GetValue("ShiftPlanner", "key") == string.Empty)
                {
                    CollectError("File 'settings.ini' missing ShiftPlanner entries.");
                }

                if (settings.GetValue("TimeAmerica", "key") == string.Empty)
                {
                    CollectError("File 'settings.ini' missing TimeAmerica entries.");
                }

                if (Errors.Count > 1)
                {
                    throw new Exception("Cannot continue.");
                }

                string strNumDays = settings.GetValue("General", "NumDays");
                if (string.IsNullOrEmpty(strNumDays))
                { 
                    strNumDays = "7"; 
                }
                bool isFullDay = settings.GetValue("General", "FullDay").ToLower().CompareTo("true") == 0;
                int numDays = int.Parse(strNumDays);
                DateTime fromDate = DateTime.Now;
                DateTime thruDate;

                if (numDays == 1 && isFullDay)
                {
                    fromDate = DateTime.Parse(fromDate.ToShortDateString());
                    string toDate = string.Format("{0} 23:59:59", fromDate.ToShortDateString());
                    thruDate = DateTime.Parse(toDate);
                }
                else
                {
                    thruDate = fromDate.AddDays(numDays);
                }

                CollectMessage(string.Format("From: {0}, thru: {1}", fromDate.ToString(), thruDate.ToString()), false);

                ClearTimeAmericaSchedules(fromDate, thruDate);

                var spSchedules = this.GetSchedulesFromShiftPlanner(fromDate, thruDate);
                TranslateSchedules(spSchedules);
                SendSchedulesToTimeAmerica();
            }
            catch(Exception e)
            {
                CollectError(e.Message);
            }

            CollectMessage(string.Format("run end: {0}", DateTime.Now.ToString()), false);
            WriteErrors();
            WriteMessages();
            return Errors.Count;
        }

        #region [Shift Planning]
        private APIResponse GetSchedulesFromShiftPlanner(DateTime fromDate, DateTime thruDate)
        {
            var myKey = settings.GetValue("ShiftPlanner", "key");
            var myUser = settings.GetValue("ShiftPlanner", "user");
            var myPassword = settings.GetValue("ShiftPlanner", "pw");

            ShiftPlanning shiftPlanner = new ShiftPlanning(myKey);
            RequestFields reqFields = new RequestFields();
            reqFields.Add("username", myUser);
            reqFields.Add("password", myPassword);

            var loginStatus = shiftPlanner.doLogin(reqFields);
            if (loginStatus.Status.Code != "1")
            {
                CollectError(string.Format("Error {0} logging into ShiftPlanning: {1}: {2}", loginStatus.Status.Code, loginStatus.Status.Error, loginStatus.Status.Text));
                CollectError(string.Format(" - user [{0}], pw [{1}], key [{2}]", myUser, myPassword, myKey));
                throw new Exception("Cannot continue.");
            }

            //var workSchedules = shiftPlanner.getSchedules();
            var filter = new RequestFields();

            filter.Add("start_date", string.Format("{0}/{1}/{2} 00:00:00", fromDate.Month, fromDate.Day, fromDate.Year));
            filter.Add("end_date", string.Format("{0}/{1}/{2} 23:59:59", thruDate.Month, thruDate.Day, thruDate.Year));
            filter.Add("type", "schedule_summary");
            var shifts = shiftPlanner.getReportsSchedule(filter);
            if (shifts.Status.Code != "1")
            {
                CollectError(string.Format("Error {0} calling ShiftPlanner.getReportsSchedule(): {1}", shifts.Status.Code, shifts.Status.Error));
                throw new Exception("Cannot continue.");
            }
            shiftPlanner.doLogout();
            return shifts;
        }
        #endregion

        private void TranslateSchedules(APIResponse spSchedules)
        {
            ScheduleItem taSched = new ScheduleItem();
            StringBuilder sb = new StringBuilder();
            foreach (var item in spSchedules.Data)
            {
                //taSched.Sched
                //var fields = emp.Value.Item;

                //foreach (var fld in fields)
                //{
                ParseSP(sb, item, 0, ref taSched);
                    //ret.Add(emp.Key, fieldVal);
                //}
                //sb.Append("\n");
            }

            //string tmp = sb.ToString();
            //File.WriteAllText(@"..\..\..\FromShiftPlanner.txt", tmp);

            //throw new Exception("TranslateSchedules() not complete.");
        }

        void ParseSP(StringBuilder sb, KeyValuePair<string, DataItem> fld, int level, ref ScheduleItem taSched)
        {
            //string pre = new string(' ', level * 2);
            if (fld.Value == null)
            {
                //sb.AppendFormat("{0}<{1}></{1}>\n", pre, fld.Key);
                return;
            }
            if (fld.Value.Item == null)
            {
                //sb.AppendFormat("{0}<{1}></{1}>\n", pre, fld.Key);
                return;
            }
            if (fld.Value.Item.Count == 1)
            {
                if (!string.IsNullOrEmpty(fld.Value.Value))
                {
                    string fn = fld.Key, val = fld.Value.Value;
                    //sb.AppendFormat("{0}<{1}>{2}</{1}>\n", pre, fld.Key, fld.Value.Value);
                    if (fld.Key == "name")
                    {
                        taSched.EmployeeNumber = string.Empty;
                        taSched.Start = DateTime.MinValue;
                        taSched.Stop = DateTime.MinValue;
                    }
                    if (fld.Key == "eid")
                    {
                        taSched.EmployeeNumber = val;
                    }
                    else if (fld.Key == "start_timestamp")
                    {
                        taSched.Start = DateTime.Parse(val);
                    }
                    else if (fld.Key == "end_timestamp")
                    {
                        taSched.Stop = DateTime.Parse(val);
                        CheckScheduleItem(ref taSched);
                    }
                }
            }
            else if (fld.Value.Item.Count > 1)
            {
                //sb.AppendFormat("{0}<{1}>\n", pre, fld.Key);
                foreach (var f1 in fld.Value.Item)
                {
                    //sb.AppendFormat("{0}", pre);
                    ParseSP(sb, f1, level + 1, ref taSched);
                    //sb.AppendFormat("{0}", pre);
                }
                //sb.AppendFormat("{0}</{1}>\n", pre, fld.Key);
                //CheckScheduleItem(ref taSched);
            }
            else
            {
            }
        }

        #region [Error/Info messages]
        private List<string> Errors { get; set; }
        private List<string> InfoMessages { get; set; }
        private string InfoFileName { get; set; }
        private string ErrorFile { get; set; }

        void CollectError(string Message)
        {
            if (this.Errors.Count == 1)
            {
                this.Errors.Clear();
                this.Errors.Add(string.Format("*** {0} ***", DateTime.Now.ToString()));
            }

            this.Errors.Add(Message);
        }
        
        void CollectMessage(string Message, bool isDetailed)
        {
            if ((isDetailed == false) || (detailedInfo == true))
            {
                this.InfoMessages.Add(Message);
            }
        }

        void WriteErrors()
        {
            if (!string.IsNullOrEmpty(ErrorFile))
            {
                File.AppendAllLines(ErrorFile, this.Errors);
            }
        }

        void WriteMessages()
        {
            if (!string.IsNullOrEmpty(InfoFileName))
            {
                File.AppendAllLines(InfoFileName, this.InfoMessages);
            }
        }
        #endregion

        #region [Time America]
        void TimeAmericaInit()
        {
            if (timeAmerica == null)
            {
                taKey = settings.GetValue("TimeAmerica", "key");
                taEndpoint = settings.GetValue("TimeAmerica", "endpoint");
                timeAmerica = new TimeAmerica(taKey, taEndpoint);
            }
        }

        private bool CheckScheduleItem(ref ScheduleItem taSched)
        {
            try
            {
                if (string.IsNullOrEmpty(taSched.EmployeeNumber))
                    return false;
                if (taSched.Start == DateTime.MinValue)
                    return false;
                if (taSched.Stop == DateTime.MinValue)
                    return false;

                taSched.UserId = 0;
                taSched.PayTypeId = payType;
                taSched.LaborLevelDetailIds = new ArrayOfInt();
                //taSched.LaborLevelDetailIds.Add(0);

                taSchedules.Add(taSched);

                taSched = new ScheduleItem();
                taSched.Start = DateTime.MinValue;
                taSched.Stop = DateTime.MinValue;

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void ClearTimeAmericaSchedules(DateTime fromDate, DateTime thruDate)
        {
            TimeAmericaInit();
            if (!timeAmerica.ClearDates(fromDate, thruDate))
            {
                CollectError(timeAmerica.LastStatus);
            }
        }

        private void SendSchedulesToTimeAmerica()
        {
            TimeAmericaInit();
            ScheduleItem[] oneSchedule = new ScheduleItem[1];

            CollectMessage(string.Format("There are {0} schedules to send.", taSchedules.Count), false);

            foreach (var taSched in taSchedules)
            {
                oneSchedule[0] = taSched;
                var taInfo = string.Format("EID [{0}] Start [{1}] Stop [{2}], PayTypeId [{3}]", taSched.EmployeeNumber, taSched.Start, taSched.Stop, taSched.PayTypeId);
                CollectMessage(taInfo, true);
                if (!timeAmerica.SendSchedules(oneSchedule))
                {
                    CollectError(string.Format("TimeAmerica.SendSchedules(): {0}", taInfo));
                    CollectError(string.Format("- Error: {0}", timeAmerica.LastStatus));
                }
            }
        }
        #endregion

    }
}
