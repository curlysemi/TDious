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
            if (tasks != null && (!tdTasks.Any() || tdTasks.All(t => t.CacheDateTime.Date != now.Date)))
            {
                IEnumerable<TDiousTask> getConverted(IEnumerable<DevOpsTask> doTasks)
                {
                    return doTasks.Select(t => new TDiousTask
                    {
                        WorkItemID = t.ID,
                        Hours = t.Hours,
                        CacheDateTime = now,
                    });
                }
                var old = new List<TDiousTask>();
                var newish = new List<TDiousTask>();
                foreach (var tdTask in tdTasks)
                {
                    if (tdTask.CacheDateTime == now.Date)
                    {
                        newish.Add(tdTask);
                    }
                    else
                    {
                        old.Add(tdTask);
                    }
                }
                var today = tdTasks.Where(t => t.CacheDateTime == now.Date);
                if (old.Any() == true)
                {
                    double total = 0;

                    tasks.DeleteMany(t => old.Contains(t));
                    var newTasks = new List<DevOpsTask>();
                    foreach (var doTask in devOpsTasks)
                    {
                        var oldTask = old.FirstOrDefault(o => o.WorkItemID == doTask.ID);
                        bool wasInOld = oldTask != null;
                        if (wasInOld)
                        {
                            // this _was_ cached at one point, but not today... So, not a _new_ task. Just need to update the cached hours and not count it
                            oldTask!.Hours = doTask.Hours;
                            tasks.Update(oldTask);
                        }
                        else
                        {
                            if (newish.Any())
                            {
                                // this wasn't cached before. If it has any hours and there were already items cached before, it should count to the new total
                                total += doTask.Hours;
                            }
                            // if there was nothing cached today, it's the first time running today, so we shouldn't count it (we're not going to bother loading the last time the work item was modified -- we just assume TDious was running before work started)
                            newTasks.Add(doTask);
                        }
                    }
                    var converted = getConverted(newTasks);
                    foreach (var c in converted)
                    {
                        tasks.Insert(c);
                    }
                    return total;
                }
                else
                {
                    // first time running ever: cache all the tasks/hours. Doesn't count
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
