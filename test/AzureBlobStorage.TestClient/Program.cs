// Copyright 2018 CloudScope, LLC
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

using Microsoft.WindowsAzure.Storage;
using Serilog;
using System;

namespace AzureBlobStorage.TestClient
{
    class Program
    {
        private static string connectionString = "";

        static void Main(string[] args)
        {            
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.AzureBlobStorage(connectionString, Serilog.Events.LogEventLevel.Information, null, "{yyyy}/{MM}/{dd}/log.txt")
                .CreateLogger();

            Log.Information("Hello World!");
        }        
    }
}
