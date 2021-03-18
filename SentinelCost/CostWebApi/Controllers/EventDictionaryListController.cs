// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

using PipelineCost.Core;

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
    public class EventDictionaryListController : ControllerBase
    {
        private readonly EventDictionaryContext _context;

        private long eventsProcessed = 0;

        private readonly ConfigHelper configHelper;

        private DataType dataType = DataType.Dictionary;

        public EventDictionaryListController(EventDictionaryContext context, ConfigHelper configHelper)
        {
            _context = context;
            this.configHelper = configHelper;
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
        public async Task<ActionResult<EventDictionaryList>> PostEventDictionaryItem(EventDictionaryList eventDictionaryList)
        {
            string serviceName = PipelineCostCommon.GetServiceName();

            Stopwatch processingStopwatch = Stopwatch.StartNew();

            Stopwatch conversionStopwatch = Stopwatch.StartNew();
            List<LogRecordSentinel> listLogRecordCdocs =
                eventDictionaryList.EventDictionaryListItems.Select(d => new LogRecordSentinel(d.EventDictionary, null, serviceName)).ToList();
            conversionStopwatch.Stop();

            // Update each record with data
            foreach (EventDictionaryItem xmlItem in eventDictionaryList.EventDictionaryListItems)
            {
                eventsProcessed++;
                xmlItem.ProcessingDateTime = DateTime.UtcNow;
                xmlItem.ProcessingServer = Environment.MachineName;
            }

            PerformanceCounter dictionaryEventsReceived = new PerformanceCounter("WECEvents", "Dictionary Events Received");
            dictionaryEventsReceived.ReadOnly = false;
            dictionaryEventsReceived.IncrementBy(eventsProcessed);

            //_context.EventDictionaryItems.AddRange(eventDictionaryList.EventDictionaryListItems);
            //await _context.SaveChangesAsync();

            processingStopwatch.Stop();

            Stopwatch dataloadStopwatch = Stopwatch.StartNew();
            configHelper.LoadDataToKusto("EventData", listLogRecordCdocs);
            dataloadStopwatch.Stop();

            PerformanceCounter dictionaryParsingEfficiency = new PerformanceCounter("WECEvents", "Dictionary Parsing Efficiency");
            dictionaryParsingEfficiency.ReadOnly = false;
            dictionaryParsingEfficiency.RawValue = conversionStopwatch.Elapsed.Milliseconds;

            // Kusto upload metric in MS
            PerformanceCounter dictionaryUploadEfficiency = new PerformanceCounter("WECEvents", "Dictionary Upload Efficiency");
            dictionaryUploadEfficiency.ReadOnly = false;
            dictionaryUploadEfficiency.RawValue = dataloadStopwatch.Elapsed.Milliseconds;
            
            // Decrement Event Counter 
            dictionaryEventsReceived.IncrementBy(-1 * eventsProcessed);

            // Set return values
            eventDictionaryList.ProcessingDateTime = DateTime.UtcNow;
            eventDictionaryList.ProcessingServer = Environment.MachineName;

            List<SentinelCostMetric> metricList = new List<SentinelCostMetric>
            {
                new SentinelCostMetric
                {
                    MachineName = Environment.MachineName,
                    ServiceName = configHelper.ServiceName,
                    OccurenceUtc = DateTime.UtcNow,
                    PackageGuid = eventDictionaryList.EventDictionaryListItems[0].PackageGuid,
                    PackageId = eventDictionaryList.EventDictionaryListItems[0].PackageId,
                    EventType = Enum.GetName(typeof(EventType), EventType.Receive),
                    MetricData = new Dictionary<string, object>()
                    {
                        { "SendType", Enum.GetName(typeof(DataType), dataType) },
                        { "ProcessTime", configHelper.GetStopWatchDictionary(processingStopwatch) },
                        { "DataLoadTime", configHelper.GetStopWatchDictionary(dataloadStopwatch) },
                    }
                }
            };
            configHelper.LoadMetricsToKusto("Metrics", metricList);

            return CreatedAtAction("GetEventDictionaryItem", new EventDictionaryItem { PackageId = eventDictionaryList.PackageId }, eventDictionaryList);
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