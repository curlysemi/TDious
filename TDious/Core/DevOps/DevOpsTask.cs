namespace TDious.Core.DevOps
{
    class DevOpsTask
    {
        public Int64 ID { get; set; }
        public string? Title { get; set; }
        public string? State { get; set; }
        public double TotalHours { get; set; }
        public double TotalHoursBackup { get; set; }
        public int Effort { get; set; }
        public string? DevOpsProject { get; set; }
        public double AddHours { get; set; }

        public double TotalIssueResearchTime { get; set; }
        public double TotalIssueResearchTimeBackup { get; set; }
        public double AddIssueResearchTime { get; set; }

        public double TotalMeetingsTime { get; set; }
        public double TotalMeetingsTimeBackup { get; set; }
        public double AddMeetingsTime { get; set; }

        public double TotalSupportTime { get; set; }
        public double TotalSupportTimeBackup { get; set; }
        public double AddSupportTime { get; set; }

        public double TotalItemReviewTime { get; set; }
        public double TotalItemReviewTimeBackup { get; set; }
        public double AddItemReviewTime { get; set; }

        public double TotalRequirementsTime { get; set; }
        public double TotalRequirementsTimeBackup { get; set; }
        public double AddRequirementsTime { get; set; }

        public double TotalInfrastructureTime { get; set; }
        public double TotalInfrastructureTimeBackup { get; set; }
        public double AddInfrastructureTime { get; set; }

        public double TotalUncategorizedTime { get; set; }
        public double TotalUncategorizedTimeBackup { get; set; }
        public double AddUncategorizedTime { get; set; }

        public double GetTrueTotalHours()
        {
            return TotalHours
                + TotalIssueResearchTime
                + TotalMeetingsTime
                + TotalSupportTime
                + TotalItemReviewTime
                + TotalRequirementsTime
                + TotalInfrastructureTime
                + TotalUncategorizedTime;
        }

    }
}
