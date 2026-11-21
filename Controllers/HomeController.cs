using Microsoft.AspNetCore.Mvc;
namespace SchoolScheduleMvc.Controllers;
public class HomeController : Controller
{
    public IActionResult Index() => View();
    public IActionResult Error() => Problem("Помилка сервера");
}
