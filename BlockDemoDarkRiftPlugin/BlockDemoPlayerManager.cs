using DarkRift.Common;
using DarkRift.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlockDemoDarkRiftPlugin
{
    class BlockDemoPlayerManager : Plugin
    {
        /// <summary>
        ///     The tag that all spawn data will be sent on.
        /// </summary>
        const int SPAWN_TAG = 0;

        /// <summary>
        ///     The tag that all player data will be received on.
        /// </summary>
        const int MOVEMENT_TAG = 1;

        /// <summary>
        ///     The name of our plugin.
        /// </summary>
        public override string Name => nameof(BlockDemoPlayerManager);

        /// <summary>
        ///     The version number of the plugin in SemVer form.
        /// </summary>
        public override Version Version => new Version(1, 0, 0);

        public override bool ThreadSafe => false;

        Dictionary<Client, Player> players = new Dictionary<Client, Player>();

        public BlockDemoPlayerManager(PluginLoadData pluginLoadData) : base(pluginLoadData)
        {
            //Subscribe for notification when a new client connects
            ClientManager.ClientConnected += ClientManager_ClientConnected;
        }

        /// <summary>
        ///     Invoked when a new client connects.
        /// </summary>
        /// <param name="sender">The client manager.</param>
        /// <param name="e">The event arguments.</param>
        void ClientManager_ClientConnected(object sender, ClientConnectedEventArgs e)
        {
            //Spawn our new player on all other players
            Player player = new Player(0, 0, 0, e.Client.GlobalID);
            foreach (Client client in ClientManager.GetAllClients())
            {
                if (client != e.Client)
                    client.SendMessage(new TagSubjectMessage(SPAWN_TAG, 0, player), SendMode.Reliable);
            }

            players.Add(e.Client, player);

            //Spawn all other players on our new player
            foreach (Client client in ClientManager.GetAllClients())
                e.Client.SendMessage(new TagSubjectMessage(SPAWN_TAG, 0, players[client]), SendMode.Reliable);

            //Subscribe to when this client sends PLAYER messages
            e.Client.Subscribe(MOVEMENT_TAG, Client_PlayerEvent);
        }

        /// <summary>
        ///     Invoked when the client sends a Player message.
        /// </summary>
        /// <param name="sender">The client.</param>
        /// <param name="e">The event arguments.</param>
        void Client_PlayerEvent(object sender, MessageReceivedEventArgs e)
        {
            //Send to everyone else
            IEnumerable<Client> others = ClientManager.GetAllClients().Intersect(new Client[] { (Client)sender });
            e.DistributeTo.UnionWith(others);
        }

        private class Player : IDarkRiftSerializable
        {
            float X { get; set; }
            float Y { get; set; }
            float Z { get; set; }
            uint ID { get; set; }

            public Player(float x, float y, float z, uint id)
            {
                this.X = x;
                this.Y = y;
                this.Z = z;
                this.ID = id;
            }

            public Player(DeserializeEvent e)
            {
                this.X = e.Reader.ReadSingle();
                this.Y = e.Reader.ReadSingle();
                this.Z = e.Reader.ReadSingle();
                this.ID = e.Reader.ReadUInt32();
            }

            public void Serialize(SerializeEvent e)
            {
                e.Writer.Write(X);
                e.Writer.Write(Y);
                e.Writer.Write(Z);
                e.Writer.Write(ID);
            }
        }
    }
}
