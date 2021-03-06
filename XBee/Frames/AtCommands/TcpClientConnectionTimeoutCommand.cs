﻿using System;

namespace XBee.Frames.AtCommands
{
    public class TcpClientConnectionTimeoutCommand : AtCommand
    {
        public const int ValueMsScale = 100;

        public TcpClientConnectionTimeoutCommand() : base("TM")
        {
        }

        public TcpClientConnectionTimeoutCommand(TimeSpan timeout)
        {
            if(timeout < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "must be greater than zero.");
            var ms = timeout.TotalMilliseconds;
            var value = ms/ValueMsScale;

            if(value > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "must be less than 109 minutes.");

            Parameter = (ushort)value;
        }
    }
}
