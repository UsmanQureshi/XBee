﻿using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using BinarySerialization;
using XBee.Frames.AtCommands;

namespace XBee
{
    internal class SerialConnection : IDisposable
    {
        private readonly FrameSerializer _frameSerializer = new FrameSerializer();

        private readonly SerialPort _serialPort;
        private CancellationTokenSource _readCancellationTokenSource;

        private readonly object _openCloseLock = new object();

        public SerialConnection(string port, int baudRate)
        {
            _serialPort = new SerialPort(port, baudRate);

            _frameSerializer.MemberSerializing += OnMemberSerializing;
            _frameSerializer.MemberSerialized += OnMemberSerialized;
            _frameSerializer.MemberDeserializing += OnMemberDeserializing;
            _frameSerializer.MemberDeserialized += OnMemberDeserialized;
        }

        public HardwareVersion? CoordinatorHardwareVersion
        {
            get { return _frameSerializer.ControllerHardwareVersion; }
            set { _frameSerializer.ControllerHardwareVersion = value; }
        }

        public void Dispose()
        {
            Close();
        }

        /// <summary>
        ///     Occurrs after a member has been serialized.
        /// </summary>
        public event EventHandler<MemberSerializedEventArgs> MemberSerialized;

        /// <summary>
        ///     Occurrs after a member has been deserialized.
        /// </summary>
        public event EventHandler<MemberSerializedEventArgs> MemberDeserialized;

        /// <summary>
        ///     Occurrs before a member has been serialized.
        /// </summary>
        public event EventHandler<MemberSerializingEventArgs> MemberSerializing;

        /// <summary>
        ///     Occurrs before a member has been deserialized.
        /// </summary>
        public event EventHandler<MemberSerializingEventArgs> MemberDeserializing;


        public async Task Send(FrameContent frameContent)
        {
            await Send(frameContent, CancellationToken.None);
        }

        public async Task Send(FrameContent frameContent, CancellationToken cancellationToken)
        {
            byte[] data = _frameSerializer.Serialize(new Frame(frameContent));
            await _serialPort.BaseStream.WriteAsync(data, 0, data.Length, cancellationToken);
        }

        public event EventHandler<FrameReceivedEventArgs> FrameReceived;

        private Task _receiveTask;

        public void Open()
        {
            lock (_openCloseLock)
            {
                _serialPort.Open();

                _readCancellationTokenSource = new CancellationTokenSource();
                CancellationToken cancellationToken = _readCancellationTokenSource.Token;

                _receiveTask = Task.Run(() =>
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            Frame frame = _frameSerializer.Deserialize(_serialPort.BaseStream);

                            var handler = FrameReceived;
                            if (handler != null)
                                Task.Run(() => handler(this, new FrameReceivedEventArgs(frame.Payload.Content)),
                                    cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception)
                        {
                            if (!cancellationToken.IsCancellationRequested)
                                throw;
                        }
                    }
// ReSharper disable MethodSupportsCancellation
                }, cancellationToken);
// ReSharper restore MethodSupportsCancellation

                _receiveTask.ConfigureAwait(false);
            }
        }

        public void Close()
        {
            lock (_openCloseLock)
            {
                if (_receiveTask == null)
                    return;

                _readCancellationTokenSource.Cancel();
                _serialPort.Close();
                _receiveTask.Wait();
                _readCancellationTokenSource.Dispose();

                _receiveTask = null;
            }
        }

        private void OnMemberSerialized(object sender, MemberSerializedEventArgs e)
        {
            EventHandler<MemberSerializedEventArgs> handler = MemberSerialized;
            handler?.Invoke(sender, e);
        }

        private void OnMemberDeserialized(object sender, MemberSerializedEventArgs e)
        {
            EventHandler<MemberSerializedEventArgs> handler = MemberDeserialized;
            handler?.Invoke(sender, e);
        }

        private void OnMemberSerializing(object sender, MemberSerializingEventArgs e)
        {
            EventHandler<MemberSerializingEventArgs> handler = MemberSerializing;
            handler?.Invoke(sender, e);
        }

        private void OnMemberDeserializing(object sender, MemberSerializingEventArgs e)
        {
            EventHandler<MemberSerializingEventArgs> handler = MemberDeserializing;
            handler?.Invoke(sender, e);
        }
    }
}