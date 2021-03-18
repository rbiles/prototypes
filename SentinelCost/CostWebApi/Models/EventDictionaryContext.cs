using System;

namespace SentinelCost.WebApi.Models
{
    using System;
    using Microsoft.EntityFrameworkCore;
    using SentinelCost.Core;

    public class EventDictionaryContext : DbContext
    {

        public ConfigHelper ConfigHelperObject { get; set; }

        public EventDictionaryContext(DbContextOptions<EventDictionaryContext> options)
            : base(options)
        {
        }

        public DbSet<EventDictionaryItem> EventDictionaryItems { get; set; }
    }
}
