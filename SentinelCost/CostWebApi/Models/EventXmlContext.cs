using System;

namespace SentinelCost.WebApi.Models
{
    using System;
    using Microsoft.EntityFrameworkCore;
    using SentinelCost.Core;

    public class EventXmlContext : DbContext
    {
        public EventXmlContext(DbContextOptions<EventXmlContext> options)
            : base(options)
        {
        }

        public DbSet<EventXmlItem> EventXmlItems { get; set; }
    }
}
