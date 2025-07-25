﻿using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.OAuth;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;

namespace TDious.Core.DevOps
{
    class DevOpsProvider
    {
        public static async Task<(List<DevOpsTask> allTasks, DevOpsTask? timeTrackingTask)> GetAllTasks()
        {
            var settings = await TDiousDataProvider.GetSettings();
            if (settings is null || settings.DevOpsUri is null || settings.DevOpsApiToken is null)
            {
                return ([], null);
            }

            try
            {
                string q = settings.CustomHomeWiql ??
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
                    int[] arr = [.. list];

                    //build a list of the fields we want to see
                    string[] fields = new string[4];
                    fields[0] = "System.Id";
                    fields[1] = "System.Title";
                    fields[2] = "System.State";
                    var completedWorkKey = "Microsoft.VSTS.Scheduling.CompletedWork";
                    fields[3] = completedWorkKey;

                    //get work items for the id's found in query
                    var workItems = witClient.GetWorkItemsAsync(arr, fields, workItemQueryResult.AsOf).Result;

                    DevOpsTask? timeTrackingTask = null;

                    if (!string.IsNullOrWhiteSpace(settings.TrackingTitle))
                    {
                        var timeTracking = workItems.FirstOrDefault(w => w.Fields["System.Title"].ToString() == settings.TrackingTitle);
                        if (timeTracking is not null)
                        {
                            workItems.Remove(timeTracking);

                            timeTracking = await witClient.GetWorkItemAsync(Convert.ToInt32(timeTracking.Fields["System.Id"]), expand: WorkItemExpand.All);
                            if (timeTracking is not null)
                            {
                                timeTrackingTask = new DevOpsTask
                                {
                                    ID = (long)timeTracking.Fields["System.Id"],
                                    Title = timeTracking.Fields["System.Title"].ToString(),
                                    State = timeTracking.Fields["System.State"].ToString(),
                                    TotalIssueResearchTime = GetDoubleField(timeTracking.Fields, Constants.IssueResearchTime),
                                    TotalMeetingsTime = GetDoubleField(timeTracking.Fields, Constants.MeetingsTime),
                                    TotalSupportTime = GetDoubleField(timeTracking.Fields, Constants.SupportTime),
                                    TotalItemReviewTime = GetDoubleField(timeTracking.Fields, Constants.ItemReviewTime),
                                    TotalRequirementsTime = GetDoubleField(timeTracking.Fields, Constants.RequirementsTime),
                                    TotalInfrastructureTime = GetDoubleField(timeTracking.Fields, Constants.InfrastructureTime),
                                    TotalUncategorizedTime = GetDoubleField(timeTracking.Fields, Constants.UncategorizedTime)
                                };
                                timeTrackingTask.TotalIssueResearchTimeBackup = timeTrackingTask.TotalIssueResearchTime;
                                timeTrackingTask.TotalMeetingsTimeBackup = timeTrackingTask.TotalMeetingsTime;
                                timeTrackingTask.TotalSupportTimeBackup = timeTrackingTask.TotalSupportTime;
                                timeTrackingTask.TotalItemReviewTimeBackup = timeTrackingTask.TotalItemReviewTime;
                                timeTrackingTask.TotalRequirementsTimeBackup = timeTrackingTask.TotalRequirementsTime;
                                timeTrackingTask.TotalInfrastructureTimeBackup = timeTrackingTask.TotalInfrastructureTime;
                                timeTrackingTask.TotalUncategorizedTimeBackup = timeTrackingTask.TotalUncategorizedTime;
                            }
                        }
                    }

                    var info = workItems.Select(w =>
                    {
                        var topic = new DevOpsTask
                        {
                            ID = (long)w.Fields["System.Id"],
                            Title = w.Fields["System.Title"].ToString(),
                            State = w.Fields["System.State"].ToString(),
                            TotalHours = GetDoubleField(w.Fields, completedWorkKey),
                            TotalHoursBackup = GetDoubleField(w.Fields, completedWorkKey),
                        };
                        return topic;
                    });

                    return (info.Reverse().OrderBy(o => o.State).ToList(), timeTrackingTask);
                }

            }
            catch { } // TODO: Error handling (send messages to UI)

            return ([], null);
        }

        private static double GetDoubleField(IDictionary<string, object> dict, string key)
        {
            if (dict.ContainsKey(key))
            {
                return (double)dict[key];
            }

            return 0;
        }

