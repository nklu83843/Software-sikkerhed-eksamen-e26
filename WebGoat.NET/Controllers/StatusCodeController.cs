using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WebGoatCore.ViewModels; 
using WebGoatCore.Models;
using WebGoatCore.ViewModels;

namespace WebGoatCore.Controllers
{
    [AllowAnonymous]
    public class StatusCodeController : Controller
    {
        public const string NAME = "StatusCode";
        private readonly ILogger<StatusCodeController> _logger;

        public StatusCodeController(ILogger<StatusCodeController> logger)
        {
            _logger = logger;
        }

        [HttpGet("/StatusCode")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Index(int code)
        {
            // Hent information om den oprindelige undtagelse via ASP.NET Core Diagnostic Feature
            var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();

            if (exceptionFeature != null)
            {
                // SIKKER LOGNING: Den fulde tekniske fejl logges internt på serveren
                _logger.LogError(
                    exceptionFeature.Error,
                    "Ubehandlet teknisk fejl opstået ved URL: {Path}",
                    exceptionFeature.Path);
            }

            // Opret en ren præsentationsmodel uden kildekodeoplysninger
            var errorViewModel = new ErrorViewModel
            {
                StatusCode = code,
                Message = code switch
                {
                    404 => "Den efterspurgte ressource eller side blev ikke fundet.",
                    403 => "Du har ikke autorisation til at tilgå denne funktion.",
                    500 => "Der opstod en uventet fejl under behandlingen af din anmodning. Hændelsen er blevet logget, og vores driftsteam er underrettet.",
                    _ => "Der opstod en uventet hændelse."
                },
                // Generer en unik sporings-ID (Correlation ID), som kunden kan opgive til support
                RequestId = HttpContext.TraceIdentifier
            };

            return View("Error", errorViewModel);
        }
    }
}