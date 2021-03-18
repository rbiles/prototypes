using System;
using System.Linq;
using System.Threading.Tasks;

namespace BlazorSampleApp.Data
{
    public class HelloFromService
    {
        private static readonly string[] PlacesOnEarth = new[]
        {
            "Barcelona", "Madrid", "Rome", "Monte Carlo", "Seattle", "San Luis Obispo", "Mexico City", "Stockholm", "Berlin", "Paris", "Santiago", "Caracas", "Fiji", "Hanalei Bay"
        };

        public Task<HelloFrom[]> GetGreeting(DateTime startDate)
        {
            var rng = new Random();
            return Task.FromResult(Enumerable.Range(1, 8).Select(index => new HelloFrom()
            {
                Date = startDate.AddDays(index),
                TemperatureC = rng.Next(-20, 55),
                Greeting = $"Hello from {PlacesOnEarth[rng.Next(PlacesOnEarth.Length)]}!"
            }).ToArray());
        }
    }
}