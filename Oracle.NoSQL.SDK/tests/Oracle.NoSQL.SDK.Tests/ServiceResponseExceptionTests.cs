/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Tests
{
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ServiceResponseExceptionTests
    {
        [TestMethod]
        public void TestMessageDoesNotIncludeResponseMessage()
        {
            const string responseMessage =
                "Login failed for user admin with password secret-value";
            var ex = new ServiceResponseException(
                HttpStatusCode.InternalServerError,
                "Internal Server Error",
                responseMessage);

            Assert.AreEqual(
                "Unsuccessful HTTP response: 500 Internal Server Error",
                ex.Message);
            Assert.AreEqual(responseMessage, ex.ResponseMessage);
            Assert.IsFalse(ex.Message.Contains(responseMessage));
            Assert.IsFalse(ex.Message.Contains("secret-value"));
        }

        [TestMethod]
        public async Task
            TestHttpResponseExceptionDoesNotIncludeResponseBodyInMessage()
        {
            const string responseMessage =
                "Unauthorized request with token secret-token";
            using var response = new HttpResponseMessage(
                HttpStatusCode.Unauthorized)
            {
                ReasonPhrase = "Unauthorized",
                Content = new StringContent(responseMessage)
            };

            var ex = await HttpRequestUtils.CreateServiceResponseExceptionAsync(
                response);

            Assert.AreEqual("Unsuccessful HTTP response: 401 Unauthorized",
                ex.Message);
            Assert.AreEqual(responseMessage, ex.ResponseMessage);
            Assert.IsFalse(ex.Message.Contains(responseMessage));
            Assert.IsFalse(ex.Message.Contains("secret-token"));
        }
    }
}
