﻿using Autofac;
using QuantitativeAnalysis.ModelLayer.Option;
using QuantitativeAnalysis.ServiceLayer.DataProcessing.Option;
using QuantitativeAnalysis.ServiceLayer.MyCore;
using QuantitativeAnalysis.Utilities.Common;
using QuantitativeAnalysis.Utilities.DataApplication;
using QuantitativeAnalysis.Utilities.OptionUtils_50ETF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantitativeAnalysis.ApplicationLayer.DataProcessingSystem.OptionTickDataProcessing
{
    public class OptionTickDataArrangement
    {
        private DateTime startDate, endDate;
        private List<DateTime> tradeDays = new List<DateTime>();
        private List<OptionInfo> optionInfoList = new List<OptionInfo>();
        private string sourceServer;
        private string targetServer;
        private string dataBase;
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="startDate">开始时间</param>
        /// <param name="endDate">结束时间</param>
        public OptionTickDataArrangement(int startDate,int endDate,string sourceServer="local",string targetServer="local",string dataBase="optionTickData")
        {

            this.startDate = Kit.ToDate(startDate);
            this.endDate = Kit.ToDate(endDate);
            this.tradeDays = DateUtils.GetTradeDays(startDate, endDate);
            this.optionInfoList = (List<OptionInfo>)Caches.get("OptionInfo_510050.SH");
            this.sourceServer = sourceServer;
            this.targetServer = targetServer;
            this.dataBase = dataBase;
            save50ETFOptionData();
        }

        private void save50ETFOptionData()
        {
            //逐日进行检查
            foreach (var date in tradeDays)
            {
                var list = OptionUtils_50ETF.getOptionListByDate(optionInfoList,Kit.ToInt(date));
                //逐合约进行检查
                foreach (var option in list)
                {
                    //先检查目标数据库中存在的数据
                    string tableName = "MarketData_"+option.optionCode.Replace('.', '_');
                    int numbers = checkTargetDataTable(date, tableName);
                   if (numbers>0) 
                    {
                        var data= OptionTickDataUtils.filteringTickData(Platforms.container.Resolve<OptionTickDataDailyStoringService>().fetchFromMssql(option.optionCode, date));
                        //var data2 = Platforms.container.Resolve<OptionTickDataDailyService>().fetchFromMssql(option.optionCode, date);
                        if (data != null && data.Count > 0)
                        {

                            string connStr = MSSQLUtils.GetConnectionString(targetServer) + "database=" + dataBase + ";";
                            MSSQLUtils.OptionDataBulkToMSSQL(connStr, DataTableUtils.ToDataTable(data), tableName);
                        }
                    }
                }
                Console.WriteLine("Date:{0} Complete!", date.ToString("yyyyMMdd"));
            }
        }


        private int checkTargetDataTable(DateTime date,string tableName)
        {

            string connStr = MSSQLUtils.GetConnectionString(targetServer);
            //数据库不存在
            if (MSSQLUtils.CheckDataBaseExist(dataBase,connStr)==false)
            {
                MSSQLUtils.CreateDataBase(connStr, getCreateDataBaseString(dataBase));
                string connTableStr = connStr + "database=" + dataBase + ";";
                MSSQLUtils.CreateTable(connTableStr, getCreateTableString(tableName));
                return 0;
            }
            //表不存在
            else if (MSSQLUtils.CheckExist(dataBase,tableName,connStr)==false)
            {
                string connTableStr = connStr + "database=" + dataBase + ";";
                MSSQLUtils.CreateTable(connTableStr, getCreateTableString(tableName));
                return 0;
            }
            //表存在判断数据是否存在
            else
            {
                string connTableStr = connStr + "database=" + dataBase + ";";
                string sqlStr = string.Format(@"select count(*) from {0} where tdate={1}", tableName, date.ToString("yyyyMMdd"));
                int number = MSSQLUtils.getNumbers(tableName, connTableStr, sqlStr);
                return number;
            }
        }

        private bool initialization(string dataBase,string tableName)
        {
            bool success = false;
            try
            {

                success = true; 
            }
            catch (Exception)
            {

                throw;
            }
            return success;
        }

        private string getCreateDataBaseString(string dataBaseName)
        {
            return "CREATE DATABASE " + dataBaseName + " ON PRIMARY (NAME = '" + dataBaseName + "', FILENAME = 'D:\\HFDB\\" + dataBaseName + ".dbf',SIZE = 1024MB,MaxSize = 512000MB,FileGrowth = 1024MB) LOG ON (NAME = '" + dataBaseName + "Log',FileName = 'D:\\HFDB\\" + dataBaseName + ".ldf',Size = 20MB,MaxSize = 1024MB,FileGrowth = 10MB)";
        }

        private string getCreateTableString(string tableName)
        {
            return "CREATE TABLE [dbo].[" + tableName + "]([code] [char](11) NOT NULL,[tdate] [int] NOT NULL," +
                    "[ttime] [int] NOT NULL,[lastPrice] [decimal](12,4) NULL,[ask1] [decimal](12,4) NULL,[ask2] [decimal](12,4) NULL," +
                    "[ask3] [decimal](12,4) NULL,[ask4] [decimal](12,4) NULL,[ask5] [decimal](12,4) NULL,[bid1] [decimal](12,4) NULL," +
                    "[bid2] [decimal](12,4) NULL,[bid3] [decimal](12,4) NULL,[bid4] [decimal](12,4) NULL,[bid5] [decimal](12,4) NULL," +
                    "[askv1] [decimal](10, 0) NULL,[askv2] [decimal](10, 0) NULL,[askv3] [decimal](10, 0) NULL,[askv4] [decimal](10, 0) NULL," +
                    "[askv5] [decimal](10, 0) NULL,[bidv1] [decimal](10, 0) NULL,[bidv2] [decimal](10, 0) NULL,[bidv3] [decimal](10, 0) NULL," +
                    "[bidv4] [decimal](10, 0) NULL,[bidv5] [decimal](10, 0) NULL,[volume] [decimal](20, 0) NULL,[amount] [decimal](20, 3) NULL," +
                    "[openInterest] [decimal](20, 0) NULL,[preClose] [decimal](12,4) NULL,[preSettle] [decimal](12,4) NULL,CONSTRAINT[PK_" + tableName + "] " +
                    "PRIMARY KEY NONCLUSTERED([code] ASC,[tdate] ASC,[ttime] ASC) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, " +
                    "IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON[PRIMARY]) ON [PRIMARY] CREATE CLUSTERED " +
                    "INDEX[IX_" + tableName + "_TDATE] ON[dbo].[" + tableName + "]([tdate] ASC,[ttime] ASC)WITH(PAD_INDEX = OFF, " +
                    "STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, " +
                    "ALLOW_PAGE_LOCKS = ON) ON[PRIMARY]";
        }
    }
}
