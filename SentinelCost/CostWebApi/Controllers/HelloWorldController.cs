using System;

namespace SentinelCost.WebApi.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using SentinelCost.WebApi.Models;

    [Route("api/[controller]")] 
    [ApiController]
    public class HelloWorldController : ControllerBase
    {
        private readonly HelloWorldContext _context;

        public HelloWorldController(HelloWorldContext context)
        {
            _context = context;

            if (_context.HelloWorldItems.Count() == 0)
            {
                _context.HelloWorldItems.Add(new HelloWorldItem { Name = "Item1" });
                _context.SaveChanges();
            }
        }

        // GET: api/HelloWorld
        [HttpGet]
        public async Task<ActionResult<HelloWorldItem>> GetHelloWorldItem()
        {
            var helloWorldItem = new HelloWorldItem
            {
                Id = 1,
                Name = "HelloWorldFromServer",
                IsComplete = true,
                TimeOfHello = DateTime.UtcNow,
                ReturnMessage = "Hello to your GET!!!"
            };

            return helloWorldItem;
        }

        // GET: api/HelloWorld/5
        [HttpGet("{id}")]
        public async Task<ActionResult<HelloWorldItem>> GetHelloWorldItem(long id)
        {
            var HelloWorldItem = await _context.HelloWorldItems.FindAsync(id);

            if (HelloWorldItem == null)
            {
                return NotFound();
            }

            return HelloWorldItem;
        }

        // PUT: api/HelloWorld/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see https://aka.ms/RazorPagesCRUD.
        [HttpPut("{id}")]
        public async Task<IActionResult> PutHelloWorldItem(long id, HelloWorldItem HelloWorldItem)
        {
            if (id != HelloWorldItem.Id)
            {
                return BadRequest();
            }

            _context.Entry(HelloWorldItem).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!HelloWorldItemExists(id))
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

        // POST: api/HelloWorld
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see https://aka.ms/RazorPagesCRUD.
        [HttpPost]
        public async Task<ActionResult<HelloWorldItem>> PostHelloWorldItem(HelloWorldItem HelloWorldItem)
        {
            //_context.HelloWorldItems.Add(HelloWorldItem);
            //await _context.SaveChangesAsync();

            HelloWorldItem.TimeOfHello = DateTime.UtcNow;
            HelloWorldItem.ReturnMessage = "Hello to your POST!!!";

            return CreatedAtAction("GetHelloWorldItem", new { id = HelloWorldItem.Id }, HelloWorldItem);
        }

        // DELETE: api/HelloWorld/5
        [HttpDelete("{id}")]
        public async Task<ActionResult<HelloWorldItem>> DeleteHelloWorldItem(long id)
        {
            var HelloWorldItem = await _context.HelloWorldItems.FindAsync(id);
            if (HelloWorldItem == null)
            {
                return NotFound();
            }

            _context.HelloWorldItems.Remove(HelloWorldItem);
            await _context.SaveChangesAsync();

            return HelloWorldItem;
        }

        private bool HelloWorldItemExists(long id)
        {
            return _context.HelloWorldItems.Any(e => e.Id == id);
        }
    }
}
