﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Script.WebHost;
using Microsoft.Azure.WebJobs.Script.WebHost.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Script.Tests.Diagnostics
{
    public class LinuxContainerEventGeneratorWithConsoleOutputTests : IDisposable
    {
        private readonly MemoryStream _consoleOut = new MemoryStream();
        private readonly string _containerName = "test-container";
        private readonly string _stampName = "test-stamp";
        private readonly string _tenantId = "test-tenant";
        private readonly string _testNodeAddress = "test-address";

        public LinuxContainerEventGeneratorWithConsoleOutputTests()
        {
            var streamWriter = new StreamWriter(_consoleOut);
            streamWriter.AutoFlush = true;
            Console.SetOut(streamWriter);
        }

        public void Dispose()
        {
            var standardOutput = new StreamWriter(Console.OpenStandardOutput());
            standardOutput.AutoFlush = true;
            Console.SetOut(standardOutput);
        }

        private IEnvironment CreateEnvironment(bool consoleDisabled = false, int bufferSize = 0, bool batched = false)
        {
            var mockEnvironment = new Mock<IEnvironment>(MockBehavior.Strict);
            mockEnvironment.Setup(p => p.GetEnvironmentVariable(EnvironmentSettingNames.ContainerName)).Returns(_containerName);
            mockEnvironment.Setup(p => p.GetEnvironmentVariable(EnvironmentSettingNames.WebSiteHomeStampName)).Returns(_stampName);
            mockEnvironment.Setup(p => p.GetEnvironmentVariable(EnvironmentSettingNames.WebSiteStampDeploymentId)).Returns(_tenantId);
            mockEnvironment.Setup(p => p.GetEnvironmentVariable(EnvironmentSettingNames.LinuxNodeIpAddress)).Returns(_testNodeAddress);

            mockEnvironment.Setup(p => p.GetEnvironmentVariable(EnvironmentSettingNames.ConsoleLoggingDisabled)).Returns(consoleDisabled ? "1" : "0");
            mockEnvironment.Setup(p => p.GetEnvironmentVariable(EnvironmentSettingNames.ConsoleLoggingBufferSize)).Returns(bufferSize.ToString());
            mockEnvironment.Setup(p => p.GetEnvironmentVariable(EnvironmentSettingNames.ConsoleLoggingBufferBatched)).Returns(batched ? "1" : "0");
            return mockEnvironment.Object;
        }

        [Fact]
        public void GenerateNothingWhenDisabled()
        {
            var env = CreateEnvironment(consoleDisabled: true);
            var generator = new LinuxContainerEventGenerator(env);

            generator.LogFunctionTraceEvent(LogLevel.Information, "C37E3412-86D1-4B93-BC5A-A2AE09D26C2D", "TestApp", "TestFunction", "TestEvent", "TestSource", "These are the details, lots of details", "This is the summary, a great summary", "TestExceptionType", "Test exception message, with details", "E2D5A6ED-4CE3-4CFD-8878-FD4814F0A1F3", "3AD41658-1C4E-4C9D-B0B9-24F2BDAE2829", "F0AAA9AD-C3A6-48B9-A75E-57BB280EBB53", "TestRuntimeSiteName", "TestSlotName", DateTime.Now);

            using var sr = new StreamReader(_consoleOut);
            _consoleOut.Position = 0;
            var output = sr.ReadToEnd().Trim();

            Assert.Equal(string.Empty, output);
        }

        [Fact]
        public void SingleEventNoBuffer()
        {
            var env = CreateEnvironment(bufferSize: 0);
            var generator = new LinuxContainerEventGenerator(env);

            var timestamp = DateTime.Parse("2023-04-19T14:12:00.0000000Z");
            generator.LogFunctionTraceEvent(LogLevel.Information, "C37E3412-86D1-4B93-BC5A-A2AE09D26C2D", "TestApp", "TestFunction", "TestEvent", "TestSource", "These are the details, lots of details", "This is the summary, a great summary", "TestExceptionType", "Test exception message, with details", "E2D5A6ED-4CE3-4CFD-8878-FD4814F0A1F3", "3AD41658-1C4E-4C9D-B0B9-24F2BDAE2829", "F0AAA9AD-C3A6-48B9-A75E-57BB280EBB53", "TestRuntimeSiteName", "TestSlotName", timestamp);

            using var sr = new StreamReader(_consoleOut);
            _consoleOut.Position = 0;
            var output = sr.ReadToEnd().Trim();

            Assert.Equal($"MS_FUNCTION_LOGS 4,C37E3412-86D1-4B93-BC5A-A2AE09D26C2D,TestApp,TestFunction,TestEvent,TestSource,\"These are the details, lots of details\",\"This is the summary, a great summary\",4.21.0.0,{timestamp.ToString("O")},TestExceptionType,\"Test exception message, with details\",E2D5A6ED-4CE3-4CFD-8878-FD4814F0A1F3,3AD41658-1C4E-4C9D-B0B9-24F2BDAE2829,F0AAA9AD-C3A6-48B9-A75E-57BB280EBB53,TEST-CONTAINER,test-stamp,test-tenant,TestRuntimeSiteName,TestSlotName", output);
        }

        [Fact]
        public async Task SingleEventBuffer()
        {
            var env = CreateEnvironment(bufferSize: 10);
            var generator = new LinuxContainerEventGenerator(env);

            var timestamp = DateTime.Parse("2023-04-19T14:12:00.0000000Z");
            generator.LogFunctionTraceEvent(LogLevel.Information, "C37E3412-86D1-4B93-BC5A-A2AE09D26C2D", "TestApp", "TestFunction", "TestEvent", "TestSource", "These are the details, lots of details", "This is the summary, a great summary", "TestExceptionType", "Test exception message, with details", "E2D5A6ED-4CE3-4CFD-8878-FD4814F0A1F3", "3AD41658-1C4E-4C9D-B0B9-24F2BDAE2829", "F0AAA9AD-C3A6-48B9-A75E-57BB280EBB53", "TestRuntimeSiteName", "TestSlotName", timestamp);
            await Task.Delay(TimeSpan.FromMilliseconds(10));

            using var sr = new StreamReader(_consoleOut);
            _consoleOut.Position = 0;
            var output = sr.ReadToEnd().Trim();

            Assert.Equal($"MS_FUNCTION_LOGS 4,C37E3412-86D1-4B93-BC5A-A2AE09D26C2D,TestApp,TestFunction,TestEvent,TestSource,\"These are the details, lots of details\",\"This is the summary, a great summary\",4.21.0.0,{timestamp.ToString("O")},TestExceptionType,\"Test exception message, with details\",E2D5A6ED-4CE3-4CFD-8878-FD4814F0A1F3,3AD41658-1C4E-4C9D-B0B9-24F2BDAE2829,F0AAA9AD-C3A6-48B9-A75E-57BB280EBB53,TEST-CONTAINER,test-stamp,test-tenant,TestRuntimeSiteName,TestSlotName", output);
        }

        [Fact]
        public async Task MultipleEventsBuffered()
        {
            var env = CreateEnvironment(bufferSize: 10);
            var generator = new LinuxContainerEventGenerator(env);

            var timestamp = DateTime.Parse("2023-04-19T14:12:00.0000000Z");
            generator.LogFunctionTraceEvent(LogLevel.Information, "C37E3412-86D1-4B93-BC5A-A2AE09D26C2D", "TestApp", "TestFunction1", "TestEvent", "TestSource", "These are the details, lots of details", "This is the summary, a great summary", "TestExceptionType", "Test exception message, with details", "E2D5A6ED-4CE3-4CFD-8878-FD4814F0A1F3", "3AD41658-1C4E-4C9D-B0B9-24F2BDAE2829", "F0AAA9AD-C3A6-48B9-A75E-57BB280EBB53", "TestRuntimeSiteName", "TestSlotName", timestamp);
            generator.LogFunctionTraceEvent(LogLevel.Information, "C37E3412-86D1-4B93-BC5A-A2AE09D26C2D", "TestApp", "TestFunction2", "TestEvent", "TestSource", "These are the details, lots of details", "This is the summary, a great summary", "TestExceptionType", "Test exception message, with details", "E2D5A6ED-4CE3-4CFD-8878-FD4814F0A1F3", "3AD41658-1C4E-4C9D-B0B9-24F2BDAE2829", "F0AAA9AD-C3A6-48B9-A75E-57BB280EBB53", "TestRuntimeSiteName", "TestSlotName", timestamp);
            generator.LogFunctionTraceEvent(LogLevel.Information, "C37E3412-86D1-4B93-BC5A-A2AE09D26C2D", "TestApp", "TestFunction3", "TestEvent", "TestSource", "These are the details, lots of details", "This is the summary, a great summary", "TestExceptionType", "Test exception message, with details", "E2D5A6ED-4CE3-4CFD-8878-FD4814F0A1F3", "3AD41658-1C4E-4C9D-B0B9-24F2BDAE2829", "F0AAA9AD-C3A6-48B9-A75E-57BB280EBB53", "TestRuntimeSiteName", "TestSlotName", timestamp);
            await Task.Delay(TimeSpan.FromMilliseconds(10));

            using var sr = new StreamReader(_consoleOut);
            _consoleOut.Position = 0;
            var output = sr.ReadToEnd().Trim().SplitLines();

            Assert.Equal(3, output.Length);

            Assert.Equal($"MS_FUNCTION_LOGS 4,C37E3412-86D1-4B93-BC5A-A2AE09D26C2D,TestApp,TestFunction1,TestEvent,TestSource,\"These are the details, lots of details\",\"This is the summary, a great summary\",4.21.0.0,{timestamp.ToString("O")},TestExceptionType,\"Test exception message, with details\",E2D5A6ED-4CE3-4CFD-8878-FD4814F0A1F3,3AD41658-1C4E-4C9D-B0B9-24F2BDAE2829,F0AAA9AD-C3A6-48B9-A75E-57BB280EBB53,TEST-CONTAINER,test-stamp,test-tenant,TestRuntimeSiteName,TestSlotName", output[0]);
            Assert.Equal($"MS_FUNCTION_LOGS 4,C37E3412-86D1-4B93-BC5A-A2AE09D26C2D,TestApp,TestFunction2,TestEvent,TestSource,\"These are the details, lots of details\",\"This is the summary, a great summary\",4.21.0.0,{timestamp.ToString("O")},TestExceptionType,\"Test exception message, with details\",E2D5A6ED-4CE3-4CFD-8878-FD4814F0A1F3,3AD41658-1C4E-4C9D-B0B9-24F2BDAE2829,F0AAA9AD-C3A6-48B9-A75E-57BB280EBB53,TEST-CONTAINER,test-stamp,test-tenant,TestRuntimeSiteName,TestSlotName", output[1]);
            Assert.Equal($"MS_FUNCTION_LOGS 4,C37E3412-86D1-4B93-BC5A-A2AE09D26C2D,TestApp,TestFunction3,TestEvent,TestSource,\"These are the details, lots of details\",\"This is the summary, a great summary\",4.21.0.0,{timestamp.ToString("O")},TestExceptionType,\"Test exception message, with details\",E2D5A6ED-4CE3-4CFD-8878-FD4814F0A1F3,3AD41658-1C4E-4C9D-B0B9-24F2BDAE2829,F0AAA9AD-C3A6-48B9-A75E-57BB280EBB53,TEST-CONTAINER,test-stamp,test-tenant,TestRuntimeSiteName,TestSlotName", output[2]);
        }

        [Fact]
        public async Task MultipleEventsBatched()
        {
            var env = CreateEnvironment(bufferSize: 10, batched: true);
            var generator = new LinuxContainerEventGenerator(env);

            var timestamp = DateTime.Parse("2023-04-19T14:12:00.0000000Z");
            generator.LogFunctionTraceEvent(LogLevel.Information, "C37E3412-86D1-4B93-BC5A-A2AE09D26C2D", "TestApp", "TestFunction1", "TestEvent", "TestSource", "These are the details, lots of details", "This is the summary, a great summary", "TestExceptionType", "Test exception message, with details", "E2D5A6ED-4CE3-4CFD-8878-FD4814F0A1F3", "3AD41658-1C4E-4C9D-B0B9-24F2BDAE2829", "F0AAA9AD-C3A6-48B9-A75E-57BB280EBB53", "TestRuntimeSiteName", "TestSlotName", timestamp);
            generator.LogFunctionTraceEvent(LogLevel.Information, "C37E3412-86D1-4B93-BC5A-A2AE09D26C2D", "TestApp", "TestFunction2", "TestEvent", "TestSource", "These are the details, lots of details", "This is the summary, a great summary", "TestExceptionType", "Test exception message, with details", "E2D5A6ED-4CE3-4CFD-8878-FD4814F0A1F3", "3AD41658-1C4E-4C9D-B0B9-24F2BDAE2829", "F0AAA9AD-C3A6-48B9-A75E-57BB280EBB53", "TestRuntimeSiteName", "TestSlotName", timestamp);
            generator.LogFunctionTraceEvent(LogLevel.Information, "C37E3412-86D1-4B93-BC5A-A2AE09D26C2D", "TestApp", "TestFunction3", "TestEvent", "TestSource", "These are the details, lots of details", "This is the summary, a great summary", "TestExceptionType", "Test exception message, with details", "E2D5A6ED-4CE3-4CFD-8878-FD4814F0A1F3", "3AD41658-1C4E-4C9D-B0B9-24F2BDAE2829", "F0AAA9AD-C3A6-48B9-A75E-57BB280EBB53", "TestRuntimeSiteName", "TestSlotName", timestamp);
            await Task.Delay(TimeSpan.FromMilliseconds(10));

            using var sr = new StreamReader(_consoleOut);
            _consoleOut.Position = 0;
            var output = sr.ReadToEnd().Trim().SplitLines();

            Assert.Equal(3, output.Length);

            Assert.Equal($"MS_FUNCTION_LOGS 4,C37E3412-86D1-4B93-BC5A-A2AE09D26C2D,TestApp,TestFunction1,TestEvent,TestSource,\"These are the details, lots of details\",\"This is the summary, a great summary\",4.21.0.0,{timestamp.ToString("O")},TestExceptionType,\"Test exception message, with details\",E2D5A6ED-4CE3-4CFD-8878-FD4814F0A1F3,3AD41658-1C4E-4C9D-B0B9-24F2BDAE2829,F0AAA9AD-C3A6-48B9-A75E-57BB280EBB53,TEST-CONTAINER,test-stamp,test-tenant,TestRuntimeSiteName,TestSlotName", output[0]);
            Assert.Equal($"MS_FUNCTION_LOGS 4,C37E3412-86D1-4B93-BC5A-A2AE09D26C2D,TestApp,TestFunction2,TestEvent,TestSource,\"These are the details, lots of details\",\"This is the summary, a great summary\",4.21.0.0,{timestamp.ToString("O")},TestExceptionType,\"Test exception message, with details\",E2D5A6ED-4CE3-4CFD-8878-FD4814F0A1F3,3AD41658-1C4E-4C9D-B0B9-24F2BDAE2829,F0AAA9AD-C3A6-48B9-A75E-57BB280EBB53,TEST-CONTAINER,test-stamp,test-tenant,TestRuntimeSiteName,TestSlotName", output[1]);
            Assert.Equal($"MS_FUNCTION_LOGS 4,C37E3412-86D1-4B93-BC5A-A2AE09D26C2D,TestApp,TestFunction3,TestEvent,TestSource,\"These are the details, lots of details\",\"This is the summary, a great summary\",4.21.0.0,{timestamp.ToString("O")},TestExceptionType,\"Test exception message, with details\",E2D5A6ED-4CE3-4CFD-8878-FD4814F0A1F3,3AD41658-1C4E-4C9D-B0B9-24F2BDAE2829,F0AAA9AD-C3A6-48B9-A75E-57BB280EBB53,TEST-CONTAINER,test-stamp,test-tenant,TestRuntimeSiteName,TestSlotName", output[2]);
        }

        [Fact]
        public async Task MultipleEventsBatchedWithTinyBuffer()
        {
            // setup in a state where the buffer isn't being processed and can only hold two messages
            var env = CreateEnvironment(bufferSize: 2, batched: true);
            var consoleWriter = new ConsoleWriter(env, LinuxContainerEventGenerator.LogUnhandledException, consoleBufferTimeout: TimeSpan.FromMilliseconds(500), autoStart: false);
            var generator = new LinuxContainerEventGenerator(env, consoleWriter);

            var timestamp = DateTime.Parse("2023-04-19T14:12:00.0000000Z");
            generator.LogFunctionTraceEvent(LogLevel.Information, "C37E3412-86D1-4B93-BC5A-A2AE09D26C2D", "TestApp", "TestFunction1", "TestEvent", "TestSource", "These are the details, lots of details", "This is the summary, a great summary", "TestExceptionType", "Test exception message, with details", "E2D5A6ED-4CE3-4CFD-8878-FD4814F0A1F3", "3AD41658-1C4E-4C9D-B0B9-24F2BDAE2829", "F0AAA9AD-C3A6-48B9-A75E-57BB280EBB53", "TestRuntimeSiteName", "TestSlotName", timestamp);
            generator.LogFunctionTraceEvent(LogLevel.Information, "C37E3412-86D1-4B93-BC5A-A2AE09D26C2D", "TestApp", "TestFunction2", "TestEvent", "TestSource", "These are the details, lots of details", "This is the summary, a great summary", "TestExceptionType", "Test exception message, with details", "E2D5A6ED-4CE3-4CFD-8878-FD4814F0A1F3", "3AD41658-1C4E-4C9D-B0B9-24F2BDAE2829", "F0AAA9AD-C3A6-48B9-A75E-57BB280EBB53", "TestRuntimeSiteName", "TestSlotName", timestamp);

            // The third write will block until the first two are flushed.
            var logTask = Task.Run(() => generator.LogFunctionTraceEvent(LogLevel.Information, "C37E3412-86D1-4B93-BC5A-A2AE09D26C2D", "TestApp", "TestFunction3", "TestEvent", "TestSource", "These are the details, lots of details", "This is the summary, a great summary", "TestExceptionType", "Test exception message, with details", "E2D5A6ED-4CE3-4CFD-8878-FD4814F0A1F3", "3AD41658-1C4E-4C9D-B0B9-24F2BDAE2829", "F0AAA9AD-C3A6-48B9-A75E-57BB280EBB53", "TestRuntimeSiteName", "TestSlotName", timestamp));
            await Task.Delay(TimeSpan.FromMilliseconds(10));
            Assert.False(logTask.IsCompleted);

            var logProcessingTask = consoleWriter.ProcessConsoleBufferAsync();
            await Task.Delay(TimeSpan.FromMilliseconds(10));
            Assert.True(logTask.IsCompleted);

            using var sr = new StreamReader(_consoleOut);
            _consoleOut.Position = 0;
            var output = sr.ReadToEnd().Trim().SplitLines();

            Assert.Equal(3, output.Length);

            Assert.Equal($"MS_FUNCTION_LOGS 4,C37E3412-86D1-4B93-BC5A-A2AE09D26C2D,TestApp,TestFunction1,TestEvent,TestSource,\"These are the details, lots of details\",\"This is the summary, a great summary\",4.21.0.0,{timestamp.ToString("O")},TestExceptionType,\"Test exception message, with details\",E2D5A6ED-4CE3-4CFD-8878-FD4814F0A1F3,3AD41658-1C4E-4C9D-B0B9-24F2BDAE2829,F0AAA9AD-C3A6-48B9-A75E-57BB280EBB53,TEST-CONTAINER,test-stamp,test-tenant,TestRuntimeSiteName,TestSlotName", output[0]);
            Assert.Equal($"MS_FUNCTION_LOGS 4,C37E3412-86D1-4B93-BC5A-A2AE09D26C2D,TestApp,TestFunction2,TestEvent,TestSource,\"These are the details, lots of details\",\"This is the summary, a great summary\",4.21.0.0,{timestamp.ToString("O")},TestExceptionType,\"Test exception message, with details\",E2D5A6ED-4CE3-4CFD-8878-FD4814F0A1F3,3AD41658-1C4E-4C9D-B0B9-24F2BDAE2829,F0AAA9AD-C3A6-48B9-A75E-57BB280EBB53,TEST-CONTAINER,test-stamp,test-tenant,TestRuntimeSiteName,TestSlotName", output[1]);
            Assert.Equal($"MS_FUNCTION_LOGS 4,C37E3412-86D1-4B93-BC5A-A2AE09D26C2D,TestApp,TestFunction3,TestEvent,TestSource,\"These are the details, lots of details\",\"This is the summary, a great summary\",4.21.0.0,{timestamp.ToString("O")},TestExceptionType,\"Test exception message, with details\",E2D5A6ED-4CE3-4CFD-8878-FD4814F0A1F3,3AD41658-1C4E-4C9D-B0B9-24F2BDAE2829,F0AAA9AD-C3A6-48B9-A75E-57BB280EBB53,TEST-CONTAINER,test-stamp,test-tenant,TestRuntimeSiteName,TestSlotName", output[2]);
        }

        [Fact]
        public void MultipleEventsBatchedWithTinyBuffer_WritesDirectlyToConsoleOnTimeout()
        {
            // setup in a state where the buffer isn't being processed and can only hold two messages
            var env = CreateEnvironment(bufferSize: 2, batched: true);
            var consoleWriter = new ConsoleWriter(env, LinuxContainerEventGenerator.LogUnhandledException, consoleBufferTimeout: TimeSpan.FromMilliseconds(10), autoStart: false);
            var generator = new LinuxContainerEventGenerator(env, consoleWriter);

            var timestamp = DateTime.Parse("2023-04-19T14:12:00.0000000Z");
            generator.LogFunctionTraceEvent(LogLevel.Information, "C37E3412-86D1-4B93-BC5A-A2AE09D26C2D", "TestApp", "TestFunction1", "TestEvent", "TestSource", "These are the details, lots of details", "This is the summary, a great summary", "TestExceptionType", "Test exception message, with details", "E2D5A6ED-4CE3-4CFD-8878-FD4814F0A1F3", "3AD41658-1C4E-4C9D-B0B9-24F2BDAE2829", "F0AAA9AD-C3A6-48B9-A75E-57BB280EBB53", "TestRuntimeSiteName", "TestSlotName", timestamp);
            generator.LogFunctionTraceEvent(LogLevel.Information, "C37E3412-86D1-4B93-BC5A-A2AE09D26C2D", "TestApp", "TestFunction2", "TestEvent", "TestSource", "These are the details, lots of details", "This is the summary, a great summary", "TestExceptionType", "Test exception message, with details", "E2D5A6ED-4CE3-4CFD-8878-FD4814F0A1F3", "3AD41658-1C4E-4C9D-B0B9-24F2BDAE2829", "F0AAA9AD-C3A6-48B9-A75E-57BB280EBB53", "TestRuntimeSiteName", "TestSlotName", timestamp);

            // This write will block until the above timeout expires, and then it should write directly to the console
            generator.LogFunctionTraceEvent(LogLevel.Information, "C37E3412-86D1-4B93-BC5A-A2AE09D26C2D", "TestApp", "TestFunction3", "TestEvent", "TestSource", "These are the details, lots of details", "This is the summary, a great summary", "TestExceptionType", "Test exception message, with details", "E2D5A6ED-4CE3-4CFD-8878-FD4814F0A1F3", "3AD41658-1C4E-4C9D-B0B9-24F2BDAE2829", "F0AAA9AD-C3A6-48B9-A75E-57BB280EBB53", "TestRuntimeSiteName", "TestSlotName", timestamp);

            using var sr = new StreamReader(_consoleOut);
            _consoleOut.Position = 0;
            var output = sr.ReadToEnd().Trim().SplitLines();

            // The first two messages are still stuck in the buffer. The third message will have been written to the console.
            // We should also have a log for the timeout exception that occurred while waiting for the buffer to become available.
            Assert.Equal(2, output.Length);
            Assert.StartsWith("MS_FUNCTION_LOGS 2,,,,,LogUnhandledException,\"System.AggregateException: One or more errors occurred. (The operation has timed out.)", output[0]);
            Assert.Equal($"MS_FUNCTION_LOGS 4,C37E3412-86D1-4B93-BC5A-A2AE09D26C2D,TestApp,TestFunction3,TestEvent,TestSource,\"These are the details, lots of details\",\"This is the summary, a great summary\",4.21.0.0,{timestamp.ToString("O")},TestExceptionType,\"Test exception message, with details\",E2D5A6ED-4CE3-4CFD-8878-FD4814F0A1F3,3AD41658-1C4E-4C9D-B0B9-24F2BDAE2829,F0AAA9AD-C3A6-48B9-A75E-57BB280EBB53,TEST-CONTAINER,test-stamp,test-tenant,TestRuntimeSiteName,TestSlotName", output[1]);
        }
    }
}
