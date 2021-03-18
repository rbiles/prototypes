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
    using PipelineCost.Core;
    using SentinelCost.Core;
    using SentinelCost.WebApi.Models;

    [Route("api/[controller]")] 
    [ApiController]
    public class EventXmlListController : ControllerBase
    {
        private readonly EventXmlContext _context;

        private long eventsProcessed = 0;

        private readonly ConfigHelper configHelper;

        private DataType dataType = DataType.Xml;
        
        public EventXmlListController(EventXmlContext context, ConfigHelper configHelper)
        {
            _context = context;
            this.configHelper = configHelper;
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
        public async Task<ActionResult<EventXmlList>> PostEventXmlItem(EventXmlList eventXmlList)
        {
            string serviceName = PipelineCostCommon.GetServiceName();

            Stopwatch processingStopwatch = Stopwatch.StartNew();

            Stopwatch conversionStopwatch = Stopwatch.StartNew();
            List<LogRecordSentinel> listLogRecordCdocs =
                eventXmlList.EventXmlListItems.Select(d => new PipelineCostConversion().ToLogRecordCdoc(d.EventXml, configHelper.ServiceName)).ToList();
            conversionStopwatch.Stop();

            // Update each record with data
            foreach (EventXmlItem xmlItem in eventXmlList.EventXmlListItems)
            {
                eventsProcessed++;
                xmlItem.ProcessingDateTime = DateTime.UtcNow;
                xmlItem.ProcessingServer = Environment.MachineName;
            }

            PerformanceCounter xmlEventsReceived = new PerformanceCounter("WECEvents", "XML Events Received");
            xmlEventsReceived.ReadOnly = false;
            xmlEventsReceived.IncrementBy(eventsProcessed);

            //_context.EventxmlItems.AddRange(eventxmlList.EventxmlListItems);
            //await _context.SaveChangesAsync();

            processingStopwatch.Stop();

            Stopwatch dataloadStopwatch = Stopwatch.StartNew();
            configHelper.LoadDataToKusto("EventData", listLogRecordCdocs);
            dataloadStopwatch.Stop();

            PerformanceCounter xmlParsingEfficiency = new PerformanceCounter("WECEvents", "XML Parsing Efficiency");
            xmlParsingEfficiency.ReadOnly = false;
            xmlParsingEfficiency.RawValue = conversionStopwatch.Elapsed.Milliseconds;

            // Kusto upload metric in MS
            PerformanceCounter xmlUploadEfficiency = new PerformanceCounter("WECEvents", "XML Upload Efficiency");
            xmlUploadEfficiency.ReadOnly = false;
            xmlUploadEfficiency.RawValue = dataloadStopwatch.Elapsed.Milliseconds;

            // Decrement Event Counter 
            xmlEventsReceived.IncrementBy(-1 * eventsProcessed);

            // Set return values
            eventXmlList.ProcessingDateTime = DateTime.UtcNow;
            eventXmlList.ProcessingServer = Environment.MachineName;

            List<SentinelCostMetric> metricList = new List<SentinelCostMetric>
            {
                new SentinelCostMetric
                {
                    MachineName = Environment.MachineName,
                    ServiceName = configHelper.ServiceName,
                    OccurenceUtc = DateTime.UtcNow,
                    PackageGuid = eventXmlList.EventXmlListItems[0].PackageGuid,
                    PackageId = eventXmlList.EventXmlListItems[0].PackageId,
                    EventType = Enum.GetName(typeof(EventType), EventType.Receive),
                    MetricData = new Dictionary<string, object>()
                    {
                        { "DataType", Enum.GetName(typeof(DataType), dataType) },
                        { "ProcessTime", configHelper.GetStopWatchDictionary(processingStopwatch) },
                        { "DataLoadTime", configHelper.GetStopWatchDictionary(dataloadStopwatch) },
                    }
                }
            };
            configHelper.LoadMetricsToKusto("Metrics", metricList);


            return CreatedAtAction("GetEventXmlItem", new EventXmlItem { PackageId = eventXmlList.PackageId}, JsonConvert.SerializeObject(eventXmlList));
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
