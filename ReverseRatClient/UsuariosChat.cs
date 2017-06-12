using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace ReverseRatClient
{

    

    class UsuariosChat
    {
        public string NickName { get; set; }
        public string CanalActual { get; set; }
        public string Cliente { get; set; }
        public Socket SocketUsr { get; set; }

        public UsuariosChat(string nick, string canal, string cliente, Socket socketusr)
        {
            NickName = nick;
            CanalActual = canal;
            Cliente = cliente;
            SocketUsr = socketusr;
        }
    }
}
