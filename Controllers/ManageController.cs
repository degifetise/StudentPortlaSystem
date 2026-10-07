using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize(Roles = "Admin")]
public class ManageController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}

[Authorize(Roles = "Teacher")]
public class TeacherController : Controller
{
    public IActionResult Gradebook()
    {
        return View();
    }
}

[Authorize(Roles = "Student")]
public class StudentController : Controller
{
    public IActionResult Gradebook()
    {
        return View();
    }
}

[Authorize(Roles = "Parent")]
public class ParentController : Controller
{
    public IActionResult ReportCard()
    {
        return View();
    }
}