using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace FaceitMatchGatherer.Middleware
{
    public class ErrorHandlingMiddleware
    {
        private readonly RequestDelegate next;
        public ErrorHandlingMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        public async Task Invoke(HttpContext context, ILogger<ErrorHandlingMiddleware> logger)
        {
            try
            {
                await next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, logger, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, ILogger<ErrorHandlingMiddleware> logger, Exception ex)
        {
            // create copy of body stream, so we can read it without manipulating it
            var requestBodyStream = new MemoryStream();
            await context.Request.Body.CopyToAsync(requestBodyStream);
            requestBodyStream.Seek(0, SeekOrigin.Begin);

            // log
            var url = UriHelper.GetDisplayUrl(context.Request);
            var requestBodyText = new StreamReader(requestBodyStream).ReadToEnd();
            logger.LogError(ex, $"Error for method [ {context.Request.Method} ], url [ {url} ] and body [ {requestBodyText} ]");

            // reset body stream
            requestBodyStream.Seek(0, SeekOrigin.Begin);
            context.Request.Body = requestBodyStream;

            // Determine the statuscode to be returned
            var code = HttpStatusCode.InternalServerError; // 500 if unexpected
            context.Response.StatusCode = (int)code;

            // Determine the message to be returned
            var msg = "Error in MatchRetriever";
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync(msg);
        }
    }
}
