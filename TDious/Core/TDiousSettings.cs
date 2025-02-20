using LiteDB;

namespace TDious.Core
{
    class TDiousSettings
    {
        [BsonId]
        public int ID { get; set; }
        public string? DevOpsApiToken { get; set; }
        public string? DevOpsUri { get; set; }
    }
}
