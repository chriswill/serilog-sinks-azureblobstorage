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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using RimDev.Automation.StorageEmulator;

namespace Serilog.Sinks.AzureBlobStorage.Tests
{
    [TestClass]
    public class AzureBlobStorageTests
    {
        private static AzureStorageEmulatorAutomation azureStorage;

        [ClassInitialize]
        public static void Initialize()
        {
            azureStorage.Start();
        }

        [ClassCleanup]
        public static void Cleanup()
        {
            azureStorage.Stop();
            azureStorage.Dispose();
        }

        [TestInitialize]
        public void TestInitialize()
        {
            if (!AzureStorageEmulatorAutomation.IsEmulatorRunning())
                throw new System.Exception("Storage emulator is not running");
            azureStorage.ClearBlobs();
        }

        [TestMethod]
        public void TestMethod1()
        {
        }
    }
}
