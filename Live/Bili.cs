﻿
using System.Net.WebSockets;
using System.Threading;
using System;
using System.Diagnostics;
using System.Text;
using System.Net.NetworkInformation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Buffers.Text;

namespace Live
{
    public class Bili : Live, ILive
    {

        Connect connect = new Connect();
        ClientWebSocket _ws => connect.WS;


        public Bili(string roomID)
        {
            #region 获取房间id和token
            var response = HttpHelper.HttpGet("https://api.live.bilibili.com/room/v1/Room/get_info", new Dictionary<string, string>() { { "room_id", roomID } });
            connect.Roomid = int.Parse(response["data"]!["room_id"]!.ToString());
            Log.WriteLine("bilibili 真实房间号", connect.Roomid.ToString());

            response = HttpHelper.HttpGet($"https://api.live.bilibili.com/room/v1/Danmu/getConf?room_id={connect.Roomid}&platform=pc&player=web");
            var wsDomain = response["data"]!["host"]!.ToString();
            //var ipAddress = Dns.GetHostAddresses(wsDomain);
            //var ip = ipAddress[new Random().Next(0, ipAddress.Length)];
            connect.WsUrl = new Uri($"wss://{wsDomain}/sub");
            Log.WriteLine("wobsocket 地址", connect.WsUrl.Host.ToString());
            connect.Token = response["data"]!["token"]!.ToString();
            #endregion

            InitWS();
        }

        /// <summary>
        /// websocket 认证 & 心跳 & 接收
        /// </summary>
        private void InitWS()
        {
            _ws.ConnectAsync(connect.WsUrl, connect.Cancellation).Wait();
            var packetModel = new { uid = 0, roomid = connect.Roomid, protover = 3, platform = "vtbai", type = 2, key = connect.Token, };
            var playload = JsonConvert.SerializeObject(packetModel);
            SendSocketDataAsync(7, playload, "发送认证包").Wait();
            var recAuth = new byte[1024];
            _ws.ReceiveAsync(recAuth, connect.Cancellation).Wait();
            recAuth = recAuth.Skip(connect.headLength).ToArray();
            Log.WriteLine("接收认证包", Encoding.UTF8.GetString(recAuth));

            _ = HeartbeatLoop();
            Task.Run(() =>
            {
                try
                {
                    ReceiveMessageLoop();
                }
                catch (Exception ex)
                {
                    Log.Error("bili接收消息错误", ex.Message);
                    throw;
                }
            });
        }

        private void JoinQueue(JObject obj)
        {
            string cmd = obj["cmd"]!.ToString();
            switch (cmd)
            {

                case "NOTICE_MSG":
                    break;
                case "DANMU_MSG"://弹幕
                case "GUARD_BUY"://舰长
                case "USER_TOAST_MSG"://续费舰长
                case "SUPER_CHAT_MESSAGE"://SC
                case "SUPER_CHAT_MESSAGE_JPN"://日语SC
                case "SEND_GIFT"://礼物
                case "COMBO_SEND"://礼物
                    Log.WriteLine("cmd", cmd);
                    Log.WriteLine("data", obj.ToString());
                    break;
                default:
                    //Log.WriteLine("data", item);
                    //Log.WriteLine("data", obj["data"].ToString());
                    break;
            }
        }


        #region 心跳 & 发送消息
        /// <summary>
        /// 心跳
        /// </summary>
        /// <returns></returns>
        async Task HeartbeatLoop()
        {
            try
            {
                while (connect.Connected)
                {
                    //每30秒发送一次 心跳
                    await SendHeartbeatAsync();
                    await Task.Delay(30000);
                }
            }
            catch (Exception e)
            {
                Log.Error($"{e}");
                this.Dispose();
            }

            // 发送ping包
            async Task SendHeartbeatAsync() => await SendSocketDataAsync(2, " ", "bili发送心跳包");
        }
        /// <summary>
        /// 发送消息到bili
        /// </summary>
        /// <param name="action">  
        /// 2   心跳包
        /// 3	心跳包回复（人气值）
        /// 5	普通包（命令）
        /// 7	认证包
        /// 8	认证包回复
        /// <param name="body">消息体</param>
        /// <param name="remarks">备注</param>
        /// <returns></returns>
        async Task SendSocketDataAsync(int action, string body = "", string remarks = "")
        {
            //总长度 头大小 头部长度 心跳包 认证包/心跳包 自增 消息体
            await SendSocketDataAsync(0, connect.headLength, 1, action, 1, body, remarks);
        }

