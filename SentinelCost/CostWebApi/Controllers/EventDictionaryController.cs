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
    using SentinelCost.Core;
    using SentinelCost.WebApi.Models;

    [Route("api/[controller]")] 
    [ApiController]
    public class EventDictionaryController : ControllerBase
    {
        private readonly EventDictionaryContext _context;

        public EventDictionaryController(EventDictionaryContext context) : base()
        {
            _context = context;
        }

        // GET: api/EventDictionary
        [HttpGet]
        public async Task<ActionResult<IEnumerable<EventDictionaryItem>>> GetEventDictionaryItem()
        {
            return await _context.EventDictionaryItems.ToListAsync();
        }

        // GET: api/EventDictionary/5
        [HttpGet("{id}")]
        public async Task<ActionResult<EventDictionaryItem>> GetEventDictionaryItem(long id)
        {
            var EventDictionaryItem = await _context.EventDictionaryItems.FindAsync(id);

            if (EventDictionaryItem == null)
            {
                return NotFound();
            }

            return EventDictionaryItem;
        }

        // PUT: api/EventDictionary/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see https://aka.ms/RazorPagesCRUD.
        [HttpPut("{id}")]
        public async Task<IActionResult> PutEventDictionaryItem(long id, EventDictionaryItem EventDictionaryItem)
        {
            if (id != EventDictionaryItem.PackageId)
            {
                return BadRequest();
            }

            _context.Entry(EventDictionaryItem).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EventDictionaryItemExists(id))
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

        // POST: api/EventDictionary
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see https://aka.ms/RazorPagesCRUD.
        [HttpPost]
        public async Task<ActionResult<EventDictionaryItem>> PostEventDictionaryItem(EventDictionaryItem eventDictionaryItem)
        {
            Stopwatch processingStopwatch = Stopwatch.StartNew();

            // _context.EventDictionaryItems.Add(EventDictionaryItem);
            // await _context.SaveChangesAsync();

            processingStopwatch.Stop();

            // Set return values
            eventDictionaryItem.ProcessingDateTime = DateTime.UtcNow;
            eventDictionaryItem.Count = 1;
            eventDictionaryItem.ProcessingServer = Environment.MachineName;

            return CreatedAtAction("GetEventDictionaryItem", new EventDictionaryItem { PackageId = eventDictionaryItem.PackageId}, eventDictionaryItem);
        }

        // DELETE: api/EventDictionary/5
        [HttpDelete("{id}")]
        public async Task<ActionResult<EventDictionaryItem>> DeleteEventDictionaryItem(long id)
        {
            var EventDictionaryItem = await _context.EventDictionaryItems.FindAsync(id);
            if (EventDictionaryItem == null)
            {
                return NotFound();
            }

            _context.EventDictionaryItems.Remove(EventDictionaryItem);
            await _context.SaveChangesAsync();

            return EventDictionaryItem;
        }

        private bool EventDictionaryItemExists(long id)
        {
            return _context.EventDictionaryItems.Any(e => e.PackageId == id);
        }
    }
}
