using System;

namespace SentinelCost.WebApi.Models
{
    using System;
    using Microsoft.EntityFrameworkCore;

    public class HelloWorldContext : DbContext
    {
        public HelloWorldContext (DbContextOptions<HelloWorldContext> options)
            : base(options)
        {
        }

        public DbSet<HelloWorldItem> HelloWorldItems { get; set; }
    }
}
