/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.Driver
{
    using System;
    using System.Net;
    using static HttpRequestUtils;

    /// <summary>
    /// The exception that is thrown when the service returns unsuccessful
    /// HTTP response.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This exception allows you to access HTTP status code and HTTP status
    /// message as well as the server response message sent in the body of
    /// the HTTP response.
    /// </para>
    /// <para>
    /// Whether this exception is retryable depends on the HTTP status code.
    /// Retryable status codes are with values 500 and above and include the
    /// following:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// <see cref="HttpStatusCode.InternalServerError"/>
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <see cref="HttpStatusCode.BadGateway"/>
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <see cref="HttpStatusCode.ServiceUnavailable"/>
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <see cref="HttpStatusCode.GatewayTimeout"/>
    /// </description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public class ServiceResponseException : NoSQLException
    {
        private static string GetMessage(HttpStatusCode statusCode,
            string reasonPhrase, string content)
        {
            var message = "Unsuccessful HTTP response: " +
                          $"{(int)statusCode} {reasonPhrase}";
            if (content != null)
            {
                message += $". Error output: {content}";
            }

            return message;
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="ServiceResponseException"/>.
        /// </summary>
        public ServiceResponseException()
        {
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="ServiceResponseException"/> with the message that
        /// describes the current exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        public ServiceResponseException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="ServiceResponseException"/> with the message that
        /// describes the current exception and an inner exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        /// <param name="inner">The inner exception.</param>
        public ServiceResponseException(string message, Exception inner)
            : base(message, inner)
        {
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="ServiceResponseException"/> with the HTTP status code,
        /// HTTP status message and HTTP response message.
        /// </summary>
        /// <remarks>
        /// The value of <see cref="Exception.Message"/> is generated from
        /// <paramref name="statusCode"/> and <paramref name="reasonPhrase"/>.
        /// </remarks>
        /// <param name="statusCode">HTTP status code.</param>
        /// <param name="reasonPhrase">HTTP status message.</param>
        /// <param name="responseMessage">Message in the body of the HTTP
        /// response.</param>
        public ServiceResponseException(HttpStatusCode statusCode,
            string reasonPhrase, string responseMessage) :
            base(GetMessage(statusCode, reasonPhrase, responseMessage))
        {
            StatusCode = statusCode;
            StatusMessage = reasonPhrase;
            ResponseMessage = responseMessage;

            // Need to discuss if the below is correct
            IsRetryable = IsStatusCodeRetryable(statusCode);
        }

        /// <summary>
        /// Gets the value indicating whether the operation that has thrown
        /// this exception may be retried.
        /// </summary>
        /// <value>The value indicating whether this instance is retryable.
        /// See the discussion in the remarks section.</value>
        public override bool IsRetryable { get; }

        /// <summary>
        /// Gets the HTTP status code.
        /// </summary>
        /// <value>HTTP status code.</value>
        public HttpStatusCode StatusCode { get; }

        /// <summary>
        /// Gets the HTTP status message.
        /// </summary>
        /// <value>HTTP status message.</value>
        public string StatusMessage { get; }

        /// <summary>
        /// Gets the service response message.
        /// </summary>
        /// <value>Message sent within the body of the service HTTP response.
        /// </value>
        public string ResponseMessage { get; }
    }

}
