using LiteDB;
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
        public static async Task SaveSettings(TDiousSettings? settings)
        {
            if (settings is null)
            {
                return;
            }

            using var db = new LiteDatabase(await GetConnectionString());
            var settingss = db.GetCollection<TDiousSettings>("settings");

            var existingSettings = await GetSettings();
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

        public static async Task<TDiousSettings?> GetSettings()
        {
            using var db = new LiteDatabase(await GetConnectionString());
            var settingss = db.GetCollection<TDiousSettings>("settings");
            return settingss?.Query()?.FirstOrDefault();
        }

        public static async Task<double> GetHoursCompletedToday(List<DevOpsTask> doTasks, DevOpsTask? timeTrackingTask, bool resetCache = false)
        {
            using var db = new LiteDatabase(await GetConnectionString());
            var tasks = db.GetCollection<TDiousTask>("tasks");

            List<DevOpsTask> devOpsTasks = new(doTasks);
            if (devOpsTasks is null || !devOpsTasks.Any())
            {
                (devOpsTasks, timeTrackingTask) = await DevOpsProvider.GetAllTasks();
            }

            if (timeTrackingTask is not null)
            {
                devOpsTasks.Insert(0, timeTrackingTask);
            }

            var tdTasks = tasks?.Query()?.ToList() ?? new List<TDiousTask>();
            var now = DateTime.Now;

            var oldCache = new List<TDiousTask>();
            var cachedToday = new List<TDiousTask>();
            if (tdTasks.Any())
            {
                foreach (var tdTask in tdTasks)
                {
                    if (tdTask.CacheDateTime.Date == now.Date && !resetCache)
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
                    return doTasks.Select(t => {
                        return new TDiousTask
                        {
                            WorkItemID = t.ID,
                            Hours = overrideHours ?? t.GetTrueTotalHours(),
                            CacheDateTime = now,
                        };
                    });
                }

                if (oldDevOps.Any() == true)
                {
                    double total = 0;

                    if (oldDevOps.Any())
                    {
                        foreach (var doTask in oldDevOps)
                        {
                            // this _was_ cached at one point, but not today... So, not a _new_ task. Just need to update the cached hours and not count it
                            var oldCachedTask = oldCache.FirstOrDefault(o => o.WorkItemID == doTask.ID);
                            if (oldCachedTask != null)
                            {
                                oldCachedTask.Hours = doTask.GetTrueTotalHours();
                                oldCachedTask.CacheDateTime = now;
                                tasks.Update(oldCachedTask);
                            }
                            else
                            {
                                var cachedTodayTask = cachedToday.FirstOrDefault(o => o.WorkItemID == doTask.ID);
                                if (cachedTodayTask != null)
                                {
                                    if (doTask.GetTrueTotalHours() > cachedTodayTask.Hours)
                                    {
                                        total += (doTask.GetTrueTotalHours() - cachedTodayTask.Hours);
                                    }
                                }
                            }
                        }
                    }
                    if (newDevOps.Any())
                    {
                        foreach (var doTask in newDevOps)
                        {
                            total += doTask.GetTrueTotalHours();
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
                        if (devOpsTask.GetTrueTotalHours() > tdTask.Hours)
                        {
                            total += (devOpsTask.GetTrueTotalHours() - tdTask.Hours);
                        }
                    }
                }
                return total;
            }
        }

        private const string PASS_KEY_NAME = "tdious_password";
        private static async Task<string> GetConnectionString()
        {
            var path = FileSystem.Current.AppDataDirectory;
            var filePath = Path.Combine(path, "tdious_settings.db");

            string? password = await SecureStorage.Default.GetAsync(PASS_KEY_NAME);
            if (password is null)
            {
                password = Guid.NewGuid().ToString();
                await SecureStorage.Default.SetAsync(PASS_KEY_NAME, password);
            }

            return $"Filename={filePath};Password={password};Connection=shared";
        }
    }
}
