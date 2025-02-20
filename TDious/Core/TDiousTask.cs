using LiteDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TDious.Core
{
    class TDiousTask
    {
        [BsonId]
        public int ID { get; set; }
        public Int64 WorkItemID { get; set; }
        public double Hours { get; set; }
        public DateTime CacheDateTime { get; set; }
    }
}
