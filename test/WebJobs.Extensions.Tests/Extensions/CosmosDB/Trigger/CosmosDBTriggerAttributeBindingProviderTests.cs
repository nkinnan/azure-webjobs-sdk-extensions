﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs.Extensions.CosmosDB;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.CosmosDB.Trigger
{
    public class CosmosDBTriggerAttributeBindingProviderTests
    {
        [Theory]
        [MemberData(nameof(InvalidCosmosDBTriggerParameters))]
        public async Task InvalidParameters_Fail(ParameterInfo parameter)
        {
            CosmosDBTriggerAttributeBindingProvider provider = new CosmosDBTriggerAttributeBindingProvider(new DefaultNameResolver(), CreateConfiguration());

            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None)));

            Assert.NotNull(ex);
        }

        [Theory]
        [MemberData(nameof(ValidCosmosDBTriggerBindigsWithEnvironmentParameters))]
        public async Task ValidParametersWithEnvironment_Succeed(ParameterInfo parameter)
        {
            var nameResolver = new TestNameResolver();
            nameResolver.Values[CosmosDBConfiguration.AzureWebJobsCosmosDBConnectionStringName] = "AccountEndpoint=https://fromEnvironment;AccountKey=someKey;";
            nameResolver.Values["CosmosDBConnectionString"] = "AccountEndpoint=https://fromSettings;AccountKey=someKey;";

            CosmosDBTriggerAttributeBindingProvider provider = new CosmosDBTriggerAttributeBindingProvider(nameResolver, CreateConfiguration());

            CosmosDBTriggerBinding binding = (CosmosDBTriggerBinding)await provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None));

            Assert.Equal(binding.TriggerValueType, typeof(IReadOnlyList<Document>));
            Assert.Equal(binding.DocumentCollectionLocation.Uri, new Uri("https://fromEnvironment"));
        }

        [Theory]
        [MemberData(nameof(ValidCosmosDBTriggerBindigsWithAppSettingsParameters))]
        public async Task ValidParametersWithAppSettings_Succeed(ParameterInfo parameter)
        {
            var nameResolver = new TestNameResolver();
            nameResolver.Values[CosmosDBConfiguration.AzureWebJobsCosmosDBConnectionStringName] = "AccountEndpoint=https://fromEnvironment;AccountKey=someKey;";
            nameResolver.Values["CosmosDBConnectionString"] = "AccountEndpoint=https://fromSettings;AccountKey=someKey;";

            CosmosDBTriggerAttributeBindingProvider provider = new CosmosDBTriggerAttributeBindingProvider(nameResolver, CreateConfiguration());

            CosmosDBTriggerBinding binding = (CosmosDBTriggerBinding)await provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None));

            Assert.Equal(binding.TriggerValueType, typeof(IReadOnlyList<Document>));
            Assert.Equal(binding.DocumentCollectionLocation.Uri, new Uri("https://fromSettings"));
        }

        [Theory]
        [MemberData(nameof(ValidCosmosDBTriggerBindigsWithDatabaseAndCollectionSettingsParameters))]
        public async Task ValidCosmosDBTriggerBindigsWithDatabaseAndCollectionSettings_Succeed(ParameterInfo parameter)
        {
            var nameResolver = new TestNameResolver();
            nameResolver.Values[CosmosDBConfiguration.AzureWebJobsCosmosDBConnectionStringName] = "AccountEndpoint=https://fromEnvironment;AccountKey=someKey;";
            nameResolver.Values["CosmosDBConnectionString"] = "AccountEndpoint=https://fromSettings;AccountKey=someKey;";
            nameResolver.Values["%aDatabase%"] = "myDatabase";
            nameResolver.Values["%aCollection%"] = "myCollection";

            CosmosDBTriggerAttributeBindingProvider provider = new CosmosDBTriggerAttributeBindingProvider(nameResolver, CreateConfiguration());

            CosmosDBTriggerBinding binding = (CosmosDBTriggerBinding)await provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None));

            Assert.Equal(binding.TriggerValueType, typeof(IReadOnlyList<Document>));
            Assert.Equal(binding.DocumentCollectionLocation.Uri, new Uri("https://fromEnvironment"));
            Assert.Equal(binding.DocumentCollectionLocation.DatabaseName, "myDatabase");
            Assert.Equal(binding.DocumentCollectionLocation.CollectionName, "myCollection");
            Assert.Equal(binding.LeaseCollectionLocation.DatabaseName, "myDatabase");
            Assert.Equal(binding.LeaseCollectionLocation.CollectionName, "leases");
        }

        [Theory]
        [MemberData(nameof(ValidCosmosDBTriggerBindigsDifferentConnectionsParameters))]
        public async Task ValidCosmosDBTriggerBindigsDifferentConnections_Succeed(ParameterInfo parameter)
        {
            var nameResolver = new TestNameResolver();
            nameResolver.Values[CosmosDBConfiguration.AzureWebJobsCosmosDBConnectionStringName] = "AccountEndpoint=https://fromEnvironment;AccountKey=someKey;";
            nameResolver.Values["CosmosDBConnectionString"] = "AccountEndpoint=https://fromSettings;AccountKey=someKey;";
            nameResolver.Values["LeaseCosmosDBConnectionString"] = "AccountEndpoint=https://fromSettingsLease;AccountKey=someKey;";

            CosmosDBTriggerAttributeBindingProvider provider = new CosmosDBTriggerAttributeBindingProvider(nameResolver, CreateConfiguration());

            CosmosDBTriggerBinding binding = (CosmosDBTriggerBinding)await provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None));

            Assert.Equal(binding.TriggerValueType, typeof(IReadOnlyList<Document>));
            Assert.Equal(binding.DocumentCollectionLocation.Uri, new Uri("https://fromSettings"));
            Assert.Equal(binding.LeaseCollectionLocation.Uri, new Uri("https://fromSettingsLease"));
        }

        [Fact]
        public void TryAndConvertToDocumentList_Fail()
        {
            IReadOnlyList<Document> convertedValue;
            Assert.False(CosmosDBTriggerBinding.TryAndConvertToDocumentList(null, out convertedValue));
            Assert.False(CosmosDBTriggerBinding.TryAndConvertToDocumentList("some weird string", out convertedValue));
            Assert.False(CosmosDBTriggerBinding.TryAndConvertToDocumentList(Guid.NewGuid(), out convertedValue));
        }

        [Fact]
        public void TryAndConvertToDocumentList_Succeed()
        {
            IReadOnlyList<Document> convertedValue;
            Assert.True(CosmosDBTriggerBinding.TryAndConvertToDocumentList("[{\"id\":\"123\"}]", out convertedValue));
            Assert.Equal("123", convertedValue[0].Id);

            IReadOnlyList<Document> triggerValue = new List<Document>() { new Document() { Id = "123" } };
            Assert.True(CosmosDBTriggerBinding.TryAndConvertToDocumentList(triggerValue, out convertedValue));
            Assert.Equal(triggerValue[0].Id, convertedValue[0].Id);
        }

        public static IEnumerable<object[]> InvalidCosmosDBTriggerParameters
        {
            get { return InvalidCosmosDBTriggerBindigs.GetParameters(); }
        }

        public static IEnumerable<object[]> ValidCosmosDBTriggerBindigsWithAppSettingsParameters
        {
            get { return ValidCosmosDBTriggerBindigsWithAppSettings.GetParameters(); }
        }

        public static IEnumerable<object[]> ValidCosmosDBTriggerBindigsWithDatabaseAndCollectionSettingsParameters
        {
            get { return ValidCosmosDBTriggerBindigsWithDatabaseAndCollectionSettings.GetParameters(); }
        }

        public static IEnumerable<object[]> ValidCosmosDBTriggerBindigsDifferentConnectionsParameters
        {
            get { return ValidCosmosDBTriggerBindigsDifferentConnections.GetParameters(); }
        }

        public static IEnumerable<object[]> ValidCosmosDBTriggerBindigsWithEnvironmentParameters
        {
            get { return ValidCosmosDBTriggerBindigsWithEnvironment.GetParameters(); }
        }

        private static ParameterInfo GetFirstParameter(Type type, string methodName)
        {
            var methodInfo = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            var paramInfo = methodInfo.GetParameters().First();

            return paramInfo;
        }

        private static class InvalidCosmosDBTriggerBindigs
        {
            public static void Func1([CosmosDBTrigger("aDatabase", "aCollection", ConnectionStringSetting = "notAConnectionString")] IReadOnlyList<Document> docs)
            {

            }

            public static void Func2([CosmosDBTrigger("aDatabase", "aCollection", ConnectionStringSetting = "notAConnectionString", LeaseConnectionStringSetting = "notAConnectionString", LeaseDatabaseName = "aDatabase", LeaseCollectionName = "aCollection")] IReadOnlyList<Document> docs)
            {

            }

            public static void Func3([CosmosDBTrigger("aDatabase", "aCollection", ConnectionStringSetting = "CosmosDBConnectionString", LeaseConnectionStringSetting = "CosmosDBConnectionString", LeaseDatabaseName = "aDatabase", LeaseCollectionName = "aCollection")] IReadOnlyList<Document> docs)
            {

            }

            public static void Func4([CosmosDBTrigger("aDatabase", "leases", ConnectionStringSetting = "CosmosDBConnectionString")] IReadOnlyList<Document> docs)
            {

            }

            public static IEnumerable<ParameterInfo[]> GetParameters()
            {
                var type = typeof(InvalidCosmosDBTriggerBindigs);

                return new[]
                {
                    new[] { GetFirstParameter(type, "Func1") },
                    new[] { GetFirstParameter(type, "Func2") },
                    new[] { GetFirstParameter(type, "Func3") },
                    new[] { GetFirstParameter(type, "Func4") }
                };
            }
        }

        private static class ValidCosmosDBTriggerBindigsWithAppSettings
        {
            public static void Func1([CosmosDBTrigger("aDatabase", "aCollection", ConnectionStringSetting = "CosmosDBConnectionString")] IReadOnlyList<Document> docs)
            {

            }

            public static void Func2([CosmosDBTrigger("aDatabase", "aCollection", ConnectionStringSetting = "CosmosDBConnectionString", LeaseConnectionStringSetting = "CosmosDBConnectionString", LeaseDatabaseName = "aDatabase", LeaseCollectionName = "aLeaseCollection")] IReadOnlyList<Document> docs)
            {

            }


            public static void Func3([CosmosDBTrigger("aDatabase", "aCollection", ConnectionStringSetting = "CosmosDBConnectionString")] JArray docs)
            {

            }

            public static IEnumerable<ParameterInfo[]> GetParameters()
            {
                var type = typeof(ValidCosmosDBTriggerBindigsWithAppSettings);

                return new[]
                {
                    new[] { GetFirstParameter(type, "Func1") },
                    new[] { GetFirstParameter(type, "Func2") },
                    new[] { GetFirstParameter(type, "Func3") }
                };
            }
        }

        private static class ValidCosmosDBTriggerBindigsDifferentConnections
        {
            public static void Func1([CosmosDBTrigger("aDatabase", "aCollection", ConnectionStringSetting = "CosmosDBConnectionString", LeaseConnectionStringSetting = "LeaseCosmosDBConnectionString")] IReadOnlyList<Document> docs)
            {

            }

            public static void Func2([CosmosDBTrigger("aDatabase", "aCollection", ConnectionStringSetting = "CosmosDBConnectionString", LeaseConnectionStringSetting = "LeaseCosmosDBConnectionString", LeaseDatabaseName = "aDatabase", LeaseCollectionName = "aLeaseCollection")] IReadOnlyList<Document> docs)
            {

            }

            public static IEnumerable<ParameterInfo[]> GetParameters()
            {
                var type = typeof(ValidCosmosDBTriggerBindigsDifferentConnections);

                return new[]
                {
                    new[] { GetFirstParameter(type, "Func1") },
                    new[] { GetFirstParameter(type, "Func2") }
                };
            }
        }

        private static class ValidCosmosDBTriggerBindigsWithEnvironment
        {
            public static void Func1([CosmosDBTrigger("aDatabase", "aCollection")] IReadOnlyList<Document> docs)
            {

            }

            public static void Func2([CosmosDBTrigger("aDatabase", "aCollection")] JArray docs)
            {

            }

            public static void Func3([CosmosDBTrigger("aDatabase", "aCollection", LeaseDatabaseName = "aDatabase", LeaseCollectionName = "aLeaseCollection")] IReadOnlyList<Document> docs)
            {

            }

            public static IEnumerable<ParameterInfo[]> GetParameters()
            {
                var type = typeof(ValidCosmosDBTriggerBindigsWithEnvironment);

                return new[]
                {
                    new[] { GetFirstParameter(type, "Func1") },
                    new[] { GetFirstParameter(type, "Func2") },
                    new[] { GetFirstParameter(type, "Func3") }
                };
            }
        }

        private static class ValidCosmosDBTriggerBindigsWithDatabaseAndCollectionSettings
        {
            public static void Func1([CosmosDBTrigger("%aDatabase%", "%aCollection%")] IReadOnlyList<Document> docs)
            {

            }

            public static void Func2([CosmosDBTrigger("%aDatabase%", "%aCollection%")] JArray docs)
            {

            }

            public static void Func3([CosmosDBTrigger("%aDatabase%", "%aCollection%", LeaseDatabaseName = "%aDatabase%")] IReadOnlyList<Document> docs)
            {

            }

            public static IEnumerable<ParameterInfo[]> GetParameters()
            {
                var type = typeof(ValidCosmosDBTriggerBindigsWithDatabaseAndCollectionSettings);

                return new[]
                {
                    new[] { GetFirstParameter(type, "Func1") },
                    new[] { GetFirstParameter(type, "Func2") },
                    new[] { GetFirstParameter(type, "Func3") }
                };
            }
        }

        private CosmosDBConfiguration CreateConfiguration()
        {
            return new CosmosDBConfiguration
            {
                ConnectionString = "AccountEndpoint=https://someuri;AccountKey=c29tZV9rZXk=;",
            };
        }
    }
}