        public static async Task<List<DevOpsTask>> GetParentWorkItems()
        {
            var settings = await TDiousDataProvider.GetSettings();
            if (settings is null || string.IsNullOrWhiteSpace(settings.CustomChildItemsToCreateWiql) ||
                string.IsNullOrWhiteSpace(settings.DevOpsUri) || string.IsNullOrWhiteSpace(settings.DevOpsApiToken))
            {
                return [];
            }

            try
            {
                using var connection = GetConnection(settings);
                using var witClient = connection.GetClient<WorkItemTrackingHttpClient>();

                var wiql = new Wiql { Query = settings.CustomChildItemsToCreateWiql };
                var workItemQueryResult = await witClient.QueryByWiqlAsync(wiql);

                if (workItemQueryResult.WorkItems.Count() == 0)
                {
                    return [];
                }

                var ids = workItemQueryResult.WorkItems.Select(w => w.Id).ToArray();

                var fields = new[]
                {
                    "System.Id",
                    "System.Title",
                    "System.State",
                    "Microsoft.VSTS.Scheduling.Effort"
                };

                var workItems = await witClient.GetWorkItemsAsync(ids, fields, workItemQueryResult.AsOf);

                return workItems.Select(w =>
                {
                    int effort = 0;
                    if (w.Fields.TryGetValue("Microsoft.VSTS.Scheduling.Effort", out var e))
                    {
                        effort = Convert.ToInt32(e);
                    }

                    return new DevOpsTask
                    {
                        ID = Convert.ToInt64(w.Fields["System.Id"]),
                        Title = w.Fields["System.Title"]?.ToString() ?? "(No Title)",
                        State = w.Fields["System.State"]?.ToString() ?? "Unknown",
                        Effort = effort
                    };
                }).OrderBy(w => w.State).ToList();
            }
            catch
            {
                return [];
            }
        }

        public static async Task SaveCompletedHoursWithComment(long taskID, double newHours, string comment, string? key = null)
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
            if (!string.IsNullOrWhiteSpace(key))
            {
                completedWorkKey = key;
            }
            if (currentWorkItem.Fields.ContainsKey(completedWorkKey))
            {
                oldHours = (double)currentWorkItem.Fields[completedWorkKey];
            }
            string hours = ConvertToTimeString(newHours - oldHours);
            string currentDescription = currentWorkItem.Fields["Microsoft.VSTS.Common.ItemDescription"]?.ToString() ?? string.Empty;
            string commentKey = key ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(commentKey))
            {
                switch (key)
                {
                    case Constants.IssueResearchTime:
                        commentKey = "Issue Research";
                        break;
                    case Constants.MeetingsTime:
                        commentKey = "Meetings";
                        break;
                    case Constants.SupportTime:
                        commentKey = "Support";
                        break;
                    case Constants.ItemReviewTime:
                        commentKey = "Item Review";
                        break;
                    case Constants.RequirementsTime:
                        commentKey = "Requirements";
                        break;
                    case Constants.InfrastructureTime:
                        commentKey = "Infrastructure";
                        break;
                    default:
                    case Constants.UncategorizedTime:
                        commentKey = "Uncategorized";
                        break;
                }

                commentKey = " " + commentKey + ": ";
            }
            string updatedDescription = currentDescription + Environment.NewLine + "<br />" + DateTime.Today.ToShortDateString() + " (" + hours + "): " + commentKey + comment;

            // Create a patch document to replace the existing CompletedWork value
            JsonPatchDocument patchDocument =
            [
                new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Replace,
                    Path = "/fields/" + completedWorkKey,
                    Value = newHours
                }
            ];

            if (settings.EnableComments)
            {
                // Append another line to the Task description
                patchDocument.Add(new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Replace,
                    Path = "/fields/Microsoft.VSTS.Common.ItemDescription",
                    Value = updatedDescription
                });
            }

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

        public static async Task CreateChildTask(long parentId, double estimatedHours)
{
            var settings = await TDiousDataProvider.GetSettings();
            if (settings is null || settings.DevOpsUri is null || settings.DevOpsApiToken is null || settings.DevOpsProject is null || settings.DevOpsEmail is null)
            {
                return;
            }

            using var connection = GetConnection(settings);
            using var witClient = connection.GetClient<WorkItemTrackingHttpClient>();

            var parent = await witClient.GetWorkItemAsync((int)parentId, expand: WorkItemExpand.All);
            if (parent is null)
            {
                return;
            }

            string? parentTitle = parent.Fields["System.Title"].ToString();
            string? parentDesc = parent.Fields?.ContainsKey("System.Description") == true ? parent.Fields["System.Description"].ToString() : "";
            string? parentArea = parent.Fields["System.AreaPath"].ToString();
            string? parentIteration = parent.Fields["System.IterationPath"].ToString();
            if (parentDesc is null || parentDesc is null || parentArea is null || parentIteration is null)
            {
                return;
            }

            var patch = new JsonPatchDocument
            {
                new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/System.Title",
                    Value = parentTitle
                },
                new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/System.AreaPath",
                    Value = parentArea
                },
                new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/System.IterationPath",
                    Value = parentIteration
                },
                new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/System.Description",
                    Value = parentDesc
                },
                new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/Microsoft.VSTS.Common.ItemDescription",
                    Value = parentDesc
                },
                new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/System.AssignedTo",
                    Value = settings.DevOpsEmail
                },
                new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/Microsoft.VSTS.Scheduling.CurrentEstimate",
                    Value = estimatedHours
                },
                new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/relations/-",
                    Value = new
                    {
                        rel = "System.LinkTypes.Hierarchy-Reverse",
                        url = parent.Url
                    }
                }
            };

            string childType = settings.CreateChildWorkItemType ?? "Task";

            await witClient.CreateWorkItemAsync(patch, project: settings.DevOpsProject, type: childType);
        }
    }
}
