using Where2Play.Models;

namespace Where2Play
{
    public static class EventRoster
    {

        static EventRoster()
        {
            AllEvents = new List<EventSummary>();
        }

        public static IList<EventSummary> AllEvents { get; set; }

    }
}