        /// <summary>
        /// 发送消息到bilibili
        /// https://github.com/SocialSisterYi/bilibili-API-collect/blob/master/docs/live/message_stream.md
        /// </summary>
        /// <param name="packetLength">总长度</param>
        /// <param name="headerLength">头部长度</param>
        /// <param name="version">协议版本
        /// 协议版本:
        ///0普通包正文不使用压缩
        ///1心跳及认证包正文不使用压缩
        ///2普通包正文使用zlib压缩
        ///3普通包正文使用brotli压缩,解压为一个带头部的协议0普通包
        /// </param>
        /// <param name="action">操作码
        /// 2	心跳包
        /// 3	心跳包回复（人气值）
        /// 5	普通包（命令）
        /// 7	认证包
        /// 8	认证包回复
        /// </param>
        /// <param name="sequence">每次发包时向上递增</param>
        /// <param name="body">消息体</param>
        /// <param name="remarks">备注</param>
        /// <returns></returns>
        async Task SendSocketDataAsync(int packetLength, short headerLength, short version, int action, int sequence = 1, string body = "", string remarks = "")
        {
            var playload = Encoding.UTF8.GetBytes(body);
            if (packetLength == 0) packetLength = playload.Length + 16;
            var buffer = new byte[packetLength];
            using (var ms = new MemoryStream(buffer))
            {
                var b = ConversionHelper.GetBytes(buffer.Length);

                await ms.WriteAsync(b, 0, 4);
                b = ConversionHelper.GetBytes(headerLength);
                await ms.WriteAsync(b, 0, 2);
                b = ConversionHelper.GetBytes(version);
                await ms.WriteAsync(b, 0, 2);
                b = ConversionHelper.GetBytes(action);
                await ms.WriteAsync(b, 0, 4);
                b = ConversionHelper.GetBytes(sequence);
                await ms.WriteAsync(b, 0, 4);
                if (playload.Length > 0)
                {
                    await ms.WriteAsync(playload, 0, playload.Length);
                }
                if (!string.IsNullOrWhiteSpace(remarks))
                {
                    var msg = Encoding.UTF8.GetString(buffer.Skip(connect.headLength).ToArray());
                    if (!string.IsNullOrWhiteSpace(msg.Trim()))
                    {
                        Log.WriteLine(remarks, msg);
                    }
                    else
                    {
                        Log.WriteLine(remarks);
                    }
                }
                //if (GModel.IsDev)
                //{
                //    Log.WriteLine("bili发送base64",Convert.ToBase64String(buffer));
                //    Log.WriteLine("bili发送16位byte", ConversionHelper.ByteToHexS(buffer));
                //}

                await _ws.SendAsync(buffer, WebSocketMessageType.Binary, true, connect.Cancellation);
            }
        }
        #endregion

