﻿using System;
using System.Diagnostics;

namespace RPChecker.Util
{
    public interface IProcess
    {
        bool Abort { get; set; }

        int ExitCode { get; set; }

        bool ProcessNotFind { get; set; }

        void GenerateLog(object args);

        void OutputHandler(object sendingProcess, DataReceivedEventArgs outLine);

        void ErrorOutputHandler(object sendingProcess, DataReceivedEventArgs outLine);

        void ExitedHandler(object sender, EventArgs e);

        event Action<string> ProgressUpdated;

        event Action<string> ValueUpdated;
    }
}