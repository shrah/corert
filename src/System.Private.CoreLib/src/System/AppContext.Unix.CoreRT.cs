// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Runtime.CompilerHelpers;

namespace System
{
    public static partial class AppContext
    {
        public static string BaseDirectory
        {
            get
            {
                string path = StartupCodeHelpers.BasePath;
                if (path == null)
                {
                    //TODO: throw appropriate exception;
                    throw new TypeLoadException("Could not read basepath");
                }
                return path.Substring(0, path.LastIndexOf('/'));
            }
        }
    }
}
