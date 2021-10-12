/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.Driver
{
    using System;
    using static ValidateUtils;

    /// <summary>
    /// Defines a range of values to be used in a DeleteRange operation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class defines a range of values to be used in a DeleteRange
    /// operation, as specified in <see cref="DeleteRangeOptions.FieldRange"/>
    /// for that operation.  It is used by
    /// <see cref="M:Oracle.NoSQL.Driver.NoSQLClient.DeleteRangeAsync*"/> and
    /// <see cref="M:Oracle.NoSQL.Driver.NoSQLClient.GetDeleteRangeAsyncEnumerable*"/>
    /// APIs.
    /// </para>
    /// <para>
    /// <see cref="FieldRange"/> is used as the least significant component in
    /// a partially specified key value in order to create a value range for
    /// an operation that returns multiple rows or keys. The data types
    /// supported by FieldRange are limited to the atomic types which are
    /// valid for primary keys.
    /// </para>
    /// <para>
    /// The <em>least significant component</em> of a key is the first
    /// component of the key that is not fully specified.  For example, if the
    /// primary key for a table is defined as the tuple &lt;a, b, c&gt; a
    /// <see cref="FieldRange"/> can be specified for <em>a</em> if the
    /// primary key supplied is empty.  A <see cref="FieldRange"/> can be
    /// specified for <em>b</em> if the primary key supplied to the operation
    /// has a concrete value for <em>a</em> but not for <em>b</em> or
    /// <em>c</em>.
    /// </para>
    /// <para>
    /// This object is used to scope a DeleteRange operation.  The field name
    /// specified must name a field in a table's primary key.  The values used
    /// must be of the same type and that type must match the type of the
    /// field specified.
    /// </para>
    /// <para>
    /// You may specify the <see cref="FieldValue"/> for lower bound, upper
    /// bound or both.  Each bound may be either inclusive, meaning the range
    /// starts with (for lower bound) or ends with (for upper bound) with this
    /// value, or exclusive, meaning the range starts after (for lower bound)
    /// or ends before (for upper bound) this value.  Properties
    /// <see cref="FieldRange.StartsWith"/> and
    /// <see cref="FieldRange.EndsWith"/> specify inclusive bounds.
    /// Properties <see cref="FieldRange.StartsAfter"/> and
    /// <see cref="FieldRange.EndsBefore"/> specify exclusive bounds.  Note
    /// that for each end of the range you may specify either inclusive or
    /// exclusive bound, but not both.
    /// </para>
    /// <para>
    /// Validation of the <see cref="FieldValue"/> object is performed when it
    /// is used in an operation.  Validation includes verifying that the field
    /// is in the required key and, in the case of a composite key, that the
    /// field is in the proper order relative to the key used in the operation.
    /// </para>
    /// </remarks>
    /// <example>
    /// Instantiating <see cref="FieldRange"/> with TIMESTAMP value and only
    /// upper end of the range.
    /// <code>
    /// var fieldRange = new FieldRange("deliveryDate")
    /// {
    ///     EndsBefore = DateTime.UtcNow - TimeSpan.FromDays(10)
    /// };
    /// </code>
    /// </example>
    /// <seealso cref="DeleteRangeOptions"/>
    /// <seealso cref="NoSQLClient.DeleteRangeAsync"/>
    public class FieldRange
    {
        /// <summary>
        /// Initializes new instance of if <see cref="FieldRange"/> class.
        /// </summary>
        /// <param name="fieldName">(Optional) The field name.  If not
        /// specified here, it must be set via <see cref="FieldName"/>
        /// property.</param>
        public FieldRange(string fieldName = null)
        {
            FieldName = fieldName;
        }

        /// <summary>
        /// Gets or sets the field name for the field range.
        /// </summary>
        /// <value>
        /// The name of the field.  This value is required.
        /// </value>
        public string FieldName { get; set; }

        /// <summary>
        /// Gets the value for the lower bound of the field range.
        /// </summary>
        /// <value>
        /// Value for the lower bound of the field range or <c>null</c> if no
        /// lower bound is enforced.
        /// </value>
        public FieldValue StartValue { get; private set; }

        /// <summary>
        /// Gets the value for the upper bound of the field range.
        /// </summary>
        /// <value>
        /// Value for the upper bound of the field range or <c>null</c> if no
        /// upper bound is enforced.
        /// </value>
        public FieldValue EndValue { get; private set; }

        /// <summary>
        /// Gets the value indicating whether the lower bound of the field
        /// range is inclusive.
        /// </summary>
        /// <value>
        /// <c>true</c> if the lower bound is inclusive, otherwise
        /// <c>false</c>.
        /// </value>
        public bool IsStartInclusive { get; private set; }

        /// <summary>
        /// Gets the value indicating whether the upper bound of the field
        /// range is inclusive.
        /// </summary>
        /// <value>
        /// <c>true</c> if the upper bound is inclusive, otherwise
        /// <c>false</c>.
        /// </value>
        public bool IsEndInclusive { get; private set; }

        /// <summary>
        /// Gets or sets the inclusive value for the lower bound of the field
        /// range.
        /// </summary>
        /// <remarks>
        /// This property is exclusive with <see cref="StartsAfter"/> in that
        /// only one of these properties may be set.
        /// </remarks>
        /// <value>
        /// Inclusive value for the lower bound.  Returns <c>null</c> if the
        /// lower bound is not set or the lower bound is exclusive.
        /// </value>
        public FieldValue StartsWith
        {
            get => IsStartInclusive ? StartValue : null;

            set
            {
                StartValue = value;
                IsStartInclusive = true;
            }
        }

        /// <summary>
        /// Gets or sets the exclusive value for the lower bound of the field
        /// range.
        /// </summary>
        /// <remarks>
        /// This property is exclusive with <see cref="StartsWith"/> in that
        /// only one of these properties may be set.
        /// </remarks>
        /// <value>
        /// Exclusive value for the lower bound.  Returns <c>null</c> if the
        /// lower bound is not set or the lower bound is inclusive.
        /// </value>
        public FieldValue StartsAfter
        {
            get => IsStartInclusive ? null : StartValue;

            set
            {
                StartValue = value;
                IsStartInclusive = false;
            }
        }

        /// <summary>
        /// Gets or sets the inclusive value for the upper bound of the field
        /// range.
        /// </summary>
        /// <remarks>
        /// This property is exclusive with <see cref="EndsBefore"/> in that
        /// only one of these properties may be set.
        /// </remarks>
        /// <value>
        /// Inclusive value for the upper bound.  Returns <c>null</c> if the
        /// upper bound is not set or the upper bound is exclusive.
        /// </value>
        public FieldValue EndsWith
        {
            get => IsEndInclusive ? EndValue : null;

            set
            {
                EndValue = value;
                IsEndInclusive = true;
            }
        }

        /// <summary>
        /// Gets or sets the exclusive value for the upper bound of the field
        /// range.
        /// </summary>
        /// <remarks>
        /// This property is exclusive with <see cref="EndsWith"/> in that
        /// only one of these properties may be set.
        /// </remarks>
        /// <value>
        /// Exclusive value for the upper bound.  Returns <c>null</c> if the
        /// upper bound is not set or the upper bound is inclusive.
        /// </value>
        public FieldValue EndsBefore
        {
            get => IsEndInclusive ? null : EndValue;

            set
            {
                EndValue = value;
                IsEndInclusive = false;
            }
        }

        internal void Validate()
        {
            CheckStringNotEmpty(FieldName, "field name in field range");

            if (StartValue is null && EndValue is null)
            {
                throw new ArgumentException("Missing bounds in field range");
            }
        }

    }
}
