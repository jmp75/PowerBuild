﻿namespace PowerBuild.Logging
{
    using Microsoft.Build.Framework;

    public interface IPowerShellLogger : ILogger
    {
        void WriteEvents();
    }
}