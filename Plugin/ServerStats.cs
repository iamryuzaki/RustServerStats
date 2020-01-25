using System;
using System.Collections.Generic;
using System.Linq;
#if RUST
using Facepunch;
#endif
using Newtonsoft.Json;
using UnityEngine;
using WebSocketSharp;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    // FullChangeLog: https://github.com/iamryuzaki/RustServerStats/blob/master/Plugin/ServerStat.Changelog.txt
    // WebPanel: http://server-stats.gamewer.ru/
    // Thanks: Мизантроп
    // ChangeLog:
    //    - 1.0.1
    //        * The plugin is adapted for all games on Oxide
    //        * Change remote server Addr
    //        * Remove rude using in plugin
    //        * Add server password and config
            
    [Info("ServerStats", "TheRyuzaki", "1.0.1")]
    public class ServerStats : CovalencePlugin
    {
        private static ServerStats Instance { get; set; } = null;

        private WebSocketSharp.WebSocket WebSocketClient = new WebSocket("ws://s1.server-stats.gamewer.ru:5191");
        private bool NetworkStatus = false;
        private bool HaveSubscribers = false;
        private bool HasUnloading = false;
        
        
        protected override void LoadDefaultConfig()
        {
            this.Config["Password"] = Random.Range(1000, 9999);
            this.LogWarning("Config file ServerStats.json is not found, you new password: " + this.Config["Password"]);

            this.Config.Save();
        }

        void Init()
        {
            Instance = this;
        }

        void OnServerInitialized()
        {
            this.Config.Load(); 
            
            this.WebSocketClient.OnOpen += OnNetworkConnected;
            this.WebSocketClient.OnClose += OnNetworkClose;
            this.WebSocketClient.OnMessage += OnNetworkMessage;
            this.timer.Repeat(1, 0, this.DoServerStats);
            this.DoNetworkConnect();
        }

        void Unload()
        {
            this.HasUnloading = true;
            this.WebSocketClient.CloseAsync();
        }

        private void OnNetworkMessage(object sender, MessageEventArgs e)
        {
            try
            {
                Dictionary<string, object> packet = JsonConvert.DeserializeObject<Dictionary<string, object>>(e.Data);
                object method = string.Empty;
                if (packet.TryGetValue("method", out method))
                {
                    switch ((string)method)
                    {
                        case "haveSubscribers":
                            this.HaveSubscribers = true;
                            break;
                        case "notHaveSubscribers":
                            this.HaveSubscribers = false;
                            break;
                    }
                }
            }
            catch
            {
                
            }
        }

        private void OnNetworkClose(object sender, CloseEventArgs e)
        {
            if (this.NetworkStatus != false)
            {
                this.Puts("NetworkStatus: false");
            }
            this.NetworkStatus = false;
            HaveSubscribers = false;
            if (this.HasUnloading == false)
            {
                this.timer.Once(10f, this.DoNetworkConnect);
            }
        }

        private void OnNetworkConnected(object sender, EventArgs e)
        {
            if (this.NetworkStatus != true)
            {
                this.Puts("NetworkStatus: true");
            }
            this.NetworkStatus = true;
#if RUST
            NetworkWelcomePacket packet = Pool.Get<NetworkWelcomePacket>();
#else
            NetworkWelcomePacket packet = new NetworkWelcomePacket();
#endif
            this.WebSocketClient.SendAsync(JsonConvert.SerializeObject(packet), (res) => { });
#if RUST
            Pool.Free(ref packet);
#endif
        }

        void DoServerStats()
        {
            if (this.NetworkStatus == true && this.HaveSubscribers == true)
            {
                var listPlugins = this.plugins.PluginManager.GetPlugins().ToArray();
#if RUST
                NetworkTickPacket packet = Pool.Get<NetworkTickPacket>();
#else
                NetworkTickPacket packet = new NetworkTickPacket();
#endif
                QueueWorkerThread(_ =>
                {
                    for (var i = 0; i < listPlugins.Length; i++)
                    {
                        packet.ListPlugins.Add(new NetworkTickPacket.PluginItem
                        {
                            Name = listPlugins[i].Name,
                            Version = listPlugins[i].Version.ToString(),
                            Author = listPlugins[i].Author,
                            Hash = listPlugins[i].Name.GetHashCode(),
                            Time = listPlugins[i].TotalHookTime
                        });
                    }

                    if (this.NetworkStatus == true && this.HaveSubscribers == true)
                        this.WebSocketClient.SendAsync(JsonConvert.SerializeObject(packet), (res) => { });
#if RUST
                    Pool.Free(ref packet);
#endif
                });
            }
        }

        void DoNetworkConnect()
        {
            if (this.NetworkStatus == false && this.HasUnloading == false)
            {
                this.WebSocketClient.Connect();
            }
        }

#if RUST
        public class NetworkWelcomePacket : Pool.IPooled
#else
        public class NetworkWelcomePacket
#endif
        {
            [JsonProperty("method")]
            public string Method { get; } = "reg_server";
            [JsonProperty("serverHash")]
            public int ServerHash { get; } = Instance.server.Name.GetHashCode();
            [JsonProperty("serverName")]
            public string ServerName { get; } = Instance.server.Name;
            [JsonProperty("password")] 
            public string Password => Instance.Config["Password"].ToString();

            public void EnterPool()
            {
                
            }

            public void LeavePool()
            {
                
            }
        }

#if RUST
        public class NetworkTickPacket: Pool.IPooled
#else
        public class NetworkTickPacket
#endif
        {
            [JsonProperty("method")]
            public string Method { get; } = "tick_server";
            [JsonProperty("listPlugins")]
#if RUST
            public List<PluginItem> ListPlugins { get; } = Pool.GetList<PluginItem>();
#else
            public List<PluginItem> ListPlugins { get; } = new List<PluginItem>();
#endif

            [JsonProperty("fps")]
#if RUST
            public int Fps => global::Performance.current.frameRate;
#else
            public int Fps => Mathf.RoundToInt(1f / UnityEngine.Time.smoothDeltaTime);
#endif
            [JsonProperty("ent")]
#if RUST
            public int Ent => BaseNetworkable.serverEntities.Count;
#else
            public int Ent => 0;
#endif
            [JsonProperty("online")]
            public int Online => Instance.players.Connected.Count();//BasePlayer.activePlayerList.Count;

            public void EnterPool()
            {
                this.ListPlugins.Clear();
            }

            public void LeavePool()
            {
                
            }

            public struct PluginItem
            {
                [JsonProperty("name")]
                public string Name { get; set; }
                [JsonProperty("author")]
                public string Author { get; set; }
                [JsonProperty("version")]
                public string Version { get; set; }
                [JsonProperty("hash")]
                public int Hash { get; set; }
                [JsonProperty("time")]
                public double Time { get; set; }
            }
        }
        
    }
}
