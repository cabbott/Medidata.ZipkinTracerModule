﻿using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ploeh.AutoFixture;
using Rhino.Mocks;
using Medidata.ZipkinTracer.Core.Collector;
using log4net;
using System.Linq;
using System.Diagnostics;
using Microsoft.Owin;

namespace Medidata.ZipkinTracer.Core.Test
{
    [TestClass]
    public class ZipkinClientTests
    {
        private IFixture fixture;
        private ISpanCollectorBuilder spanCollectorBuilder;
        private SpanCollector spanCollectorStub;
        private SpanTracer spanTracerStub;
        private ITraceProvider traceProvider;
        private ILog logger;

        [TestInitialize]
        public void Init()
        {
            fixture = new Fixture();
            spanCollectorBuilder = MockRepository.GenerateStub<ISpanCollectorBuilder>();
            traceProvider = MockRepository.GenerateStub<ITraceProvider>();
            logger = MockRepository.GenerateStub<ILog>();
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void CTOR_WithNullLogger()
        {
            new ZipkinClient(null, new ZipkinConfig(), spanCollectorBuilder);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void CTOR_WithNullConfig()
        {
            new ZipkinClient(logger, null, spanCollectorBuilder);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void CTOR_WithNullBuilder()
        {
            new ZipkinClient(logger, new ZipkinConfig(), null, null);
        }

        [TestMethod]
        public void CTOR_WithTraceIdNullOrEmpty()
        {
            var zipkinConfigStub = CreateZipkinConfigWithDefaultValues();

            traceProvider.Expect(x => x.TraceId).Return(string.Empty);
            traceProvider.Expect(x => x.IsSampled).Return(true);

            spanCollectorStub = MockRepository.GenerateStub<SpanCollector>(new Uri("http://localhost"), (uint)0, logger);
            spanCollectorBuilder.Expect(x => x.Build(Arg<Uri>.Is.Anything, Arg<uint>.Is.Anything, Arg<ILog>.Is.Equal(logger))).Return(spanCollectorStub);
            var zipkinClient = new ZipkinClient(logger, zipkinConfigStub, spanCollectorBuilder);
            Assert.IsFalse(zipkinClient.IsTraceOn);
        }

        [TestMethod]
        public void CTOR_WithIsSampledFalse()
        {
            var zipkinConfigStub = CreateZipkinConfigWithDefaultValues();

            traceProvider.Expect(x => x.TraceId).Return(fixture.Create<string>());
            traceProvider.Expect(x => x.IsSampled).Return(false);

            spanCollectorStub = MockRepository.GenerateStub<SpanCollector>(new Uri("http://localhost"), (uint)0, logger);
            spanCollectorBuilder.Expect(x => x.Build(Arg<Uri>.Is.Anything, Arg<uint>.Is.Anything, Arg<ILog>.Is.Equal(logger))).Return(spanCollectorStub);
            var zipkinClient = new ZipkinClient(logger, zipkinConfigStub, spanCollectorBuilder);
            Assert.IsFalse(zipkinClient.IsTraceOn);
        }

        [TestMethod]
        public void CTOR_StartCollector()
        {
            var zipkinClient = (ZipkinClient)SetupZipkinClient();
            Assert.IsNotNull(zipkinClient.spanCollector);
            Assert.IsNotNull(zipkinClient.spanTracer);
        }

        [TestMethod]
        public void CTOR_ZpkinServer_Exception()
        {
            var zipkinConfigStub = CreateZipkinConfigWithDefaultValues();

            traceProvider.Expect(x => x.TraceId).Return(fixture.Create<string>());
            traceProvider.Expect(x => x.IsSampled).Return(true);

            spanCollectorStub = MockRepository.GenerateStub<SpanCollector>(new Uri("http://localhost"), (uint)0, logger);

            var expectedException = new Exception();
            spanCollectorBuilder.Expect(x => x.Build(Arg<Uri>.Is.Anything, Arg<uint>.Is.Anything, Arg<ILog>.Is.Equal(logger))).Throw(expectedException);

            var zipkinClient = new ZipkinClient(logger, zipkinConfigStub, spanCollectorBuilder);
            Assert.IsFalse(zipkinClient.IsTraceOn);
        }

        [TestMethod]
        public void Shutdown_StopCollector()
        {
            var zipkinClient = (ZipkinClient)SetupZipkinClient();

            zipkinClient.ShutDown();

            spanCollectorStub.AssertWasCalled(x => x.Stop());
        }

        [TestMethod]
        public void Shutdown_CollectorNullDoesntThrow()
        {
            var zipkinClient = (ZipkinClient)SetupZipkinClient();
            zipkinClient.spanCollector = null;

            zipkinClient.ShutDown();
        }

        [TestMethod]
        public void StartServerSpan()
        {
            var tracerClient = SetupZipkinClient();
            var zipkinClient = (ZipkinClient)tracerClient;
            spanTracerStub = GetSpanTracerStub();
            zipkinClient.spanTracer = spanTracerStub;
            var uriHost = "https://www.x@y.com";
            var uriAbsolutePath = "/object";
            var methodName = "GET";
            var spanName = methodName;
            var requestUri = new Uri(uriHost + uriAbsolutePath);

            var expectedSpan = new Span();
            spanTracerStub.Expect(
                x => x.ReceiveServerSpan(
                    Arg<string>.Is.Equal(spanName.ToLower()),
                    Arg<string>.Is.Equal(traceProvider.TraceId),
                    Arg<string>.Is.Equal(traceProvider.ParentSpanId),
                    Arg<string>.Is.Equal(traceProvider.SpanId),
                    Arg<Uri>.Is.Equal(requestUri))).Return(expectedSpan);

            var result = tracerClient.StartServerTrace(requestUri, methodName);

            Assert.AreEqual(expectedSpan, result);
        }

        [TestMethod]
        public void StartServerSpan_Exception()
        {
            var tracerClient = SetupZipkinClient();
            var zipkinClient = (ZipkinClient)tracerClient;
            spanTracerStub = GetSpanTracerStub();
            zipkinClient.spanTracer = spanTracerStub;
            var uriHost = "https://www.x@y.com";
            var uriAbsolutePath = "/object";
            var methodName = "GET";
            var spanName = methodName;
            var requestUri = new Uri(uriHost + uriAbsolutePath);

            spanTracerStub.Expect(
                x => x.ReceiveServerSpan(
                    Arg<string>.Is.Equal(spanName.ToLower()),
                    Arg<string>.Is.Anything,
                    Arg<string>.Is.Anything,
                    Arg<string>.Is.Anything,
                    Arg<Uri>.Is.Equal(requestUri))).Throw(new Exception());

            var result = tracerClient.StartServerTrace(requestUri, methodName);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void StartServerSpan_IsTraceOnIsFalse()
        {
            var tracerClient = SetupZipkinClient();
            var zipkinClient = (ZipkinClient)tracerClient;
            zipkinClient.IsTraceOn = false;
            var uriHost = "https://www.x@y.com";
            var uriAbsolutePath = "/object";
            var methodName = "GET";

            var result = tracerClient.StartServerTrace(new Uri(uriHost + uriAbsolutePath), methodName);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void EndServerSpan()
        {
            var tracerClient = SetupZipkinClient();
            var zipkinClient = (ZipkinClient)tracerClient;
            spanTracerStub = GetSpanTracerStub();
            zipkinClient.spanTracer = spanTracerStub;
            var serverSpan = new Span();

            tracerClient.EndServerTrace(serverSpan);

            spanTracerStub.AssertWasCalled(x => x.SendServerSpan(serverSpan));
        }

        [TestMethod]
        public void EndServerSpan_Exception()
        {
            var tracerClient = SetupZipkinClient();
            var zipkinClient = (ZipkinClient)tracerClient;
            spanTracerStub = GetSpanTracerStub();
            zipkinClient.spanTracer = spanTracerStub;
            var serverSpan = new Span();

            spanTracerStub.Expect(x => x.SendServerSpan(serverSpan)).Throw(new Exception());

            tracerClient.EndServerTrace(serverSpan);
        }

        [TestMethod]
        public void EndServerSpan_IsTraceOnIsFalse_DoesntThrow()
        {
            var tracerClient = SetupZipkinClient();
            var zipkinClient = (ZipkinClient)tracerClient;
            zipkinClient.IsTraceOn = false;
            var serverSpan = new Span();

            tracerClient.EndServerTrace(serverSpan);
        }

        [TestMethod]
        public void EndServerSpan_NullServerSpan_DoesntThrow()
        {
            var tracerClient = SetupZipkinClient();

            tracerClient.EndServerTrace(null);
        }

        [TestMethod]
        public void StartClientSpan()
        {
            var tracerClient = SetupZipkinClient();
            var zipkinClient = (ZipkinClient)tracerClient;
            spanTracerStub = GetSpanTracerStub();
            zipkinClient.spanTracer = spanTracerStub;
            var clientServiceName = "abc-sandbox";
            var uriAbsolutePath = "/object";
            var methodName = "GET";
            var spanName = methodName;

            var expectedSpan = new Span();
            spanTracerStub.Expect(
                x => x.SendClientSpan(
                    Arg<string>.Is.Equal(spanName.ToLower()),
                    Arg<string>.Is.Equal(traceProvider.TraceId),
                    Arg<string>.Is.Equal(traceProvider.ParentSpanId),
                    Arg<string>.Is.Equal(traceProvider.SpanId),
                    Arg<Uri>.Is.Anything)).Return(expectedSpan);

            var result = tracerClient.StartClientTrace(new Uri("https://" + clientServiceName + ".xyz.net:8000" + uriAbsolutePath), methodName, traceProvider);

            Assert.AreEqual(expectedSpan, result);
        }

        [TestMethod]
        public void StartClientSpan_UsingIpAddress()
        {
            var tracerClient = SetupZipkinClient();
            var zipkinClient = (ZipkinClient)tracerClient;
            spanTracerStub = GetSpanTracerStub();
            zipkinClient.spanTracer = spanTracerStub;
            var clientServiceName = "192.168.178.178";
            var uriAbsolutePath = "/object";
            var methodName = "GET";
            var spanName = methodName;

            var expectedSpan = new Span();
            spanTracerStub.Expect(
                x => x.SendClientSpan(
                    Arg<string>.Is.Equal(spanName.ToLower()),
                    Arg<string>.Is.Equal(traceProvider.TraceId),
                    Arg<string>.Is.Equal(traceProvider.ParentSpanId),
                    Arg<string>.Is.Equal(traceProvider.SpanId),
                    Arg<Uri>.Is.Anything)).Return(expectedSpan);

            var result = tracerClient.StartClientTrace(new Uri("https://" + clientServiceName + ".xyz.net:8000" + uriAbsolutePath), methodName, traceProvider);

            Assert.AreEqual(expectedSpan, result);
        }

        [TestMethod]
        public void StartClientSpan_MultipleDomainList()
        {
            var zipkinConfig = CreateZipkinConfigWithDefaultValues();
            zipkinConfig.NotToBeDisplayedDomainList = new List<string> { ".abc.net", ".xyz.net" };
            var tracerClient = SetupZipkinClient(zipkinConfig);
            var zipkinClient = (ZipkinClient)tracerClient;
            spanTracerStub = GetSpanTracerStub();
            zipkinClient.spanTracer = spanTracerStub;
            var clientServiceName = "abc-sandbox";
            var uriAbsolutePath = "/object";
            var methodName = "GET";
            var spanName = methodName;

            var expectedSpan = new Span();
            spanTracerStub.Expect(
                x => x.SendClientSpan(
                    Arg<string>.Is.Equal(spanName.ToLower()),
                    Arg<string>.Is.Equal(traceProvider.TraceId),
                    Arg<string>.Is.Equal(traceProvider.ParentSpanId),
                    Arg<string>.Is.Equal(traceProvider.SpanId),
                    Arg<Uri>.Is.Anything)).Return(expectedSpan);

            var result = tracerClient.StartClientTrace(new Uri("https://" + clientServiceName + ".xyz.net:8000" + uriAbsolutePath), methodName, traceProvider);

            Assert.AreEqual(expectedSpan, result);
        }

        [TestMethod]
        public void StartClientSpan_Exception()
        {
            var tracerClient = SetupZipkinClient();
            var zipkinClient = (ZipkinClient)tracerClient;
            spanTracerStub = GetSpanTracerStub();
            zipkinClient.spanTracer = spanTracerStub;
            var clientServiceName = "abc-sandbox";
            var uriAbsolutePath = "/object";
            var methodName = "GET";
            var spanName = methodName;

            spanTracerStub.Expect(
                x => x.SendClientSpan(
                    Arg<string>.Is.Equal(spanName.ToLower()),
                    Arg<string>.Is.Anything,
                    Arg<string>.Is.Anything,
                    Arg<string>.Is.Anything,
                    Arg<Uri>.Is.Anything)).Throw(new Exception());

            var result = tracerClient.StartClientTrace(new Uri("https://" + clientServiceName + ".xyz.net:8000" + uriAbsolutePath), methodName, traceProvider);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void StartClientSpan_IsTraceOnIsFalse()
        {
            var tracerClient = SetupZipkinClient();
            var zipkinClient = (ZipkinClient)tracerClient;
            zipkinClient.IsTraceOn = false;
            var clientServiceName = "abc-sandbox";
            var clientServiceUri = new Uri("https://" + clientServiceName + ".xyz.net:8000");
            var methodName = "GET";

            var result = tracerClient.StartClientTrace(clientServiceUri, methodName, traceProvider);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void EndClientSpan()
        {
            var returnCode = fixture.Create<short>();
            var tracerClient = SetupZipkinClient();
            var zipkinClient = (ZipkinClient)tracerClient;
            spanTracerStub = GetSpanTracerStub();
            zipkinClient.spanTracer = spanTracerStub;
            var clientSpan = new Span();

            tracerClient.EndClientTrace(clientSpan, returnCode);

            spanTracerStub.AssertWasCalled(x => x.ReceiveClientSpan(clientSpan, returnCode));
        }

        [TestMethod]
        public void EndClientSpan_Exception()
        {
            var returnCode = fixture.Create<short>();
            var tracerClient = SetupZipkinClient();
            var zipkinClient = (ZipkinClient)tracerClient;
            spanTracerStub = GetSpanTracerStub();
            zipkinClient.spanTracer = spanTracerStub;
            var clientSpan = new Span();

            spanTracerStub.Expect(x => x.ReceiveClientSpan(clientSpan, returnCode)).Throw(new Exception());

            tracerClient.EndClientTrace(clientSpan, returnCode);
        }

        [TestMethod]
        public void EndClientSpan_NullClientTrace_DoesntThrow()
        {
            var returnCode = fixture.Create<short>();
            var tracerClient = SetupZipkinClient();
            spanTracerStub = GetSpanTracerStub();

            var called = false;
            spanTracerStub.Stub(x => x.ReceiveClientSpan(Arg<Span>.Is.Anything, Arg<short>.Is.Equal(returnCode)))
                .WhenCalled(x => { called = true; });

            tracerClient.EndClientTrace(null, returnCode);

            Assert.IsFalse(called);
        }

        [TestMethod]
        public void EndClientSpan_IsTraceOnIsFalse_DoesntThrow()
        {
            var returnCode = fixture.Create<short>();
            var tracerClient = SetupZipkinClient();
            spanTracerStub = GetSpanTracerStub();
            var zipkinClient = (ZipkinClient)tracerClient;
            zipkinClient.IsTraceOn = false;

            var called = false;
            spanTracerStub.Stub(x => x.ReceiveClientSpan(Arg<Span>.Is.Anything, Arg<short>.Is.Equal(returnCode)))
                .WhenCalled(x => { called = true; });

            tracerClient.EndClientTrace(new Span(), returnCode);

            Assert.IsFalse(called);
        }

        [TestMethod]
        [TestCategory("TraceRecordTests")]
        public void Record_IsTraceOnIsFalse_DoesNotAddAnnotation()
        {
            // Arrange
            var tracerClient = SetupZipkinClient();
            spanTracerStub = GetSpanTracerStub();
            var zipkinClient = (ZipkinClient)tracerClient;
            zipkinClient.IsTraceOn = false;

            var testSpan = new Span() { Annotations = new List<Annotation>() };

            // Act
            tracerClient.Record(testSpan, "irrelevant");

            // Assert
            Assert.AreEqual(0, testSpan.Annotations.Count, "There are annotations but the trace is off.");
        }

        [TestMethod]
        [TestCategory("TraceRecordTests")]
        public void Record_WithoutValue_AddsAnnotationWithCallerName()
        {
            // Arrange
            var callerMemberName = new StackTrace().GetFrame(0).GetMethod().Name;
            var tracerClient = SetupZipkinClient();
            spanTracerStub = GetSpanTracerStub();
            var zipkinClient = (ZipkinClient)tracerClient;
            zipkinClient.IsTraceOn = true;

            var testSpan = new Span() { Annotations = new List<Annotation>() };

            // Act
            tracerClient.Record(testSpan);

            // Assert
            Assert.AreEqual(1, testSpan.Annotations.Count, "There is not exactly one annotation added.");
            Assert.IsNotNull(
                testSpan.Annotations.SingleOrDefault(a => a.Value == callerMemberName),
                "The record with the caller name is not found in the Annotations."
            );
        }

        [TestMethod]
        [TestCategory("TraceRecordTests")]
        public void RecordBinary_IsTraceOnIsFalse_DoesNotAddBinaryAnnotation()
        {
            // Arrange
            var keyName = "TestKey";
            var testValue = "Some Value";
            var tracerClient = SetupZipkinClient();
            spanTracerStub = GetSpanTracerStub();
            var zipkinClient = (ZipkinClient)tracerClient;
            zipkinClient.IsTraceOn = false;

            var testSpan = new Span() { Binary_annotations = new List<BinaryAnnotation>() };

            // Act
            tracerClient.RecordBinary(testSpan, keyName, testValue);

            // Assert
            Assert.AreEqual(0, testSpan.Binary_annotations.Count, "There are annotations but the trace is off.");
        }

        [TestMethod]
        [TestCategory("TraceRecordTests")]
        public void RecordLocalComponent_WithNotNullValue_AddsLocalComponentAnnotation()
        {
            // Arrange
            var testValue = "Some Value";
            var tracerClient = SetupZipkinClient();
            spanTracerStub = GetSpanTracerStub();
            var zipkinClient = (ZipkinClient)tracerClient;
            zipkinClient.IsTraceOn = true;

            var testSpan = new Span() { Binary_annotations = new List<BinaryAnnotation>() };

            // Act
            tracerClient.RecordLocalComponent(testSpan, testValue);

            // Assert
            var annotation = testSpan.Binary_annotations.SingleOrDefault(a => a.Key == zipkinCoreConstants.LOCAL_COMPONENT);
            Assert.IsNotNull(annotation, "There is no local trace annotation in the binary annotations.");
            Assert.AreEqual(testValue, annotation.Value, "The local component annotation value is not correct.");
        }

        [TestMethod]
        [TestCategory("TraceRecordTests")]
        public void RecordLocalComponent_IsTraceOnIsFalse_DoesNotAddLocalComponentAnnotation()
        {
            // Arrange
            var testValue = "Some Value";
            var tracerClient = SetupZipkinClient();
            spanTracerStub = GetSpanTracerStub();
            var zipkinClient = (ZipkinClient)tracerClient;
            zipkinClient.IsTraceOn = false;

            var testSpan = new Span() { Binary_annotations = new List<BinaryAnnotation>() };

            // Act
            tracerClient.RecordBinary(testSpan, zipkinCoreConstants.LOCAL_COMPONENT, testValue);

            // Assert
            Assert.AreEqual(0, testSpan.Binary_annotations.Count, "There are annotations but the trace is off.");
        }

        private ITracerClient SetupZipkinClient(IZipkinConfig zipkinConfig = null)
        {
            spanCollectorStub = MockRepository.GenerateStub<SpanCollector>(new Uri("http://localhost"), (uint)0, logger);
            spanCollectorBuilder.Expect(x => x.Build(Arg<Uri>.Is.Anything, Arg<uint>.Is.Anything, Arg<ILog>.Is.Equal(logger))).Return(spanCollectorStub);

            traceProvider.Stub(x => x.TraceId).Return(fixture.Create<string>());
            traceProvider.Stub(x => x.SpanId).Return(fixture.Create<string>());
            traceProvider.Stub(x => x.ParentSpanId).Return(fixture.Create<string>());
            traceProvider.Stub(x => x.IsSampled).Return(true);

            var context = MockRepository.GenerateStub<IOwinContext>();
            var request = MockRepository.GenerateStub<IOwinRequest>();
            context.Stub(x => x.Request).Return(request);
            context.Stub(x => x.Environment).Return(new Dictionary<string, object> { { TraceProvider.Key, traceProvider } });

            IZipkinConfig zipkinConfigSetup = zipkinConfig;
            if (zipkinConfig == null)
            {
                zipkinConfigSetup = CreateZipkinConfigWithDefaultValues();
            }

            return new ZipkinClient(logger, zipkinConfigSetup, spanCollectorBuilder, context);
        }

        private IZipkinConfig CreateZipkinConfigWithDefaultValues()
        {
            return new ZipkinConfig
            {
                ZipkinBaseUri = new Uri("http://zipkin.com"),
                Domain = new Uri("http://server.com"),
                SpanProcessorBatchSize = 123,
                ExcludedPathList = new List<string> { "/foo", "/bar", "/baz" },
                SampleRate = 0.5,
                NotToBeDisplayedDomainList = new List<string> { ".xyz.net" }
            };
        }

        private SpanTracer GetSpanTracerStub()
        {
            return
                MockRepository.GenerateStub<SpanTracer>(
                    spanCollectorStub,
                    MockRepository.GenerateStub<ServiceEndpoint>(),
                    new List<string>(),
                    new Uri("http://server.com")
                );
        }
    }
}
