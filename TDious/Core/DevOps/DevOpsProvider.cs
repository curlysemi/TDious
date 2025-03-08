using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.OAuth;
using Microsoft.VisualStudio.Services.TestManagement.TestPlanning.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TDious.Core.DevOps
{
    class DevOpsProvider
    {
        public static async Task<List<DevOpsTask>> GetAllTasks()
        {
            var settings = await TDiousDataProvider.GetSettings();
            if (settings is null || settings.DevOpsUri is null || settings.DevOpsApiToken is null)
            {
                return new List<DevOpsTask>();
            }

            try
            {
                string q = settings.CustomWiql ??
                    "Select [State], [Title], [Completed Work] " +
                    "From WorkItems " +
                    "Where [Created By] = @Me " +
                    "And ( [Work Item Type] = 'Task' ) " +
                    "And [System.State] <> 'Closed' " +
                    "Order By [State] Desc, [Changed Date] Asc";

                //create a wiql object and build our query
                Wiql wiql = new Wiql()
                {
                    Query = q
                };

                using var connection = GetConnection(settings);
                using var witClient = connection.GetClient<WorkItemTrackingHttpClient>();
                var workItemQueryResult = await witClient.QueryByWiqlAsync(wiql);

                //some error handling
                if (workItemQueryResult.WorkItems.Count() != 0)
                {
                    //need to get the list of our work item id's and put them into an array
                    var list = new List<int>();
                    foreach (var item in workItemQueryResult.WorkItems)
                    {
                        list.Add(item.Id);
                    }
                    int[] arr = list.ToArray();

                    //build a list of the fields we want to see
                    string[] fields = new string[4];
                    fields[0] = "System.Id";
                    fields[1] = "System.Title";
                    fields[2] = "System.State";
                    var completedWorkKey = "Microsoft.VSTS.Scheduling.CompletedWork";
                    fields[3] = completedWorkKey;

                    //get work items for the id's found in query
                    var workItems = witClient.GetWorkItemsAsync(arr, fields, workItemQueryResult.AsOf).Result;

                    var info = workItems.Select(w =>
                    {
                        double completedWork = 0;
                        if (w.Fields.ContainsKey(completedWorkKey))
                        {
                            completedWork = (double)w.Fields[fields[3]];
                        }
                        var topic = new DevOpsTask
                        {
                            ID = (Int64)w.Fields["System.Id"],
                            Title = w.Fields["System.Title"].ToString(),
                            State = w.Fields["System.State"].ToString(),
                            TotalHours = completedWork,
                        };
                        return topic;
                    });

                    return info.Reverse().OrderBy(o => o.State).ToList();
                }

            }
            catch { } // TODO: Error handling (send messages to UI)

            return new List<DevOpsTask>();
        }

        public static async Task SaveCompletedHoursWithComment(long taskID, double newHours, string comment)
        {
            var settings = await TDiousDataProvider.GetSettings();
            if (settings is null || settings.DevOpsUri is null || settings.DevOpsApiToken is null)
            {
                return;
            }

            using var connection = GetConnection(settings);
            using var witClient = connection.GetClient<WorkItemTrackingHttpClient>();

            var id = Convert.ToInt32(taskID);
            var currentWorkItem = await witClient.GetWorkItemAsync(id);

            double oldHours = 0;
            var completedWorkKey = "Microsoft.VSTS.Scheduling.CompletedWork";
            if (currentWorkItem.Fields.ContainsKey(completedWorkKey))
            {
                oldHours = (double)currentWorkItem.Fields[completedWorkKey];
            }
            string hours = ConvertToTimeString(newHours - oldHours);
            string currentDescription = currentWorkItem.Fields["Microsoft.VSTS.Common.ItemDescription"]?.ToString() ?? string.Empty;
            string updatedDescription = currentDescription + Environment.NewLine + "<br />" + DateTime.Today.ToShortDateString() + " (" + hours + "): " + comment;

            // Create a patch document to replace the existing CompletedWork value
            JsonPatchDocument patchDocument =
            [
                new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Replace,
                    Path = "/fields/Microsoft.VSTS.Scheduling.CompletedWork",
                    Value = newHours
                },
                // Append another line to the Task description
                new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Replace,
                    Path = "/fields/Microsoft.VSTS.Common.ItemDescription",
                    Value = updatedDescription
                },
            ];

            // Update the work item
            var updatedWorkItem = await witClient.UpdateWorkItemAsync(patchDocument, id);
        }

        private static VssConnection GetConnection(TDiousSettings settings)
        {
            var uri = new Uri(settings.DevOpsUri!);
            var credentials = new VssOAuthAccessTokenCredential(settings.DevOpsApiToken);
            return new VssConnection(uri, credentials);
        }

        private static string ConvertToTimeString(double hours)
        {
            // Handle special cases first
            if (hours == 0.25)
                return "15 mins";
            if (hours == 0.5)
                return "1/2 hr";
            if (hours == 0.75)
                return "45 mins";
            if (hours == 1)
                return "1 hr";

            // Handle case for hours with 45 minutes (e.g., 1.75 => "1 hr 45 mins")
            if (hours % 1 == 0.75)
            {
                int fullHours = (int)hours;
                string hr = "hr";
                if (fullHours > 1)
                {
                    hr += "s";
                }
                return $"{fullHours} {hr} {45} mins";
            }

            // For hours greater than 1 or fractional, use this general approach
            if (hours % 1 == 0)
            {
                return $"{(int)hours} hrs";
            }

            // If the hours are a non-integer (like 1.5 or 2.5)
            return $"{hours} hrs";
        }
    }
}
