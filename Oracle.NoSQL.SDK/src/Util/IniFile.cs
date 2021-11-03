/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    internal static class IniFile
    {
        private ref struct ProfileReader
        {
            private readonly string configFile;
            private readonly string profileName;
            private Dictionary<string, string> profile;

            internal ProfileReader(string configFile, string profileName)
            {
                this.configFile = configFile;
                this.profileName = profileName;
                profile = null;
            }

            private bool ProcessLine(string line)
            {
                line = line.Trim();
                if (line.Length == 0 || line[0] == '#')
                {
                    return false;
                }

                if (line[0] == '[' && line[line.Length - 1] == ']')
                {
                    line = line.Substring(1, line.Length - 2);
                    if (profile == null && line == profileName)
                    {
                        profile = new Dictionary<string, string>();
                        return false;
                    }

                    // Finished reading target profile because next profile
                    // is starting, no need to read the rest.
                    if (profile != null)
                    {
                        return true;
                    }
                }

                if (profile == null)
                {
                    return false;
                }

                var i = line.IndexOf('=');
                if (i == -1)
                {
                    throw new ArgumentException(
                        $"Invalid line in config file {configFile} " +
                        $"(not a key=value): {line}");
                }

                var key = line.Substring(0, i).TrimEnd();
                if (key.Length == 0)
                {
                    throw new ArgumentException(
                        $"Invalid line in config file {configFile} " +
                        $"(empty key): {line}");
                }

                profile[key] = line.Substring(i + 1).TrimStart();
                return false;
            }

            internal Dictionary<string, string> ReadProfile()
            {
                foreach (var line in File.ReadLines(configFile))
                {
                    if (ProcessLine(line))
                    {
                        break;
                    }
                }

                return profile;
            }

        }

        internal static Dictionary<string, string> GetProfile(
            string configFile, string profileName)
        {
            var profileReader = new ProfileReader(configFile, profileName);
            return profileReader.ReadProfile();
        }

    }

}
