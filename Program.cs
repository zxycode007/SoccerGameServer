using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AsycServer
{
    class Program
    {
        static void Main(string[] args)
        {
            Program p = new Program();
            while (true)
            {
                p.Logic();
                Thread.Sleep(10);
            }
        }
        ServerLogger logger;
        AsyUdpServer server;
        /// <summary>
        /// 客户端列表
        /// </summary>
        List<Client> clientList = new List<Client>();
        /// <summary>
        /// 客户端 ---- 用户ID  列表
        /// </summary>
        Dictionary<Client, int> userList = new Dictionary<Client, int>();
        /// <summary>
        /// 关键帧数据列表
        /// 保存服务器每一个关键帧的各个用户的KeyData列表
        /// </summary>
        Dictionary<int, Dictionary<int, List<string>>> keyDic = new Dictionary<int,Dictionary<int,List<string>>>();//
        /// <summary>
        /// 用户ID --- 用户信息的字典
        /// </summary>
        Dictionary<int, Game.GamePlayer> playerInfoDic = new Dictionary<int, Game.GamePlayer>();
        private int roleId = 1000; //客户端的人物id
        private int frameCount = 1; //当前帧数

        public Program()
        {
            server = new AsyUdpServer(1255, 1337);
            logger = new ServerLogger();
            logger.Log("server startup!");
             
            AsyUdpServer.DebugInfo.upData = true;

            server.OnStart += OnStart;
            server.OnConnect += OnConnect;
            server.OnMessage += OnMessage;
            server.OnDisconnect += OnDisconnect;
            server.OnDebug += OnDebug;

            Thread t = new Thread(InputThread);
            t.Start();
        }

        public void Logic()
        {
            server.Update();
        }

        public void OnStart()
        {
            Console.WriteLine("Server started!");
        }

        public void OnConnect(Client c)
        {
            
            Console.WriteLine("{0}[{1}, {2}] connected!", c.ID, c.tcpAdress, c.udpAdress);
            clientList.Add(c);
            logger.Log(string.Format("{0}[{1}, {2}] connected!", c.ID, c.tcpAdress, c.udpAdress));
            MessageBuffer msg = new MessageBuffer();
            msg.WriteInt(cProto.CONNECT);
            msg.WriteInt(roleId);
            c.Send(msg);
            roleId++;
        }

        public void OnMessage(Client c, MessageBuffer msg)
        {
            int cproto = msg.ReadInt();
            switch(cproto)
            {
                case cProto.CONNECT:
                    break;
                case cProto.LOGIN:
                    {
                        int playerID = msg.ReadInt(); 
                        if(!userList.ContainsKey(c))
                        {
                            userList.Add(c, playerID);
                        }
                        if (userList[c] != playerID)
                        {
                            Console.WriteLine(string.Format("连接客户端 用户ID 不匹配 {0}", playerID));
                            break;
                        }
                        string playerName = msg.ReadString();
                        string playerIconPath = msg.ReadString();
                        Game.GamePlayer playerInfo = new Game.GamePlayer();
                        playerInfo.PlayerID = playerID;
                        playerInfo.PlayerName = playerName;
                        playerInfo.PlayerIcon = playerIconPath;

                        playerInfoDic[playerID] = playerInfo;
                        MessageBuffer buff = new MessageBuffer();
                        buff.WriteInt(cProto.TO_TEAM_SELECT);
                        buff.WriteInt(playerID);
                        buff.WriteString(playerName);
                        buff.WriteString(playerIconPath);
                        Console.WriteLine(string.Format("玩家{0} {1} {2} To Select Team!", playerID, playerName, playerIconPath));
                        c.Send(buff);
                        break;
                    }
                case cProto.READY:
                    //Console.WriteLine("Ready消息");
                   
                    if (!userList.ContainsKey(c))
                    {
                        int id = msg.ReadInt();
                        userList.Add(c, id);
                        Console.WriteLine(string.Format("玩家{0},Ready", id));
                        logger.Log(string.Format("玩家{0}Ready消息", c.ID));
                    }
                    //所有的玩家都准备好了，可以开始同步
                    if(userList.Count >= clientList.Count)
                    {
                        frameCount = 1;
                        keyDic = new Dictionary<int, Dictionary<int, List<string>>>();
                        string playStr = "";
                        List<string> playList = new List<string>();
                        //遍历连接的玩家列表
                        foreach(var play in userList)
                        {
                            //创建角色信息
                            for(int i=0;i<3;i++)
                            {
                                //用户ID, 单位名字=用户ID+i
                                CharData charData = new CharData(play.Value, play.Value  + i.ToString());
                                playList.Add(charData.ToString());
                            }
                              
                        }
                        playStr = string.Join(";", playList.ToArray());
                        MessageBuffer buff = new MessageBuffer();
                        buff.WriteInt(cProto.START);
                        buff.WriteString(playStr);

                        for (int i = 0; i < clientList.Count; ++i)
                        {
                            Console.WriteLine(string.Format("玩家{0},Start", clientList[i].ID));
                            logger.Log(string.Format("玩家{0},Start", clientList[i].ID));
                            clientList[i].Send(buff);
                        }
                        frameCount = 1;
                        Console.WriteLine("重载服务器关键帧数=1");
                    }
                    break;
                ///位置属于状态同步，直接通知客户端更新就可以了
                case cProto.SYNC_POS:
                    for (int i = 0; i < clientList.Count; ++i)
                    {
                        if(c == clientList[i])
                        {
                            continue;
                        }
                        clientList[i].Send(msg);
                    }
                        break;
                    ///收到客户端发的关键帧信息
                case cProto.SYNC_KEY:
                        Console.WriteLine("同步关键帧{0}", frameCount);
                        //取出客户端当前帧数
                        int clientCurFrameCount = msg.ReadInt();
                        //客户端关键帧数据
                        string keyStr = msg.ReadString();
                        //服务器的关键帧中是否包含此关键帧
                        if (keyDic.ContainsKey(clientCurFrameCount))
                        {
                            //是否包含用户ID
                            if (keyDic[clientCurFrameCount].ContainsKey(userList[c]))
                            {
                                //为其添加KeyData
                                keyDic[clientCurFrameCount][userList[c]].Add(keyStr);
                            }
                            else
                            {
                                //不包含用户ID，新用户创建一个
                                keyDic[clientCurFrameCount][userList[c]] = new List<string>();
                                keyDic[clientCurFrameCount][userList[c]].Add(keyStr);
                            }
                        }
                        else
                        {
                            //创建新的关键帧数据
                            keyDic[clientCurFrameCount] = new Dictionary<int, List<string>>();
                            keyDic[clientCurFrameCount][userList[c]] = new List<string>();
                            keyDic[clientCurFrameCount][userList[c]].Add(keyStr);
                        }
                        //客户端当前关键帧==服务器帧数
                        if (clientCurFrameCount == frameCount-1)
                        {
                            Console.WriteLine(string.Format("同步关键帧{0}", frameCount));
                            logger.Log(string.Format("同步关键帧{0}", frameCount));
                            //收到所有客户端发来的关键帧数据，服务器帧数推进
                            if (keyDic[clientCurFrameCount].Count == clientList.Count)
                            {
                                List<string> keyDataList = new List<string>();
                                //取出该当前帧所有用户的KeyDataList添加到一条KeyDataList
                                foreach (var dataList in keyDic[clientCurFrameCount].Values)
                                {
                                    keyDataList.AddRange(dataList);
                                }
                                //将这条KeyDataList发给每一个客户端
                                string keyData = string.Join(";", keyDataList.ToArray());
                                MessageBuffer buff = new MessageBuffer();
                                buff.WriteInt(cProto.SYNC_KEY);
                                buff.WriteInt(frameCount);
                                buff.WriteString(keyData);

                                for (int i = 0; i < clientList.Count; ++i)
                                {
                                    Console.WriteLine(string.Format("Send KeyData to Player{0}", clientList[i].ID));
                                    logger.Log(string.Format("Send KeyData to Player{0}", clientList[i].ID));
                                    clientList[i].Send(buff);
                                }
                                //服务器帧数更新
                                frameCount += 1;
                            }
                        }
                        break;
                case cProto.START:
                    break;
            }
        }

        public void OnDisconnect(Client c)
        {
            Console.WriteLine("{0}[{1}, {2}] disconnected!", c.ID, c.tcpAdress, c.udpAdress);
            clientList.Remove(c);
            if(userList.ContainsKey(c))
            {
                userList.Remove(c);
            }
        }

        public void OnDebug(string s)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(s);
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        public void InputThread()
        {
            while (true)
            {
                string input = Console.ReadLine();

                if (server.Active)
                {
                    string[] inputArgs = input.Split(' ');
                    if (inputArgs[0] == "quit") server.Close();
                    if (inputArgs[0] == "kick") server.GetClient(int.Parse(inputArgs[1])).Disconnect();
                }
                else
                {
                    if (input == "start") server.StartUp("127.0.0.1");
                }
            }
        }
    }
}
