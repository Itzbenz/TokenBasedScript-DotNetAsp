using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TokenBasedScript.Models;

namespace TokenBasedScript.Controllers;

public class ErrorController : Controller
{
    public ViewResult Index()
    {
        return View("Error");
    }
    public async Task<ViewResult> Error404()
    {
        Response.StatusCode = 404;  //you may want to set this to 200
        //return html file
        var html = await System.IO.File.ReadAllTextAsync("wwwroot/404.html");
        ViewData["html"] = html;
        ViewBag.Title = "404";
        return View("NotFound", html);
    }

}