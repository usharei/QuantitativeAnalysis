﻿using NLog;
using QuantitativeAnalysis.DataAccessLayer.Common;
using QuantitativeAnalysis.ServiceLayer.MyCore;
using QuantitativeAnalysis.Utilities.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantitativeAnalysis.ServiceLayer.DataProcessing.Common
{
    public abstract class BasicDataService<T> where T : new()
    {
        static Logger log = LogManager.GetCurrentClassLogger();

        protected abstract List<T> readFromWind(string code,DateTime startDate,DateTime endDate);

        protected abstract void saveToLocalCsvFile(IList<T> data, string path, bool appendMode = false, string tag = null);

        protected abstract List<T> readFromLocalCsv(String path);

        protected abstract void saveToSQLServer(IList<T> data,string serviceName, string dataBaseName, string tableName, string sqlStr);

        /// <summary>
        /// 核心存储程序
        /// </summary>
        /// <param name="appendMode">是否为append模式，否则为new模式</param>
        /// <param name="localCsvExpiration">CacheData中本地csv文件的保鲜期（天数）</param>
        /// <param name="tag"></param>
        public virtual List<T> fetchFromLocalCsvOrWindAndSaveAndCache(int localCsvExpiration=5, bool appendMode = false, String tag = null, string code=null, DateTime startDate=new DateTime(), DateTime endDate=new DateTime())
        {
            
            if (tag == null) tag = typeof(T).Name;
            List<T> data = null;
            var filePathPattern = _buildCacheDataFilePath(tag,code, "*");
            var todayFilePath = _buildCacheDataFilePath(tag, code,DateTime.Now.ToString("yyyyMMdd"));
            var dirPath = Path.GetDirectoryName(filePathPattern);
            if (Directory.Exists(dirPath) == false)
            {
                Directory.CreateDirectory(dirPath);
            }
            var fileNamePattern = Path.GetFileName(filePathPattern);
            var allFilePaths = Directory.EnumerateFiles(dirPath, fileNamePattern)
                .OrderByDescending(fn => fn).ToList();

            var lastestFilePath = (allFilePaths == null || allFilePaths.Count == 0) ? null : allFilePaths[0];
            var daysdiff = FileUtils.GetCacheDataFileDaysPastTillToday(lastestFilePath);
            if (daysdiff > localCsvExpiration && Caches.WindConnection == true)
            {   //CacheData太旧，需要远程更新，然后保存到本地CacheData目录
                var txt = (daysdiff == int.MaxValue) ? "不存在" : "已过期" + daysdiff + "天";
                log.Info("本地csv文件{0}，尝试Wind读取新数据...", txt);
                try
                {
                    data = readFromWind(code,startDate,endDate);
                }
                catch (Exception e)
                {
                    log.Error(e, "从Wind读取数据失败！");

                }

                log.Info("正在保存新数据到本地...");
                try
                {
                    if (lastestFilePath == null)
                    {   //新增                        
                        saveToLocalCsvFile(data, todayFilePath, appendMode, tag);
                        log.Debug("文件{0}已保存.", todayFilePath);
                    }
                    else
                    {   //修改
                        saveToLocalCsvFile(data, lastestFilePath, appendMode, tag);
                        //重命名为最新日期
                        File.Move(lastestFilePath, todayFilePath);
                        log.Debug("文件重命名为{0}", todayFilePath);
                    }
                }
                catch (Exception e)
                {
                    log.Error(e);
                }

            }
            else
            {   //CacheData不是太旧，直接读取
                log.Info("正在从本地csv文件{0}读取数据... ", lastestFilePath);
                try
                {

                    data = readFromLocalCsv(lastestFilePath);
                }
                catch (Exception e)
                {
                    log.Error(e, "从本地csv文件读取数据失败！");
                }

            }
            if (data != null)
            {
                //加载到内存缓存
                if (tag!="TradeDays")
                {
                    tag = tag + '_' + code;
                }
                Caches.put(tag, data);
                log.Info("已将{0}加载到内存缓存.", tag);
                log.Info("获取{0}数据列表成功.共{1}行.", tag, data.Count);
            }
            else
            {
                log.Warn("没有任何内容可以缓存！");
            }

            return data;
        }

        private static string _buildCacheDataFilePath(string tag, string code, string date)
        {
            if (tag == null) tag = typeof(T).ToString();
            if (tag == "TradeDays")
            {
                return FileUtils.GetCacheDataFilePath("CacheData.Path.TradeDays", new Dictionary<string, string>
                {
                    ["{tag}"] = tag,
                    ["{date}"] = date
                });
            }
            return FileUtils.GetCacheDataFilePath("CacheData.Path.Basic", new Dictionary<string, string>
            {
                ["{tag}"] = tag,
                ["{code}"] = code,
                ["{date}"] = date
            });
        }
    }
}
