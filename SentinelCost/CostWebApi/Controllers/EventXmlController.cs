using System;

namespace SentinelCost.WebApi.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Newtonsoft.Json;
    using SentinelCost.Core;
    using SentinelCost.WebApi.Models;

    [Route("api/[controller]")] 
    [ApiController]
    public class EventXmlController : ControllerBase
    {
        private readonly EventXmlContext _context;

        public EventXmlController(EventXmlContext context)
        {
            _context = context;

            if (_context.EventXmlItems.Count() == 0)
            {
                _context.EventXmlItems.Add(new EventXmlItem { EventXml = "Item1" });
                _context.SaveChanges();
            }
        }

        // GET: api/EventXml
        [HttpGet]
        public async Task<ActionResult<IEnumerable<EventXmlItem>>> GetEventXmlItem()
        {
            return await _context.EventXmlItems.ToListAsync();
        }

        // GET: api/EventXml/5
        [HttpGet("{id}")]
        public async Task<ActionResult<EventXmlItem>> GetEventXmlItem(long id)
        {
            var EventXmlItem = await _context.EventXmlItems.FindAsync(id);

            if (EventXmlItem == null)
            {
                return NotFound();
            }

            return EventXmlItem;
        }

        // PUT: api/EventXml/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see https://aka.ms/RazorPagesCRUD.
        [HttpPut("{id}")]
        public async Task<IActionResult> PutEventXmlItem(long id, EventXmlItem EventXmlItem)
        {
            if (id != EventXmlItem.PackageId)
            {
                return BadRequest();
            }

            _context.Entry(EventXmlItem).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EventXmlItemExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/EventXml
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see https://aka.ms/RazorPagesCRUD.
        [HttpPost]
        public async Task<ActionResult<EventXmlItem>> PostEventXmlItem(EventXmlItem eventXmlItem)
        {
            Stopwatch processingStopwatch = Stopwatch.StartNew();

            _context.EventXmlItems.Add(eventXmlItem);
            await _context.SaveChangesAsync();

            processingStopwatch.Stop();

            // Set return values
            eventXmlItem.ProcessingDateTime = DateTime.UtcNow;
            eventXmlItem.Count = 1;
            eventXmlItem.ProcessingServer = Environment.MachineName;

            return CreatedAtAction("GetEventXmlItem", new EventXmlItem { PackageId = eventXmlItem.PackageId}, JsonConvert.SerializeObject(eventXmlItem));
        }

        // DELETE: api/EventXml/5
        [HttpDelete("{id}")]
        public async Task<ActionResult<EventXmlItem>> DeleteEventXmlItem(long id)
        {
            var EventXmlItem = await _context.EventXmlItems.FindAsync(id);
            if (EventXmlItem == null)
            {
                return NotFound();
            }

            _context.EventXmlItems.Remove(EventXmlItem);
            await _context.SaveChangesAsync();

            return EventXmlItem;
        }

        private bool EventXmlItemExists(long id)
        {
            return _context.EventXmlItems.Any(e => e.PackageId == id);
        }
    }
}
