using LiteDB;

namespace TDious.Core
{
    class TDiousSettings
    {
        [BsonId]
        public int ID { get; set; }
        public string? DevOpsApiToken { get; set; }
        public string? DevOpsUri { get; set; }
        public string? DevOpsProject { get; set; }
        public string? DevOpsEmail { get; set; }
        public string? CustomHomeWiql { get; set; }
        public string? CustomChildItemsToCreateWiql { get; set; }
        public string? CreateChildWorkItemType { get; set; }
        public string? TrackingTitle { get; set; }
        public bool EnableComments { get; set; }
    }
}
