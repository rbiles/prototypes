using System;

namespace SentinelCost.WebApi.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using Microsoft.AspNetCore.Mvc;
    using Newtonsoft.Json;
    using SentinelCost.WebApi.Models;

    [Route("api/[controller]")]
    [ApiController]
    public class ControllerBase : Controller
    {
        private readonly TodoContext _context;

        public ControllerBase()
        {
        }

        public ControllerBase(TodoContext context)
        {

        }
    }
}