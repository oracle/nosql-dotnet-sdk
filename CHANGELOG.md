# Change Log

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/).

## Unpublished

**Added**
* Implemented version 4 of the serialization protocol.
* Added new features enabled in V4 protocol such as cloud tagging, table ETag,
table usage request paging and new fields in TableResult.

## [5.1.5]

**Added**

* New regions for cloud service: ord, tiw, bgy, mxp, ork, snn,
  dus, dtm, sgu, ifp, gcn
* Support for WriteMany with multiple tables that share the same shard key.
Added new methods to WriteOperationCollection that take table name parameter.
In addition, added new WriteManyAsync method that does not take table name
parameter and expects table names be included in WriteOperationCollection
instance.

**Fixed**

* Fixed CS0419 warnings in doc comments.

## [5.1.4]

**Added**

* SetVariable methods in PreparedStatement to set bind variables by name and
by position in the query string.
* Support for rate limiting.
* Support for proxy V3 protocol including features such as on-demand tables,
durability and new result fields for row modification time.
* Added new regions for cloud service: mad, lin, cdg, qro, mtz, vcp, brs, ukb.

**Changed**

* Changed timeout handling for "with completion" table DDL and admin
operations to ensure that errors that are not related to completion of the
operation on the server side do not result in long waits.  Also, ensure that
elapsed time is reflected in subsequent HTTP request timeouts when the
operation is retried by the retry handler.
* Changed hash functions for DoubleValue and NumberValue to make sure that if
the values of different types are equal (as in Equals), they hash to the same
value.
* Allow values of type Number to be read as double if they are too large to
fit into into decimal.
* Changed SortIterator to use stable sort to be consistent across platform
SDKs.

**Fixed**

* Fixed an issue when using PrivateKeyPEM property for IAM authentication to
allow both Unix and Windows line endings in the PEM string.
* Fixed potential invalid timeout errors and make sure table state polling or
admin operation status polling do not run over timeout.
* Fixed erroneous min-max comparison in group iterator.
* Fixed erroneous returned result in group iterator when no aggregates are
present.
* Fixed unhandled exception in hash function for NumberValue.

## [5.1.3]

**Added**

* Added new regions for cloud service: jnb, wga, sin, mrs, arn, mct, auh.

**Fixed**

* Fixed a bug in IAMAuthorizationProvider where local time was used instead of
universal time to determine the validity of the signature causing the
connection to fail in some time zones.

## [5.1.2]

This is the initial release of Oracle NoSQL SDK for .NET.
