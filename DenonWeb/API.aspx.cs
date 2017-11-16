using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace DenonWeb
{
    public partial class API : System.Web.UI.Page
    {
        private TcpClient _tcpSocket;
        private string _host = "10.1.2.66";
        private int _port = 23;
        private int _readDelay = 500;

        public enum Command
        {
            VolDown,
            VolUp,
            VolMuteOn,
            VolMuteOff,
            VolMuteToggle
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            var responseMessage = "No command received";

            try
            {
                if (Request.QueryString["command"] == null)
                {
                    responseMessage = "Request didn't have a command query";

                }
                else
                {
                    CheckConnection();

                    if (!IsConnected())
                    {
                        responseMessage = "No connection could be made to Denon AVR!";
                    }
                    else
                    {
                        Command command = (Command)Enum.Parse(typeof(Command), Request.QueryString["command"]);

                        switch (command)
                        {
                            case Command.VolDown:
                                Write("MVDOWN");
                                responseMessage = "Turned volume down";
                                break;
                            case Command.VolUp:
                                Write("MVUP");
                                responseMessage = "Turned volume up";
                                break;
                            case Command.VolMuteOn:
                                Write("MUON");
                                responseMessage = "Mute turned on";
                                break;
                            case Command.VolMuteOff:
                                Write("MUOFF");
                                responseMessage = "Mute turned off";
                                break;
                            case Command.VolMuteToggle:
                                Write("MU?");

                                string muteState = Read().Trim();

                                if (muteState == "MUOFF")
                                {
                                    Write("MUON");
                                    responseMessage = "Mute turned on";
                                }
                                else if (muteState == "MUON")
                                {
                                    Write("MUOFF");
                                    responseMessage = "Mute turned off";
                                }

                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                responseMessage = $"Error occured: {ex}";
            }

            Response.Write(responseMessage);
        }

        private void CheckConnection()
        {
            if (_tcpSocket == null)
                Connect();
            else if (!_tcpSocket.Connected)
                Connect();
        }

        private bool IsConnected()
        {
            if (_tcpSocket == null)
                return false;

            return _tcpSocket.Connected;
        }

        public void Connect()
        {
            try
            {
                _tcpSocket = new TcpClient(_host, _port);
            }
            catch (Exception){
            }
        }


        public string Read()
        {
            if (!IsConnected()) return null;
            var sb = new StringBuilder();
            do
            {
                ParseCommmand(sb);
                
                // Sleep required as AVR tends to have variable delay
                Thread.Sleep(_readDelay);
            } while (_tcpSocket.Available > 0);
            return sb.ToString();
        }
        internal enum Verbs
        {
            WILL = 251,
            WONT = 252,
            DO = 253,
            DONT = 254,
            IAC = 255
        }
        internal enum Options
        {
            SGA = 3
        }

        private void ParseCommmand(StringBuilder sb)
        {
            while (_tcpSocket.Available > 0)
            {
                var input = _tcpSocket.GetStream().ReadByte();
                switch (input)
                {
                    case -1:
                        break;
                    case (int) Verbs.IAC:
                        // interpret as command
                        var inputverb = _tcpSocket.GetStream().ReadByte();
                        if (inputverb == -1) break;
                        switch (inputverb)
                        {
                            case (int) Verbs.IAC:
                                //literal IAC = 255 escaped, so append char 255 to string
                                sb.Append(inputverb);
                                break;
                            case (int) Verbs.DO:
                            case (int) Verbs.DONT:
                            case (int) Verbs.WILL:
                            case (int) Verbs.WONT:
                                // reply to all commands with "WONT", unless it is SGA (suppres go ahead)
                                var inputoption = _tcpSocket.GetStream().ReadByte();
                                if (inputoption == -1) break;
                                _tcpSocket.GetStream().WriteByte((byte) Verbs.IAC);
                                if (inputoption == (int) Options.SGA)
                                    _tcpSocket.GetStream().WriteByte(inputverb == (int) Verbs.DO
                                        ? (byte) Verbs.WILL
                                        : (byte) Verbs.DO);
                                else
                                    _tcpSocket.GetStream().WriteByte(inputverb == (int) Verbs.DO
                                        ? (byte) Verbs.WONT
                                        : (byte) Verbs.DONT);
                                _tcpSocket.GetStream().WriteByte((byte) inputoption);
                                break;
                            default:
                                break;
                        }
                        break;
                    default:
                        sb.Append((char) input);
                        break;
                }
            }
        }

        public void Write(string cmd)
        {
            // Append <CR>
            cmd = cmd + Environment.NewLine;

            if (!_tcpSocket.Connected) return;
            var buf = Encoding.ASCII.GetBytes(cmd.Replace("\0xFF", "\0xFF\0xFF"));
            _tcpSocket.GetStream().Write(buf, 0, buf.Length);
        }

    }
}