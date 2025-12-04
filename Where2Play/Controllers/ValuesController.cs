using Microsoft.AspNetCore.Mvc;
using Where2Play.Models;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Where2Play.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ValuesController : ControllerBase
    {
        // GET: api/<ValuesController>
        [HttpGet]
        public IEnumerable<EventSummary> Get()
        {

            return EventRoster.AllEvents;
        }

        // GET api/<ValuesController>/5
        [HttpGet("{id}")]
        public EventSummary Get(int id)
        {
            return EventRoster.AllEvents.FirstOrDefault(e => e.GetHashCode() == id);
        }

        // POST api/<ValuesController>
        [HttpPost]
        public IList<EventSummary> Post([FromBody] EventSummary value)
        {
            return EventRoster.AllEvents;
        }

        // PUT api/<ValuesController>/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] EventSummary value)
        {
        }

        // DELETE api/<ValuesController>/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
