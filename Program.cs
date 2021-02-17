﻿using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;
using System.Xml.Serialization;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace PosClient
{
    public class Message
    {
        public string From { get; set; }
        public string To { get; set; }
        public string Msg { get; set; }
        public string Stamp { get; set; }

        public override string ToString()
        {
            return $"From: {From}\nTo: {To}\n{Msg}\nStamp: {Stamp}";
        }
    }

    public class Client
    {
        public static string ip = "127.0.0.1";
        public static int port = 14300;
        public static int TAM = 8192;

        // Para los servicios de criptografía
        public static MDCD mdcd = new MDCD();
        // Para guardar la clave pública del servidor
        public static string srvPubKey;
        // Para el propio cliente
        public static RSACryptoServiceProvider rsa = mdcd.rsa;

        // Para verificar mensajes del servidor
        public static bool Verify(Message m)
        {
            try 
            {
                string txt = m.From + m.To + m.Msg;
                string sha = X.ShaHash(txt);
                return X.VerifyData(sha, m.Stamp, srvPubKey);
            }
            catch (Exception)
            {
                return false;
            }
        }

        //Para firmar mensajes
        public static void Sign(ref Message m)
        {
            string txt = m.From + m.To + m.Msg;
            string sha = X.ShaHash(txt);
            m.Stamp = X.SignedData(sha, rsa);
        }

        public static IPAddress GetLocalIpAddress()
        {
            List<IPAddress> ipAddressList = new List<IPAddress>();
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress ipAddress = ipHostInfo.AddressList[0];
            int t = ipHostInfo.AddressList.Length;
            string ip;
            for (int i = 0; i < t; i++)
            {
                ip = ipHostInfo.AddressList[i].ToString();
                if (ip.Contains(".") && !ip.Equals("127.0.0.1")) ipAddressList.Add(ipHostInfo.AddressList[i]);
            }
            if (ipAddressList.Count > 0)
            {
                return ipAddressList[0];//devuelve la primera posible
            }
            return null;
        }

        public static void ReadServerIpPort()
        {
            string s;
            Console.WriteLine("Datos del servidor: ");
            string defIp = GetLocalIpAddress().ToString();
            Console.Write("Dir. IP [{0}]: ", defIp);
            s = Console.ReadLine();
            if ((s.Length > 0) && (s.Replace(".", "").Length == s.Length - 3))
            {
                ip = s;
            }
            else
            {
                ip = defIp;
            }
            Console.Write("PUERTO [{0}]: ", port);
            s = Console.ReadLine();
            if (Int32.TryParse(s, out int i))
            {
                port = i;
            }
        }

        public static void PrintOptionMenu()
        {
            Console.WriteLine("====================");
            Console.WriteLine("        MENU        ");
            Console.WriteLine("====================");
            Console.WriteLine("0: Salir");
            Console.WriteLine("1: Chequear correo");
            Console.WriteLine("2: Obtener mensaje");
            Console.WriteLine("3: Escribir mensaje");
            Console.WriteLine("4: MyDeCoDer ");
            Console.WriteLine("5: Enviar clave pub.");
        }

        public static int ReadOption()
        {
            string s = null;
            while (true)
            {
                Console.Write("Opción [0-5]: ");
                s = Console.ReadLine();
                if (Int32.TryParse(s, out int i))
                {
                    if ((i >= 0) && (i <= 5))
                    {
                        return i;
                    }
                }
            }
        }

        public static Socket Connect()
        {
            IPAddress ipAddress = IPAddress.Parse(ip);
            IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);

            Socket socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            socket.Connect(remoteEP);

            return socket;
        }

        public static void Disconnect(Socket socket)
        {
            socket.Shutdown(SocketShutdown.Both);
            socket.Close();
        }

        public static void Send(Socket socket, Message message)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(Message));
            Stream stream = new MemoryStream();
            serializer.Serialize(stream, message);
            byte[] byteData = ((MemoryStream)stream).ToArray();
            // string xml = Encoding.ASCII.GetString(byteData, 0, byteData.Length);
            // Console.WriteLine(xml);//Imprime el texto enviado
            int bytesSent = socket.Send(byteData);
        }

        public static Message Receive(Socket socket)
        {
            byte[] bytes = new byte[TAM];
            int bytesRec = socket.Receive(bytes);
            string xml = Encoding.ASCII.GetString(bytes, 0, bytesRec);
            // Console.WriteLine(xml);//Imprime el texto recibido
            byte[] byteArray = Encoding.ASCII.GetBytes(xml);
            MemoryStream stream = new MemoryStream(byteArray);
            Message response = (Message)new XmlSerializer(typeof(Message)).Deserialize(stream);
            return response;
        }

        public static void Process(int option)
        {
            switch (option)
            {
                case 1:
                    ChequearCorreo();
                    break;
                case 2:
                    ObtenerMensaje();
                    break;
                case 3:
                    EscribirMensaje();
                    break;
                case 4:
                    mdcd.Run();
                    break;
                case 5:
                    EnviarClavePub();
                    break;
            }
        }

        public static void EnviarClavePub()
        {
            Console.WriteLine("--------------------");
            Console.WriteLine("5: Enviar clave pub.");
            Console.WriteLine("--------------------");
            Console.Write("From: ");
            string f = Console.ReadLine();

            Socket socket = Connect();
            Message request = new Message { From = f, To = "0", Msg = "PUBKEY " + X.RsaGetPubParsXml(rsa)};
            Sign(ref request);
            Send(socket, request);
            Console.WriteLine("....................");
            Message response = Receive(socket);
            if (!response.Msg.StartsWith("ERROR"))
            {
                //si no se puede verificar la respuesta mostrar en consola "ERROR server VALIDATION"
                //y no asignar a srvPubKey la clave pública del servidor recibida
                srvPubKey = response.Msg;
                Verifyif(response);
            }
            Console.WriteLine(response);
            Disconnect(socket);
        }

        public static void ChequearCorreo()
        {
            Console.WriteLine("--------------------");
            Console.WriteLine("1: Chequear correo  ");
            Console.WriteLine("--------------------");
            Console.Write("From: ");
            string f = Console.ReadLine();

            Socket socket = Connect();
            Message request = new Message { From = f, To = "0", Msg = "LIST", Stamp = "Client" };
            Sign(ref request);
            Send(socket, request);
            Console.WriteLine("....................");
            Message response = Receive(socket);
            //si no se puede verificar la respuesta mostrar en consola "ERROR server VALIDATION"
            Verifyif(response);
            Console.WriteLine(response);
            Disconnect(socket);
        }

        public static void ObtenerMensaje()
        {
            Console.WriteLine("--------------------");
            Console.WriteLine("2: Obtener mensaje  ");
            Console.WriteLine("--------------------");
            Console.Write("From: ");
            string f = Console.ReadLine();
            Console.Write("Num.: ");
            string n = Console.ReadLine();

            Socket socket = Connect();
            Message request = new Message { From = f, To = "0", Msg = "RETR " + n, Stamp = "Client" };
            Sign(ref request);
            Send(socket, request);
            Console.WriteLine("....................");
            Message response = Receive(socket);
            Console.WriteLine(response);
            Disconnect(socket);
        }

        public static void EscribirMensaje()
        {
            Console.WriteLine("--------------------");
            Console.WriteLine("3: Escribir mensaje ");
            Console.WriteLine("--------------------");
            Console.Write("From: ");
            string f = Console.ReadLine();
            Console.Write("To: ");
            string t = Console.ReadLine();
            Console.Write("Msg: ");
            string m = Console.ReadLine();

            Socket socket = Connect();
            Message request = new Message { From = f, To = t, Msg = m, Stamp = "Client" };
            Sign(ref request);
            Send(socket, request);
            Console.WriteLine("....................");
            Message response = Receive(socket);
            //si no se puede verificar la respuesta mostrar en consola "ERROR server VALIDATION"
            Verifyif(response);
            Console.WriteLine(response);
            Disconnect(socket);
        }

        private static void Verifyif(Message response)
        {
            if (!Verify(response))
            {
                Console.WriteLine("ERROR server VALIDATION");
            }
            else { }
        }

        public static int Main(String[] args)
        {
            ReadServerIpPort();
            while (true)
            {
                PrintOptionMenu();
                int opt = ReadOption();
                if (opt == 0) break;
                Process(opt);
            }
            Console.WriteLine("FIN.");
            return 0;
        }
    }
}