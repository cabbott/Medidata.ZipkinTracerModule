using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Owin;

namespace Medidata.ZipkinTracer.Owin
{
    internal static class ZipkinTracerOwinExtensions
    {
        private static readonly string httpContextBaseKey = "System.Web.HttpContextBase";

        public static HttpContextBase ToHttpContext(this IOwinContext owinContext) =>
            owinContext.Environment.ContainsKey(httpContextBaseKey) ?
            owinContext.Environment[httpContextBaseKey] as HttpContextBase : null;
    }
}
