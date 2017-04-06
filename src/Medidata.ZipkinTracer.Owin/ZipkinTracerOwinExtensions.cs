using System;
using System.Web;
using Microsoft.Owin;

namespace Medidata.ZipkinTracer.Owin
{
    internal static class ZipkinTracerOwinExtensions
    {
        public static readonly string HttpContextBaseKey = "System.Web.HttpContextBase";

        public static HttpContextBase ToHttpContext(this IOwinContext owinContext) =>
            owinContext.Environment.ContainsKey(HttpContextBaseKey) ?
            owinContext.Environment[HttpContextBaseKey] as HttpContextBase :
            throw new NotSupportedException("Self hosted and non-System.Web-based scenarios are not supported.");
    }
}
