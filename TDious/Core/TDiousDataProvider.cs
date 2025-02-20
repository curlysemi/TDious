﻿using LiteDB;
using Microsoft.VisualStudio.Services.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TDious.Core.DevOps;

namespace TDious.Core
{
    static class TDiousDataProvider
    {
        public static void SaveSettings(TDiousSettings settings)
        {
            using var db = new LiteDatabase(GetFilePath());
            var settingss = db.GetCollection<TDiousSettings>("settings");

            var existingSettings = GetSettings();
            if (existingSettings == null)
            {
                settingss.Insert(settings);
            }
            else
            {
                settings.ID = existingSettings.ID;
                settingss.Update(settings);
            }
        }

        public static TDiousSettings? GetSettings()
        {
            using var db = new LiteDatabase(GetFilePath());
            var settingss = db.GetCollection<TDiousSettings>("settings");
            return settingss?.Query()?.FirstOrDefault();
        }

        public static async Task<double> GetHoursCompletedToday(List<DevOpsTask> devOpsTasks)
        {
            using var db = new LiteDatabase(GetFilePath());
            var tasks = db.GetCollection<TDiousTask>("tasks");

            devOpsTasks = devOpsTasks ?? await DevOpsProvider.GetAllTasks();

            var tdTasks = tasks?.Query()?.ToList() ?? new List<TDiousTask>();
            var now = DateTime.Now;

            var oldCache = new List<TDiousTask>();
            var cachedToday = new List<TDiousTask>();
            if (tdTasks.Any())
            {
                foreach (var tdTask in tdTasks)
                {
                    if (tdTask.CacheDateTime.Date == now.Date)
                    {
                        cachedToday.Add(tdTask);
                    }
                    else
                    {
                        oldCache.Add(tdTask);
                    }
                }
            }

            var oldDevOps = new List<DevOpsTask>();
            var newDevOps = new List<DevOpsTask>();
            foreach (var doTask in devOpsTasks)
            {
                var oldTask = oldCache.FirstOrDefault(o => o.WorkItemID == doTask.ID);
                bool wasInOld = oldTask != null;
                var oldCached = cachedToday.FirstOrDefault(o => o.WorkItemID == doTask.ID);
                bool wasCachedToday = oldCached != null;
                if (wasInOld || wasCachedToday)
                {
                    oldDevOps.Add(doTask);
                }
                else if (!wasCachedToday)
                {
                    newDevOps.Add(doTask);
                }
            }

            if (tasks != null && (!tdTasks.Any() || newDevOps.Any() || oldCache.Any() || cachedToday.Any()))
            {
                IEnumerable<TDiousTask> getConverted(IEnumerable<DevOpsTask> doTasks, double? overrideHours = null)
                {
                    return doTasks.Select(t => new TDiousTask
                    {
                        WorkItemID = t.ID,
                        Hours = overrideHours ?? t.Hours,
                        CacheDateTime = now,
                    });
                }

                if (oldDevOps.Any() == true)
                {
                    double total = 0;

                    //// Delete all the items cached not today (previous days)
                    //if (oldCache.Any())
                    //{
                    //    tasks.DeleteMany(t => oldCache.Contains(t));
                    //}

                    if (oldDevOps.Any())
                    {
                        foreach (var doTask in oldDevOps)
                        {
                            // this _was_ cached at one point, but not today... So, not a _new_ task. Just need to update the cached hours and not count it
                            var oldCachedTask = oldCache.FirstOrDefault(o => o.WorkItemID == doTask.ID);
                            if (oldCachedTask != null)
                            {
                                oldCachedTask.Hours = doTask.Hours;
                                oldCachedTask.CacheDateTime = now;
                                tasks.Update(oldCachedTask);
                            }
                            else
                            {
                                var cachedTodayTask = cachedToday.FirstOrDefault(o => o.WorkItemID == doTask.ID);
                                if (cachedTodayTask != null)
                                {
                                    if (doTask.Hours > cachedTodayTask.Hours)
                                    {
                                        total += (doTask.Hours - cachedTodayTask.Hours);
                                    }
                                }
                            }
                        }
                    }
                    if (newDevOps.Any())
                    {
                        foreach (var doTask in newDevOps)
                        {
                            //if (cachedToday.Any())
                            //{
                                //// this wasn't cached before. If it has any hours and there were already items cached before, it should count to the new total
                                total += doTask.Hours;
                            //}
                            //// else: if there was nothing cached today, it's the first time running today, so we shouldn't count it (we're not going to bother loading the last time the work item was modified -- we just assume TDious was running before work started)
                        }
                        // these are new tasks. Cache as 0 hours so we credit them in later calculations
                        var converted = getConverted(newDevOps, overrideHours: 0);
                        foreach (var c in converted)
                        {
                            tasks.Insert(c);
                        }
                    }
                    return total;
                }
                else
                {
                    // first time running ever: cache all the tasks/hours. Doesn't count
                    tasks.DeleteAll();

                    var converted = getConverted(devOpsTasks);
                    foreach (var c in converted)
                    {
                        tasks.Insert(c);
                    }
                    return 0;
                }
            }
            else
            {
                double total = 0;
                foreach (var tdTask in tdTasks)
                {
                    var devOpsTask = devOpsTasks.FirstOrDefault(t => t.ID == tdTask.WorkItemID);
                    if (devOpsTask != null)
                    {
                        if (devOpsTask.Hours > tdTask.Hours)
                        {
                            total += (devOpsTask.Hours - tdTask.Hours);
                        }
                    }
                }
                return total;
            }
        }

        private static string GetFilePath()
        {
            var path = FileSystem.Current.AppDataDirectory;
            var filePath = Path.Combine(path, "tdious_settings.db");
            return $"Filename={filePath}; Connection=shared";
        }
    }
}
