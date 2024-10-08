#region Copyright notice and license
// Copyright 2019 The gRPC Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// https://github.com/dotnet/AspNetCore.Docs/blob/main/aspnetcore/grpc/test-services/sample/Tests/Server/IntegrationTests/Helpers/ForwardingLoggerProvider.cs
#endregion

using System;
using Microsoft.Extensions.Logging;

namespace ProjectOrigin.Vault.Tests.TestClassFixtures.GrpcHelpers
{
    internal class ForwardingLoggerProvider : ILoggerProvider
    {
        private readonly LogMessage _logAction;

        public ForwardingLoggerProvider(LogMessage logAction)
        {
            _logAction = logAction;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new ForwardingLogger(categoryName, _logAction);
        }

        public void Dispose()
        {
        }

        internal class ForwardingLogger : ILogger, IDisposable
        {
            private readonly string _categoryName;
            private readonly LogMessage _logAction;

            public ForwardingLogger(string categoryName, LogMessage logAction)
            {
                _categoryName = categoryName;
                _logAction = logAction;
            }

            IDisposable ILogger.BeginScope<TState>(TState state)
            {
                return this;
            }

            public void Dispose()
            {

            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                _logAction(logLevel, _categoryName, eventId, formatter(state, exception), exception);
            }
        }
    }
}
