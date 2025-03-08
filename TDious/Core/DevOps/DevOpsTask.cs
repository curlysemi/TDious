namespace TDious.Core.DevOps
{
    class DevOpsTask
    {
        public Int64 ID { get; set; }
        public string? Title { get; set; }
        public string? State { get; set; }
        public double TotalHours { get; set; }
        public double AddHours { get; set; }
    }
}
