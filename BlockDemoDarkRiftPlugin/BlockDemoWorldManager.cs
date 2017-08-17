using DarkRift;
using DarkRift.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlockDemoDarkRiftPlugin
{
    public class BlockDemoWorldManager : Plugin
    {
        /// <summary>
        ///     The tag that all world data will be received on.
        /// </summary>
        const byte WORLD_TAG = 2;

        /// <summary>
        ///     The subject for place block events.
        /// </summary>
        const ushort PLACE_BLOCK_SUBJECT = 0;

        /// <summary>
        ///     The subject for destroy block events.
        /// </summary>
        const ushort DESTROY_BLOCK_SUBJECT = 1;
        
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
        ///     The blocks that have been added to the world.
        /// </summary>
        /// <remarks>
        ///     The block primitives are structs with custom hash functions and equality testing so
        ///     a hash set is a very efficient way of storing and retrieving them.
        /// </remarks>
        HashSet<Block> blocks = new HashSet<Block>();

        public BlockDemoWorldManager(PluginLoadData pluginLoadData) : base(pluginLoadData)
        {
            //Build a basic floor
            for (int x = -5; x <= 5; x++)
            {
                for (int z = -5; z <= 5; z++)
                {
                    blocks.Add(new Block(x, -2, z));
                }
            }

            //Subscribe for notification when a new client connects
            ClientManager.ClientConnected += ClientManager_ClientConnected;
        }

        /// <summary>
        ///     Invoked when a new client connects.
        /// </summary>
        /// <param name="sender">The client manager that initiated the event.</param>
        /// <param name="e">The event arguments.</param>
        void ClientManager_ClientConnected(object sender, ClientConnectedEventArgs e)
        {
            //When new clients connect we subscribe to when they send data on the WORLD tag.
            e.Client.MessageReceived += Client_WorldEvent;

            //Send back the current world - ideally this would be in a single message but that's beyond this simple
            //demo
            lock (blocks)
            {
                foreach (Block block in blocks)
                    e.Client.SendMessage(new TagSubjectMessage(WORLD_TAG, 0, block), SendMode.Reliable);
            }
        }

        /// <summary>
        ///     Invoked whenever data is received from a client.
        /// </summary>
        /// <param name="sender">The client that sent the data.</param>
        /// <param name="e">The event arguments.</param>
        void Client_WorldEvent(object sender, MessageReceivedEventArgs e)
        {
            TagSubjectMessage message = e.Message as TagSubjectMessage;

            //Check it's tag
            if (message != null && message.Tag == WORLD_TAG)
            {
                //Extract the client and message
                Client client = (Client)sender;

                //If the client sent too much or too little data then strike them for future reference
                if (message.GetReader().Length != 12)
                {
                    client.Strike("Malformed world event received.");
                    return;
                }

                //Extract block information
                Block block = message.Deserialize<Block>();

                //Snap the block to the 1x1x1 grid
                block.SnapToGrid();

                //Choose what to do with the event
                switch (message.Subject)
                {
                    case PLACE_BLOCK_SUBJECT:
                        lock (blocks)
                        {
                            //Add the new block they placed!
                            bool success = blocks.Add(block);

                            //If the block was already present return
                            if (!success)
                                return;
                        }

                        break;

                    case DESTROY_BLOCK_SUBJECT:
                        //Destroy the block they requested!
                        lock (blocks)
                        {
                            //Find block
                            bool success = blocks.Remove(block);

                            //If the block couldn't be removed ignore the request
                            if (!success)
                                return;
                        }

                        break;
                }

                //Since we've snapped the block to the grid we need to make sure that the message contains the latest 
                //block position
                message.Serialize(block);

                //Finally add all clients to the distribution list so the message is passed onto them and they can show the
                //placement of the block
                e.DistributeTo.UnionWith(ClientManager.GetAllClients());
            }
        }
        
        /// <summary>
        ///     A basic block primitive.
        /// </summary>
        private struct Block : IDarkRiftSerializable
        {
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }

            public Block(float x, float y, float z)
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
                e.Writer.Write(this.X);
                e.Writer.Write(this.Y);
                e.Writer.Write(this.Z);
            }

            /// <summary>
            ///     Snaps the block onto the grid.
            /// </summary>
            public void SnapToGrid()
            {
                this.X = (float)Math.Round(this.X);
                this.Y = (float)Math.Round(this.Y);
                this.Z = (float)Math.Round(this.Z);
            }
            
            /// <summary>
            ///     Compares an object for equality with this.
            /// </summary>
            /// <param name="obj">The object to compare with.</param>
            /// <returns>Whether the object is equal to this block.</returns>
            public override bool Equals(object obj)
            {
                if (obj == null || !(obj is Block))
                    return false;

                Block b = (Block)obj;

                return this.X == b.X && this.Y == b.Y && this.Z == b.Z;
            }

            /// <summary>
            ///     Basic hashcode generator based on the position of the object.
            /// </summary>
            /// <returns>The block's hashed position.</returns>
            public override int GetHashCode()
            {
                return (int)(this.X * 23 + this.Y * 29 + this.Z * 31);
            }
        }
    }
}
