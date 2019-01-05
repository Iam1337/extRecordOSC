/* Copyright (c) 2019 ExT (V.Sigalkin) */

using UnityEngine;

using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;

#if EXTOSC

using extOSC;
using extOSC.Core;

#endif

namespace extRecordOSC
{
    public class OSCRecorder : MonoBehaviour
    {
        #region Static Public Vars

        public static readonly string HeaderTitle = "extOSC";

        public static readonly ushort HeaderVersion = 0;

        #endregion

        #region Static Private Vars

#if !EXTOSC

        private static readonly string _errorText =
 "Warning! extOSC not found. extRecordOSC require extOSC asset. GitHub: https://github.com/iam1337/extOSC";

#endif

        #endregion

        #region Public Vars

        public bool IsRecording
        {
            get { return _writer != null; }
        }

        public bool IsPlaying
        {
            get { return _reader != null; }
        }

#if EXTOSC
        public OSCReceiver Receiver
        {
            get { return _receiver; }
            set
            {
                if (_receiver == value)
                    return;

                if (_stream != null)
                    throw new Exception("TODO");

                _receiver = value;
            }
        }
#endif

        #endregion

        #region Private Vars

#if EXTOSC

        [SerializeField] private OSCReceiver _receiver;

        private OSCBind _bind;

#endif

        private float _startTime;

        private float _lastTime;

        private float _nextTime;

        private int _headerPosition;

        private int _packetsCount;

        private FileStream _stream;

        private BinaryWriter _writer;

        private BinaryReader _reader;

        private Action<OSCReceiver, OSCPacket> _receiveDelegate;

        #endregion

        #region Unity Methods


        protected void Awake()
        {
#if EXTOSC
            var methodInfo =
                typeof(OSCReceiver).GetMethod("PacketReceived", BindingFlags.Instance | BindingFlags.NonPublic);
            _receiveDelegate =
                (Action<OSCReceiver, OSCPacket>) Delegate.CreateDelegate(typeof(Action<OSCReceiver, OSCPacket>),
                    methodInfo);
#else
            Debug.LogError(_errorText);
#endif
        }

        protected void Update()
        {
            if (_reader != null)
                ProcessPlay();
        }

        protected void OnDestroy()
        {
            if (_writer != null)
                StopRecord();

            if (_reader != null)
                StopPlay();
        }

        #endregion

        #region Public Methods     

        public void StartRecord(string path)
        {
#if !EXTOSC
            Debug.LogError(_errorText);
#else

            if (_writer != null)
                throw new Exception("TODO");

            if (_reader != null)
                throw new Exception("TODO");

            if (_receiver == null)
                throw new Exception("TODO");

            _stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
            _writer = new BinaryWriter(_stream, Encoding.UTF8);

            _startTime = Time.time;
            _lastTime = Time.time;

            _writer.Write(HeaderTitle);
            _writer.Write(HeaderVersion);
            _headerPosition = (int) _writer.BaseStream.Position;
            _writer.Write(0f); // LENGTH
            _writer.Write(0); // PACKETS COUNT;

            _bind = _receiver.Bind("*", ReceivePacket);

            // TODO: Start record info.
#endif
        }

        public void StopRecord()
        {
#if !EXTOSC
            Debug.LogError(_errorText);
#else
            if (_writer == null)
                throw new Exception("TODO");

            if (_reader != null)
                throw new Exception("TODO");

            if (_receiver == null)
                throw new Exception("TODO");

            _receiver.Unbind(_bind);

            _writer.Seek(_headerPosition, SeekOrigin.Begin);
            _writer.Write(_lastTime - _startTime);
            _writer.Write(_packetsCount);

            ((IDisposable) _writer).Dispose();
            _writer = null;

            _stream.Dispose();
            _stream = null;

            // TODO: Stop record info.
#endif
        }

        public void StartPlay(string path)
        {
#if !EXTOSC
            Debug.LogError(_errorText);
#else
            if (_reader != null)
                throw new Exception("TODO");

            if (_writer != null)
                throw new Exception("TODO");

            if (_receiver == null)
                throw new Exception("TODO");

            _stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            _reader = new BinaryReader(_stream, Encoding.UTF8);

            var title = _reader.ReadString();
            if (title != HeaderTitle)
                throw new Exception("TODO");

            var version = _reader.ReadUInt16();
            if (version != HeaderVersion)
                throw new Exception("TODO");

            var length = _reader.ReadSingle();
            var packetCount = _reader.ReadInt32();

            // TODO: Start play info.

            _startTime = Time.time;
            _nextTime = ReadTimestamp();
#endif
        }

        public void StopPlay()
        {
#if !EXTOSC
            Debug.LogError(_errorText);
#else
            if (_reader == null)
                throw new Exception("TODO");

            if (_writer != null)
                throw new Exception("TODO");

            if (_receiver == null)
                throw new Exception("TODO");

            ((IDisposable) _reader).Dispose();
            _reader = null;

            _stream.Dispose();
            _stream = null;

            // TODO: Stop play info.
#endif
        }

        #endregion

        #region Private Methods

#if EXTOSC

        private void ReceivePacket(OSCMessage message)
        {
            WriteTimestamp();
            WritePacket(message);

            _packetsCount++;
        }

        private void WriteTimestamp()
        {
            _lastTime = Time.time - _startTime;
            _writer.Write(_lastTime);
        }

        private void WritePacket(OSCPacket packet)
        {
            if (packet.Ip != null)
            {
                var ipBuffer = packet.Ip.GetAddressBytes();
                var ipSize = ipBuffer.Length;

                _writer.Write(ipSize);
                _writer.Write(ipBuffer);
            }
            else
            {
                _writer.Write(0);
            }

            _writer.Write((ushort) packet.Port);

            var size = 0;
            var buffer = OSCConverter.Pack(packet, out size);

            _writer.Write(size);
            _writer.Write(buffer, 0, size);
        }

        private float ReadTimestamp()
        {
            return _startTime + _reader.ReadSingle();
        }

        private OSCPacket ReadPacket()
        {
            var ip = (IPAddress) null;
            var ipSize = _reader.ReadInt32();
            if (ipSize > 0)
            {
                var ipBytes = _reader.ReadBytes(ipSize);
                ip = new IPAddress(ipBytes);
            }

            var port = (int) _reader.ReadUInt16();
            var size = _reader.ReadInt32();
            var bytes = _reader.ReadBytes(size);

            var message = OSCConverter.Unpack(bytes);
            message.Ip = ip;
            message.Port = port;

            return message;
        }

        private void ProcessPlay()
        {
            while (_nextTime <= Time.time)
            {
                var packet = ReadPacket();
                _receiveDelegate.Invoke(_receiver, packet);

                if (_stream.Position == _stream.Length)
                {
                    StopPlay();
                    return;
                }

                _nextTime = ReadTimestamp();
            }
        }
#endif

        #endregion
    }
}