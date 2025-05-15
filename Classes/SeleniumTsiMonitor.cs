using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium;
using SeleniumExtras.WaitHelpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TSI_Monitor.Classes
{
    static class SeleniumTsiMonitor
    {
        public static string OpenedTickets
        { get
            {
                if (DateTime.Now - LastUpdate > TimeSpan.FromMinutes(10))
                {
                    _OpenTSI_Tickets = GetOpenedTickets();
                    LastUpdate = DateTime.Now;
                    return _OpenTSI_Tickets;
                }
                else
                {
                    return _OpenTSI_Tickets;
                }
            }
            
            private set { } 
        }

        private static string _OpenTSI_Tickets;
        private static DateTime LastUpdate;


        private const string Url = "https://jira.puls.ru/secure/Dashboard.jspa?selectPageId=15003";
        private const string TableCssSelector = "#qrf-table-view-23015 > table";

        public static string GetOpenedTickets()
        {
            if (DateTime.Now - LastUpdate < TimeSpan.FromMinutes(10))
            {
                return _OpenTSI_Tickets;
            }

            WebDriver driver = null;
            DataTable TicketsDTable = InitializeTable();
            try
            {
                ChromeOptions chromeOptions = new ChromeOptions();
                ChromeDriverService driverService = ChromeDriverService.CreateDefaultService();

                driverService.HideCommandPromptWindow = true;
                chromeOptions.AddArguments("--headless=new"); // comment out for testing

                driver = new ChromeDriver(driverService, chromeOptions);


                driver.Navigate().GoToUrl(Url);
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(60));
                var ipTextBox = wait.Until(ExpectedConditions.ElementExists(By.CssSelector(TableCssSelector)));
                Thread.Sleep(2000);

                IWebElement ticketTable = driver.FindElement(By.CssSelector(TableCssSelector));

                if (ticketTable == null)
                    return "Ошибка получения таблицы";

                var ticketRows = ticketTable.FindElements(By.TagName("tr"));

                foreach (var trow in ticketRows.Skip(1))
                {
                    var text = trow.Text.Replace("Светашов Евгений Викторович", "РИТ").Replace("Зверев Михаил Дмитриевич", "Админ");
                    var param = text.Split('\n');

                    if (text == "No issues found.")
                    {
                        return "Нет открытых задач TSI";
                    }

                    DataRow row = TicketsDTable.NewRow();
                    row[0] = param[0]; // code
                    row[1] = param[1]; // theme
                    row[2] = param[2]; // status + assigner
                    row[3] = param[3]; // timeout
                    row[4] = param[4]; // creator

                    TicketsDTable.Rows.Add(row);
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error("Ошибка в методе  SeleniumMonitorTSI.GetOpenTSITickets: " + ex.ToString());
                return "Возникла ошибка. Смотрите лог файл";
            }
            finally
            {
                driver.Close();
                driver.Quit();
            }

            _OpenTSI_Tickets = ConvertToString(TicketsDTable);
            return _OpenTSI_Tickets;
        }

        private static DataTable InitializeTable()
        {
            DataTable dt = new DataTable();

            DataColumn column1 = new DataColumn("Код", typeof(string));
            DataColumn column2 = new DataColumn("Тема", typeof(string));
            DataColumn column3 = new DataColumn("Статус + Исполнитель", typeof(string));
            DataColumn column4 = new DataColumn("Время до решения", typeof(string));
            DataColumn column5 = new DataColumn("Автор", typeof(string));

            dt.Columns.Add(column1);
            dt.Columns.Add(column2);
            dt.Columns.Add(column3);
            dt.Columns.Add(column4);
            dt.Columns.Add(column5);

            return dt;
        }

        private static DataTable MsgToDataRow(DataTable dataTable, string msg)
        {
            DataRow row = dataTable.NewRow();
            row[0] = msg;
            dataTable.Rows.Add(row);

            return dataTable;
        }

        private static string ConvertToString(DataTable dataTable)
        {
            string result = "";
            foreach (DataRow row in dataTable.Rows)
            {
                foreach (var item in row.ItemArray)
                {
                    result += item.ToString() + "|"; // Используем табуляцию для разделения
                }
                result += "\n\n"; // Переход на новую строку
            }
            return result;
        }
    }
}
