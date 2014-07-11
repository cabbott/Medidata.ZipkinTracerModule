﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Medidata.ZipkinTracerModule
{
    public class SpanTracer
    {
        private Collector.SpanCollector spanCollector;
        private string serviceName;
        private IZipkinEndpoint zipkinEndpoint;

        public SpanTracer(Collector.SpanCollector spanCollector, string serviceName, IZipkinEndpoint zipkinEndpoint)
        {
            if ( spanCollector == null) 
            {
                throw new ArgumentNullException("spanCollector is null");
            }

            if ( String.IsNullOrEmpty(serviceName)) 
            {
                throw new ArgumentNullException("serviceName is null or empty");
            }

            if ( zipkinEndpoint == null) 
            {
                throw new ArgumentNullException("serviceName is null or empty");
            }

            this.serviceName = serviceName;
            this.spanCollector = spanCollector;
            this.zipkinEndpoint = zipkinEndpoint;
        }

        public virtual Span StartClientSpan(string requestName, string traceId, string parentSpanId)
        {
            //generate new span Id
            var newSpan = new Span();
            newSpan.Id = LongRandom(0, long.MaxValue);
            newSpan.Trace_id = Convert.ToInt64(traceId);

            if ( !String.IsNullOrEmpty(parentSpanId))
            {
                newSpan.Parent_id = Convert.ToInt64(parentSpanId);
            }

            newSpan.Name = requestName;
            newSpan.Annotations = new List<Annotation>();

            // set annotation - zipkinCoreConstants.CLIENT_SEND
            var annotation = new Annotation()
            {
                Host = zipkinEndpoint.GetEndpoint(serviceName),
                Timestamp = GetTimeStamp(),
                Value = zipkinCoreConstants.CLIENT_SEND
            };

            newSpan.Annotations.Add(annotation);

            return newSpan;
        }

        public virtual void EndClientSpan(Span span, int duration)
        {
            throw new NotImplementedException();
        }

        private long GetTimeStamp()
        {
            var t = DateTime.UtcNow - new DateTime(1970, 1, 1);
            return Convert.ToInt64(t.TotalMilliseconds * 1000);
        }

        private long LongRandom(long min, long max)
        {
            byte[] gb = Guid.NewGuid().ToByteArray();
            return BitConverter.ToInt64(gb, 0);
        }
    }
}
