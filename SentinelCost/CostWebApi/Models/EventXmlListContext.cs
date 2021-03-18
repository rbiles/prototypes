using System;

namespace SentinelCost.WebApi.Models
{
    using System;
    using Microsoft.EntityFrameworkCore;
    using SentinelCost.Core;

    public class EventXmlListContext : DbContext
    {
        public EventXmlListContext(DbContextOptions<EventXmlContext> options)
            : base(options)
        {
        }

        public DbSet<EventXmlItem> EventXmlItems { get; set; }
    }
}
