﻿using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Medidata.ZipkinTracer.Core.Handlers
{
    public class ZipkinMessageHandler : DelegatingHandler
    {
        private readonly ITraceProvider _provider;
        private readonly ITracerClient _client;

        public ZipkinMessageHandler(ITraceProvider provider, ITracerClient client)
            : this(provider, client, new HttpClientHandler())
        {
        }

        public ZipkinMessageHandler(ITraceProvider provider, ITracerClient client, HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
            _provider = provider;
            _client = client;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var nextTrace = _provider.GetNext();

            request.Headers.Add(TraceProvider.TraceIdHeaderName, nextTrace.TraceId);
            request.Headers.Add(TraceProvider.SpanIdHeaderName, nextTrace.SpanId);
            request.Headers.Add(TraceProvider.ParentSpanIdHeaderName, nextTrace.ParentSpanId);
            request.Headers.Add(TraceProvider.SampledHeaderName, nextTrace.ParentSpanId);

            var span = _client.StartClientTrace(request.RequestUri, request.Method.ToString(), nextTrace);
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            _client.EndClientTrace(span, (int)response.StatusCode);
            return response;
        }
    }
}