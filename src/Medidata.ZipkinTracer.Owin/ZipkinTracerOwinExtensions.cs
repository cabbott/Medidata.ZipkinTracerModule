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
        public static HttpContextBase ToHttpContext(this IOwinContext owinContext) =>
            owinContext.Environment["System.Web.HttpContextBase"] as HttpContextBase;
    }
}
