using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Medidata.ZipkinTracer.Core;

namespace Medidata.ZipkinTracer.WebApi
{
    public class ZipkinTraceHandler: DelegatingHandler
    {
		private readonly IZipkinConfig config;

        public ZipkinTraceHandler(IZipkinConfig config): base()
        {
            this.config = config;
        }

        public ZipkinTraceHandler(IZipkinConfig config, HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
            this.config = config;
        }

        protected async override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (InnerHandler == null)
                InnerHandler = new HttpClientHandler();

            var context = new HttpContextWrapper(HttpContext.Current);

            if (config.Bypass?.Invoke(context.Request) ?? false)
                return await base.SendAsync(request, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var zipkin = new ZipkinClient(config, context);
            var span = zipkin.StartServerTrace(context.Request.Url, context.Request.HttpMethod);

            var result = await base.SendAsync(request, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            zipkin.EndServerTrace(span);

            return result;
        }
    }
}
