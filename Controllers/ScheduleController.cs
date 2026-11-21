using Microsoft.AspNetCore.Mvc;
using SchoolScheduleMvc.Services;
namespace SchoolScheduleMvc.Controllers;
[ApiController]
[Route("api/schedule")]
public class ScheduleController(ScheduleService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] bool refresh = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var schedule = await service.GetAsync(refresh, cancellationToken);
            return Ok(schedule);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or System.Xml.XmlException or TaskCanceledException)
        {
            return StatusCode(502, new { error = "Не вдалося отримати або розпізнати Google Таблицю: " + ex.Message });
        }
    }
}
