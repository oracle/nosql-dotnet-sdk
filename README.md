# Documentation for the Oracle NoSQL Database .NET SDK

This is a README for the nosql-dotnet-doc branch of the
[Oracle NoSQL Database .NET SDK repository](https://github.com/oracle/nosql-dotnet-sdk). This branch uses the GitHub pages mechanism to publish documentation on GitHub.

## Building and Publishing Documentation

Generated documentation is published on
[GitHub](https://oracle.github.io/nosql-dotnet-sdk/) using the GitHub Pages
facility. Publication is automatic based on changes pushed to this (gh-pages)
branch of the
[Oracle NoSQL Database .NET SDK](https://github.com/oracle/nosql-dotnet-sdk)
repository.

To publish:

In these instructions <nosql-dotnet-sdk> is the path to a current clone from
which to publish the documentation and <nosql-dotnet-doc> is the path to
a fresh clone of the nosql-dotnet-doc branch (see instructions below).

1. clone the nosql-dotnet-doc branch of the SDK repository into <nosql-dotnet-doc>:

        git clone --single-branch --branch nosql-dotnet-doc https://github.com/oracle/nosql-dotnet-sdk.git nosql-dotnet-doc

2. generate documentation in the master (or other designated) branch of the
repository:

        $ cd <nosql-dotnet-sdk>
        $ rm -rf doc/api
        $ npm run docs

3. copy generated doc to the nosql-dotnet-doc repo

        $ cp -r <nosql-dotnet-sdk>/Documentation/_site/* <nosql-dotnet-doc>

4. commit and push after double-checking the diff in the nosql-dotnet-doc
repository

        $ cd <nosql-dotnet-doc>
        $ git commit
        $ git push

The new documentation will automatically be published.
