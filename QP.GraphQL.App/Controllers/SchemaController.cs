using Microsoft.AspNetCore.Mvc;
using QP.GraphQL.App.Schema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QP.GraphQL.App.Controllers
{
    [ApiController]
    [Route("/api/[controller]")]
    public class SchemaController : ControllerBase
    {
        private readonly ISchemaFactory _factory;

        public SchemaController(ISchemaFactory factory)
        {
            _factory = factory;
        }

        [HttpPost]
        [Route("reload")]
        public SchemaContext ReloadSchema()
        {
            _factory.ReloadSchema();
            return _factory.Context;
        }

        [HttpGet]
        [Route("context")]
        public SchemaContext GetSchemaContext()
        {
            return _factory.Context;
        }
    }
}
