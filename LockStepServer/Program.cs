using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.Sockets.Kcp;
using System.Net.Sockets.Kcp.Simple;
using MR.Net;
using ProtoBuf;

public class Program
{
    public static int m_FrameDeltaTime = 1000 / 30;
    public static uint m_CurFrame;
    public static List<PBInput> m_ControlData = new List<PBInput>();
    public static List<KcpService.Client> m_Players = new List<KcpService.Client>();
    public static DateTime m_StartTime;
    public static DateTime m_LastTime;
    public static bool m_GameBegin = false;
    public static void Main(string[] args)
    {
        Console.WriteLine("服务启动");
        
        KcpService kcpServer = new KcpService(8848);
        kcpServer.onNewClient += client =>
        {
            lock (m_Players)
            {
                m_Players.Add(client);
                if (m_Players.Count > 0)
                {
                    m_GameBegin = true;
                    m_StartTime = DateTime.UtcNow;
                }
            }

            Task.Run(async () =>
            {
                while (true)
                {
                    var mes = await client.ReceiveAsync();
                    C2SInput data = new C2SInput();
                    using (MemoryStream memoryStream = new MemoryStream(mes))
                    {
                        data = Serializer.Deserialize<C2SInput>(memoryStream);
                    }
                
                    if (data.playerInput != null)
                    {
                        lock (m_ControlData)
                        {
                            m_ControlData.Add(data.playerInput);
                        }
                        Console.WriteLine($"接收到操作 {data.playerInput.Type}");
                    }
                }
            });
        };

        // kcpServer.onReceive += buffer =>
        // {
        //     begin = true;
        //     C2SInput data = new C2SInput();
        //     using (MemoryStream memoryStream = new MemoryStream(buffer))
        //     {
        //         data = Serializer.Deserialize<C2SInput>(memoryStream);
        //     }
        //     
        //     if (data.playerInput != null)
        //     {
        //         lock (m_ControlData)
        //         {
        //             m_ControlData.Add(data.playerInput);
        //         }
        //         Console.WriteLine($"接收到操作 {data.playerInput.Type}");
        //     }
        // };
        // kcpClient.kcp.TraceListener = new ConsoleTraceListener();

        // Task.Run(async () =>
        // {
        //     while (true)
        //     {
        //         kcpClient.kcp.Update(DateTimeOffset.UtcNow);
        //         await Task.Delay(10);
        //     }
        // });
        Task.Run(async () =>
        {
            m_LastTime = m_StartTime = DateTime.UtcNow;
            while (true)
            {
                if (m_GameBegin)
                {
                    while (Math.Floor((DateTime.UtcNow - m_StartTime).TotalSeconds * 30) > m_CurFrame)
                    {
                        // m_LastTime = m_LastTime.AddMilliseconds(m_FrameDeltaTime);
                        m_CurFrame++;
                        
                        S2CFrameMessage frameMessage = new S2CFrameMessage();
                        frameMessage.frameId = m_CurFrame;
                        
                        lock (m_ControlData)
                        {
                            if (m_ControlData.Count > 0)
                            {
                                frameMessage.Messages.AddRange(m_ControlData);
                                m_ControlData.Clear();
                            }
                        }
                
                        byte[] buffer = null;
                        using (MemoryStream ms = new MemoryStream())
                        {
                            Serializer.Serialize<S2CFrameMessage>(ms, frameMessage);
                            ms.Position = 0;
                            int length = (int)ms.Length;
                            buffer = new byte[length];
                            ms.Read(buffer, 0, length);
                        }

                        lock (m_Players)
                        {
                            foreach (var client in m_Players)
                            {
                                client.Send(buffer, buffer.Length);
                            }
                        }
                    }
                }

                await Task.Delay(10);
            }
        });
    
        
        // StartReceive(kcpServer);
        Console.ReadLine();
    }
    
    // static async void StartReceive(KcpService client)
    // {
    //     while (true)
    //     {
    //         var mes = await client.ReceiveAsync();
    //         begin = true;
    //         C2SInput data = new C2SInput();
    //         using (MemoryStream memoryStream = new MemoryStream(mes))
    //         {
    //             data = Serializer.Deserialize<C2SInput>(memoryStream);
    //         }
    //
    //         if (data.playerInput != null)
    //         {
    //             lock (m_ControlData)
    //             {
    //                 m_ControlData.Add(data.playerInput);
    //             }
    //             Console.WriteLine($"接收到操作 {data.playerInput.Type}");
    //         }
    //     }
    // }
}