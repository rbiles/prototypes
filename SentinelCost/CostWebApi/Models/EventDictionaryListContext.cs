using System;

namespace SentinelCost.WebApi.Models
{
    using System;
    using Microsoft.EntityFrameworkCore;
    using SentinelCost.Core;

    public class EventDictionaryListContext : DbContext
    {
        public EventDictionaryListContext(DbContextOptions<EventDictionaryListContext> options)
            : base(options)
        {
        }

        public DbSet<EventDictionaryItem> EventDictionaryItems { get; set; }
    }
}
