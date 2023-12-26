﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Schema;

namespace FTP
{
    internal class Listener
    {
        public int port { get;private set; }
        public Socket sck { get; private set; }
        public bool isListening { get; private set; }
        public Dictionary<string, UserInfo> userInfo { get; private set; }
        public Dictionary<string, bool> logs { get; private set; }
        private string rootPath = "E:\\Server\\Root";

        public Listener(int _port, Dictionary<string, UserInfo> info)
        {
            this.port = _port;
            sck=new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            this.userInfo=info;
            logs=new Dictionary<string, bool>();
        }

        public void Start()
        {
            if(isListening)
                return;

            isListening = true;
            sck.Bind(new IPEndPoint(0, port));
            sck.Listen(0);

            sck.BeginAccept(Callback, null);

        }
        private void Callback(IAsyncResult ar)
        {
            Socket e=this.sck.EndAccept(ar);
            new Thread(() =>
            {
                ReadData(e);
            }).Start();
            if (OnSocketAccepeted!=null)
                OnSocketAccepeted(e);

            this.sck.BeginAccept(Callback, null);
            
        }
        public delegate void SocketAccepetedHandler(Socket e);
        public event SocketAccepetedHandler OnSocketAccepeted;

        private void ReadData(Socket acp)
        {
            while (true)
            {
                byte[] buffer = new byte[255];
                int bufferSize = acp.Receive(buffer,0,buffer.Length, SocketFlags.None);
                if (bufferSize<=0)
                    Thread.CurrentThread.Abort();

                Array.Resize(ref buffer, bufferSize);

                string clientRequest=Encoding.UTF8.GetString(buffer);
                ClientRequest request=JsonSerializer.Deserialize<ClientRequest>(clientRequest);
                
                if (request.requestType=="POST")
                {
                    ServerResponse response = new ServerResponse();
                    switch (request.command)
                    {
                        case "Login":
                            response = Login(request);
                            break;
                        case "LIST":
                            string path=Path.Combine(rootPath, request.serverDirectory);
                            DirectoryInfo directory=new DirectoryInfo(path);
                            FileInfo[] files=directory.GetFiles();
                            response.response="";
                            for(int i = 0; i<files.Length; i++)
                            {
                                response.response+= files[i].CreationTime+" "+files[i].Name+"\n";
                            }
                            response.HTTP_Code=200;
                            break;
                    }
                    string resJson = JsonSerializer.Serialize(response);
                    byte[]sendBuffer= Encoding.UTF8.GetBytes(resJson);
                    acp.Send(sendBuffer, 0, sendBuffer.Length, SocketFlags.None);
                    if (response.HTTP_Code!=200)
                    {
                        acp.Disconnect(true);
                    }

                }
                

            }
        }

        private ServerResponse Login(ClientRequest request)
        {
            ServerResponse response = new ServerResponse();
            bool loginResult = CheckLogin(request.userInfo);
            logs.TryGetValue(request.userInfo.username, out bool log);

            if (loginResult && !log)
            {
                response.HTTP_Code=200;
                response.response="User was Logged in successfully";
                logs.Add(request.userInfo.username, true);
            }
            else if (log==true)
            {
                response.HTTP_Code=403;
                response.response="User is already logged in";
            }
            else
            {
                response.HTTP_Code=404;
                response.response="Invalid Username or Password";
            }

            return response;
        }

        private bool CheckLogin(UserInfo _info)
        {
            userInfo.TryGetValue(_info.username, out UserInfo value);
            
            if(value==null)
                return false;

            if (value.password==_info.password)
                return true;
            

            return false;
        }

        
        public void Stop()
        {
            if(!isListening)
                return;

            isListening = false;
            sck.Close();
            sck.Dispose();
            sck=new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }
    }
}