        #region 接收消息
        /// <summary>
        /// 接收消息
        /// </summary>
        /// <returns></returns>
        void ReceiveMessageLoop()
        {

            var headBuffer = new byte[connect.headLength];
            //var headBuffer = new byte[16];
            while (connect.Connected)
            {
                #region 头信息
                _ws.ReceiveAsync(headBuffer, connect.Cancellation).Wait();
                if (GModel.IsDev) Log.WriteLine("bili接收头信息", ConversionHelper.ByteToHexS(headBuffer.Take(connect.headLength).ToArray()));
                //前4位标识封包总大小，coding时第一个byte有时会有脏数据导致接收byte错误，例
                //正常 00 00 03 14
                //错误 20 00 00 14
                //正常应该几百几千字节，这里为 788 字节，错误的 536870932（53亿）字节 500MB
                //所以这里跳过第一个字节，后三位能取最大值为16MB，对文本够用，一般也不会有这么长的数据过来
                int countLength = ConversionHelper.GetInt(headBuffer.Take(4).ToArray());
                if (countLength > 9999) countLength = 9999;
                //int countLength = ConversionHelper.GetInt(headBuffer.Skip(1).Take(3).ToArray());
                var bodyBuffer = new byte[countLength - connect.headLength];
                _ws.ReceiveAsync(bodyBuffer, connect.Cancellation).Wait();
                #endregion

                #region 协议版本
                int agreement = ConversionHelper.GetInt(headBuffer.Skip(6).Take(2).ToArray());
                string bodyMsg = "";
                List<string> bodys = new List<string>();
                switch (agreement)
                {
                    //普通正文
                    case 0:
                        bodyMsg = Encoding.UTF8.GetString(bodyBuffer);
                        bodys.Add(bodyMsg);
                        if (GModel.IsDev) Log.WriteLine("接收普通正文体信息", bodyMsg);
                        break;
                    //心跳|认证包
                    case 1:
                        if (GModel.IsDev) Log.WriteLine("接收认证/心跳包", Encoding.UTF8.GetString(bodyBuffer));
                        break;
                    //普通包 zlib 格式
                    case 2:
                        bodyMsg = Encoding.UTF8.GetString(ConversionHelper.ZlibDecompress(bodyBuffer));
                        bodys.Add(bodyMsg);
                        if (GModel.IsDev) Log.WriteLine("接收zlib普通包", bodyMsg);
                        break;
                    //普通包 brotli 格式，brotli解压后包含多条消息
                    case 3:
                        //解压
                        bodyBuffer = ConversionHelper.BrotliDecompress(bodyBuffer).ToArray();
                        //游标
                        int broCurr = 0;
                        int broNext = 0;
                        do
                        {
                            if (GModel.IsDev) Log.WriteLine("接收brotil普通包头信息分片" + broNext, ConversionHelper.ByteToHexS(bodyBuffer.Skip(broCurr).Take(connect.headLength).ToArray()));
                            //第一段 头/体/总
                            var broHeadLength = ConversionHelper.GetInt(bodyBuffer.Skip(broCurr + 4).Take(2).ToArray());
                            var broCountLength = ConversionHelper.GetInt(bodyBuffer.Skip(broCurr).Take(4).ToArray());
                            var broBodyLength = broCountLength - broHeadLength;
                            var broMsg = Encoding.UTF8.GetString(bodyBuffer.Skip(broCurr + broHeadLength).Take(broBodyLength).ToArray());
                            bodys.Add(broMsg);
                            if (GModel.IsDev) Log.WriteLine("接收brotil普通包体信息" + broNext, broMsg);
                            broCurr += broCountLength;
                            broNext++;
                        } while (broCurr < bodyBuffer.Length);
                        break;
                    default:
                        bodyMsg = Encoding.UTF8.GetString(bodyBuffer);
                        Log.WriteLine("接收default体信息", bodyMsg);
                        break;
                }
                foreach (var item in bodys)
                {
                    if ((agreement == 0 || agreement == 2 || agreement == 3) && item.Length > 0)
                    {
                        JoinQueue(JObject.Parse(item));
                    }
                }


                #endregion


            }
        }
        #endregion

        /// <summary>
        /// bili 链接对象
        /// </summary>
        private class Connect
        {
            //参考仓库：https://github.com/a820715049/BiliBiliLive
            public Connect()
            {
                WS = new ClientWebSocket();
            }
            public ClientWebSocket WS { get; set; }
            public int Roomid { get; set; }
            public Uri WsUrl { get; set; }
            public string Token { get; set; }
            public short headLength => 16;

            public CancellationToken Cancellation = new CancellationToken();

            public bool Connected => WS != null && (WS.State == WebSocketState.Connecting || WS.State == WebSocketState.Open);
        }


        public void Dispose()
        {
            _ws?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
