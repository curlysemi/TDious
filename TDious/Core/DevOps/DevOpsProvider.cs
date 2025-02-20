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
            var settings = TDiousDataProvider.GetSettings();
            if (settings == null)
            {
                return new List<DevOpsTask>();
            }

            try
            {
                //create a wiql object and build our query
                Wiql wiql = new Wiql()
                {
                    Query = "Select [State], [Title], [Completed Work] " +
                            "From WorkItems " +
                            "Where [Assigned to] = @Me " +
                            "And ( [Work Item Type] = 'Task' ) " +
                            //"And ( [Work Item Type] = 'Bug' Or [Work Item Type] = 'Work Request' ) " +
                            //"And [System.TeamProject] = '" + project + "' " +
                            "And [System.State] <> 'Closed' "
                             + "Order By [State] Desc, [Changed Date] Asc"
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

                    //if (currentTopicIdToExclude != null)
                    //{
                    //    workItems = workItems.Where(w => w.Fields["System.Id"].ToString() != currentTopicIdToExclude).ToList();
                    //}
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
                            Hours = completedWork,
                        };
                        return topic;
                    });

                    return info.Reverse().OrderBy(o => o.State).ToList();
                }

            }
            catch (Exception ex)
            {
            }

            return new List<DevOpsTask>();
        }

        public static async Task SaveCompletedHours(long taskID, double newHours)
        {
            var settings = TDiousDataProvider.GetSettings();
            if (settings == null)
            {
                return;
            }

            using var connection = GetConnection(settings);
            using var witClient = connection.GetClient<WorkItemTrackingHttpClient>();

            // Create a patch document to replace the existing CompletedWork value
            JsonPatchDocument patchDocument = new JsonPatchDocument
            {
                new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Replace,
                    Path = "/fields/Microsoft.VSTS.Scheduling.CompletedWork",
                    Value = newHours
                }
            };

            // Update the work item
            var id = Convert.ToInt32(taskID);
            var updatedWorkItem = await witClient.UpdateWorkItemAsync(patchDocument, id);
        }

        private static VssConnection GetConnection(TDiousSettings settings)
        {
            var uri = new Uri(settings.DevOpsUri);
            var credentials = new VssOAuthAccessTokenCredential(settings.DevOpsApiToken);
            return new VssConnection(uri, credentials);
        }
    }
}
