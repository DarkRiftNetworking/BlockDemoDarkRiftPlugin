using DarkRift;
using DarkRift.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlockDemoDarkRiftPlugin
{
    /// <summary>
    ///     Manages the players in the server.
    /// </summary>
    class BlockDemoPlayerManager : Plugin
    {
        /// <summary>
        ///     The version number of the plugin in SemVer form.
        /// </summary>
        public override Version Version => new Version(1, 0, 0);
        
        /// <summary>
        ///     Indicates that this plugin is thread safe and DarkRift can invoke events from 
        ///     multiple threads simultaneously.
        /// </summary>
        public override bool ThreadSafe => true;

        /// <summary>
        ///     The player in the server.
        /// </summary>
        Dictionary<IClient, Player> players = new Dictionary<IClient, Player>();

        public BlockDemoPlayerManager(PluginLoadData pluginLoadData) : base(pluginLoadData)
        {
            //Subscribe for notification when a new client connects
            ClientManager.ClientConnected += ClientManager_ClientConnected;

            //Subscribe for notifications when a new client disconnects
            ClientManager.ClientDisconnected += ClientManager_ClientDisconnected;
        }

        /// <summary>
        ///     Invoked when a new client connects.
        /// </summary>
        /// <param name="sender">The client manager.</param>
        /// <param name="e">The event arguments.</param>
        void ClientManager_ClientConnected(object sender, ClientConnectedEventArgs e)
        {
            //Spawn our new player on all other players
            Player player = new Player(new Vec3(0, 0, 0), new Vec3(0, 0, 0), e.Client.ID);
            using (Message message = Message.Create(BlockTags.SpawnPlayer, player))
            {
                foreach (IClient client in ClientManager.GetAllClients())
                {
                    if (client != e.Client)
                        client.SendMessage(message, SendMode.Reliable);
                }
            }

            lock (players)
                players.Add(e.Client, player);

            //Spawn all other players on our new player
            foreach (IClient client in ClientManager.GetAllClients())
            {
                Player p;
                lock (players)
                    p = players[client];

                using (Message message = Message.Create(BlockTags.SpawnPlayer, p))
                    e.Client.SendMessage(message, SendMode.Reliable);
            }

            //Subscribe to when this client sends messages
            e.Client.MessageReceived += Client_PlayerEvent;
        }

        /// <summary>
        ///     Invoked when a new client connects.
        /// </summary>
        /// <param name="sender">The client manager.</param>
        /// <param name="e">The event arguments.</param>
        private void ClientManager_ClientDisconnected(object sender, ClientDisconnectedEventArgs e)
        {
            players.Remove(e.Client);

            using (DarkRiftWriter writer = DarkRiftWriter.Create())
            {
                writer.Write(e.Client.ID);

                using (Message message = Message.Create(BlockTags.DespawnSplayer, writer))
                {
                    foreach (IClient client in ClientManager.GetAllClients())
                        client.SendMessage(message, SendMode.Reliable);
                }
            }
        }

        /// <summary>
        ///     Invoked when the client sends a Player message.
        /// </summary>
        /// <param name="sender">The client.</param>
        /// <param name="e">The event arguments.</param>
        void Client_PlayerEvent(object sender, MessageReceivedEventArgs e)
        {
            using (Message message = e.GetMessage() as Message)
            {
                //Check it's a movement message
                if (message != null && message.Tag == BlockTags.Movement)
                {
                    //Get the player in question
                    Player player;
                    lock (players)
                        player = players[e.Client];

                    //Deserialize the new position
                    Vec3 newPosition = message.Deserialize<Vec3>();
                    Vec3 newRotation = message.Deserialize<Vec3>();

                    lock (player)
                    {
                        //Update the player
                        player.Position = newPosition;
                        player.Rotation = newRotation;

                        //Serialize the whole player to the message so that we also include the ID
                        message.Serialize(player);
                    }

                    //Send to everyone else
                    foreach (IClient sendTo in ClientManager.GetAllClients().Except(new IClient[] { e.Client }))
                        sendTo.SendMessage(message, SendMode.Reliable);
                }
            }
        }

        /// <summary>
        ///     Holds serializable data about a player.
        /// </summary>
        private class Player : IDarkRiftSerializable
        {
            public Vec3 Position { get; set; }
            public Vec3 Rotation { get; set; }
            public uint ID { get; set; }

            public Player()
            {

            }

            public Player(Vec3 position, Vec3 rotation, uint ID)
            {
                this.Position = position;
                this.Rotation = rotation;
                this.ID = ID;
            }

            public void Deserialize(DeserializeEvent e)
            {
                this.Position = e.Reader.ReadSerializable<Vec3>();
                this.Rotation = e.Reader.ReadSerializable<Vec3>();
                this.ID = e.Reader.ReadUInt32();
            }

            public void Serialize(SerializeEvent e)
            {
                e.Writer.Write(Position);
                e.Writer.Write(Rotation);
                e.Writer.Write(ID);
            }
        }

        /// <summary>
        ///     A primative 3 axis vector.
        /// </summary>
        private class Vec3 : IDarkRiftSerializable
        {
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }

            public Vec3()
            {

            }

            public Vec3(float x, float y, float z)
            {
                this.X = x;
                this.Y = y;
                this.Z = z;
            }

            public void Deserialize(DeserializeEvent e)
            {
                this.X = e.Reader.ReadSingle();
                this.Y = e.Reader.ReadSingle();
                this.Z = e.Reader.ReadSingle();
            }

            public void Serialize(SerializeEvent e)
            {
                e.Writer.Write(X);
                e.Writer.Write(Y);
                e.Writer.Write(Z);
            }
        }
    }
}
