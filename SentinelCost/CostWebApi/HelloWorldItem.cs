﻿using System;

namespace SentinelCost.WebApi
{
    using System;

    public class HelloWorldItem
    {
        public long Id { get; set; }

        public string Name { get; set; }

        public bool IsComplete { get; set; }

        public DateTime TimeOfHello { get; set; }

        public string ReturnMessage { get; set; }
    }
}