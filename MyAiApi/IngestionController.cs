using Microsoft.AspNetCore.Mvc;
using DataIngestion;

namespace MyAiApi.Controllers;

[ApiController]
[Route("[controller]")]
public class IngestionController : ControllerBase
{
    private readonly DataIngestionOrchestrator _orchestrator;

    public IngestionController(DataIngestionOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [HttpPost("upload")]
    [DisableRequestSizeLimit] // Useful for large PDFs
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");

        try
        {
            using var stream = file.OpenReadStream();
            await _orchestrator.ProcessFile(file.FileName, stream);

            return Ok(new
            {
                Message = $"Successfully processed {file.FileName}",
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            // The exception will be caught by OpenTelemetry automatically
            return StatusCode(500, $"Internal error processing file: {ex.Message}");
        }
    }
}