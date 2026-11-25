#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.

using Microsoft.AspNetCore.Diagnostics;
using System;

namespace WebGoatCore.ViewModels
{
    public class ErrorViewModel
    {
        public int StatusCode { get; set; }
        public string Message { get; set; }
        public string RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

        // =========================================================================
        // Bagudkompatibilitet til HomeController og Error.cshtml (Øvelse 33/34)
        // =========================================================================
        public Exception ExceptionInfo { get; set; }
        public bool ShowExceptionInfo => ExceptionInfo != null;
    }
}
