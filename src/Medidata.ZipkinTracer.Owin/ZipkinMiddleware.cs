using System.Threading.Tasks;
using Medidata.ZipkinTracer.Core;
using Microsoft.Owin;
using Owin;

namespace Medidata.ZipkinTracer.Owin
{
    public class ZipkinMiddleware : OwinMiddleware
    {
        private readonly IZipkinConfig _config;

        public ZipkinMiddleware(OwinMiddleware next, IZipkinConfig options) : base(next)
        {
            _config = options;
        }

        public override async Task Invoke(IOwinContext context)
        {
            var httpContext = context.ToHttpContext();

            if (_config.Bypass != null && _config.Bypass(httpContext.Request))
            {
                await Next.Invoke(context);
                return;
            }

            var zipkin = new ZipkinClient(_config, httpContext);
            var span = zipkin.StartServerTrace(context.Request.Uri, context.Request.Method);
            await Next.Invoke(context);
            zipkin.EndServerTrace(span);
        }
    }

    public static class AppBuilderExtensions
    {
        public static void UseZipkin(this IAppBuilder app, IZipkinConfig config)
        {
            config.Validate();
            app.Use<ZipkinMiddleware>(config);
        }
    }
}