/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.Text.Json;

    /// <summary>
    /// The exception that is thrown by
    /// <see cref="FieldValue.FromJsonString"/> when failed to parse JSON.
    /// </summary>
    /// <remarks>
    /// This exception encapsulates the implementation-specific JSON parse
    /// exception as <see cref="Exception.InnerException"/> property.
    /// </remarks>
    public class JsonParseException : NoSQLException
    {

        internal JsonParseException(JsonException inner) :
            base($"Error parsing JSON: {inner.Message}", inner)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="JsonParseException"/>.
        /// </summary>
        public JsonParseException()
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="JsonParseException"/>
        /// with the message that describes the current exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        public JsonParseException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="JsonParseException"/>
        /// with the message that describes the current exception and an inner
        /// exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        /// <param name="inner">The inner exception.</param>
        public JsonParseException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
