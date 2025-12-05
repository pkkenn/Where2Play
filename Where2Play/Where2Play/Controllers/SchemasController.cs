using Microsoft.AspNetCore.Mvc;
using NJsonSchema;
using NJsonSchema.Generation;
using System.Reflection;

namespace Where2Play.Controllers
{
    /// <summary>
    /// Serves JSON Schema for DTOs by type name.
    /// Kept as a single small controller for schema generation.
    /// </summary>
    [ApiController]
    [Route("schemas")]
    public class SchemasController : ControllerBase
    {
        [HttpGet("{typeName}.json")]
        [Produces("application/schema+json")]
        public IActionResult GetSchema(string typeName)
        {
            var dtoAssembly = Assembly.GetExecutingAssembly();

            Type[] types;
            try
            {
                types = dtoAssembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t is not null).Cast<Type>().ToArray();
            }

            var targetType = types
                .Where(t => t.IsClass && t.Namespace != null)
                .FirstOrDefault(t => t.Namespace!.Contains("Contracts.V1") && string.Equals(t.Name, typeName, StringComparison.OrdinalIgnoreCase));

            if (targetType == null)
                return NotFound();

            var settings = new SystemTextJsonSchemaGeneratorSettings
            {
                SchemaType = SchemaType.OpenApi3,
                DefaultReferenceTypeNullHandling = ReferenceTypeNullHandling.NotNull
            };
            var generator = new JsonSchemaGenerator(settings);
            var schema = generator.Generate(targetType);
            var json = schema.ToJson();
            return Content(json, "application/schema+json");
        }
    }
}
