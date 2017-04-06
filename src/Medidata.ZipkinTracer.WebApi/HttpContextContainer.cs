using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Medidata.ZipkinTracer.WebApi
{
    internal static class HttpContextContainer
    {
        private static HttpContextBase current = null;

        public static HttpContextBase Current
        {
            get => current != null ? current : current = new HttpContextWrapper(HttpContext.Current);
            set => current = value;
        }
    }
}
